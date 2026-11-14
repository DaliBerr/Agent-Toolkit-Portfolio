from __future__ import annotations

import contextlib
import hashlib
import http.server
import importlib.util
import json
import os
from pathlib import Path
import py_compile
import subprocess
import sys
import tempfile
import threading
import time
import unittest


SKILL_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = SKILL_ROOT / "scripts" / "unity_ops.py"
SKILL_DOC = SKILL_ROOT / "SKILL.md"
EXPECTED_ROUTES = {
    "unity4": ("UnityProject4", "http://127.0.0.1:8080/mcp", "unityMCP_0"),
    "colo": ("Colo-3D", "http://127.0.0.1:8081/mcp", "unityMCP_1"),
    "lilith": ("lilith", "http://127.0.0.1:8082/mcp", "unityMCP_2"),
    "arttest": ("arttest", "http://127.0.0.1:8083/mcp", "unityMCP_3"),
}


def load_ops_module():
    spec = importlib.util.spec_from_file_location("unity_ops_under_test", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load {SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    previous = sys.dont_write_bytecode
    sys.dont_write_bytecode = True
    try:
        spec.loader.exec_module(module)
    finally:
        sys.dont_write_bytecode = previous
    return module


ops = load_ops_module()


def run_cli(*args: str, input_text: str | None = None, timeout: float = 10.0, expect_json: bool = True):
    completed = subprocess.run(
        [sys.executable, str(SCRIPT), *args],
        input=input_text,
        text=True,
        capture_output=True,
        timeout=timeout,
        check=False,
    )
    stdout = completed.stdout.strip()
    if expect_json:
        try:
            payload = json.loads(stdout) if stdout else None
        except json.JSONDecodeError as exc:
            raise AssertionError(
                f"CLI did not emit compact JSON: rc={completed.returncode}, stdout={stdout!r}, stderr={completed.stderr!r}"
            ) from exc
    else:
        payload = None
    return completed.returncode, payload, completed


def assert_no_atomic_temps(testcase: unittest.TestCase, state_root: Path):
    leftovers = sorted(path for path in state_root.rglob(".tmp-*.json") if path.is_file())
    testcase.assertEqual([], leftovers, f"atomic broker temp files leaked: {leftovers}")


class FakeMcpHandler(http.server.BaseHTTPRequestHandler):
    server_version = "FakeMcp/1.0"

    def log_message(self, _format, *_args):
        return

    def _reply(self, body: bytes, content_type: str, session_id: str | None = None, status: int = 200):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        if session_id:
            self.send_header("mcp-session-id", session_id)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        if body:
            try:
                self.wfile.write(body)
            except (BrokenPipeError, ConnectionAbortedError, OSError):
                # Expected when the client timeout closes the test socket first.
                return

    def _json(self, payload, session_id: str | None = None, status: int = 200):
        self._reply(
            json.dumps(payload, separators=(",", ":")).encode("utf-8"),
            "application/json",
            session_id=session_id,
            status=status,
        )

    def do_POST(self):  # noqa: N802 - BaseHTTPRequestHandler API
        length = int(self.headers.get("Content-Length", "0"))
        request = json.loads(self.rfile.read(length).decode("utf-8"))
        self.server.requests.append((request, dict(self.headers)))
        method = request.get("method")
        session_id = "fake-session-42"
        if method == "initialize":
            self._json(
                {
                    "jsonrpc": "2.0",
                    "id": request["id"],
                    "result": {"protocolVersion": "2025-03-26", "serverInfo": {"name": "fake"}},
                },
                session_id=session_id,
            )
            return
        if method == "notifications/initialized":
            self._reply(b"", "application/json", status=204)
            return
        if method == "tools/list":
            notification = {
                "jsonrpc": "2.0",
                "method": "notifications/progress",
                "params": {"progress": 1},
            }
            result = {
                "jsonrpc": "2.0",
                "id": request["id"],
                "result": {"tools": [{"name": "read_console"}]},
            }
            data = (
                f"data: {json.dumps(notification, separators=(',', ':'))}\n\n"
                f"data: {json.dumps(result, separators=(',', ':'))}\n\n"
            ).encode("utf-8")
            self._reply(data, "text/event-stream", session_id=session_id)
            return
        if method == "resources/read":
            self._json(
                {
                    "jsonrpc": "2.0",
                    "id": request["id"],
                    "result": {"contents": [{"uri": request["params"]["uri"], "text": "state"}]},
                },
                session_id=session_id,
            )
            return
        if method == "tools/call" and request.get("params", {}).get("arguments", {}).get("error"):
            self._json(
                {
                    "jsonrpc": "2.0",
                    "id": request["id"],
                    "error": {"code": -32001, "message": "fake tool failure"},
                },
                session_id=session_id,
            )
            return
        if method == "timeout":
            time.sleep(self.server.response_delay)
            self._json(
                {"jsonrpc": "2.0", "id": request["id"], "result": {"ok": True}},
                session_id=session_id,
            )
            return
        self._json(
            {"jsonrpc": "2.0", "id": request["id"], "result": {"echo": request.get("params", {})}},
            session_id=session_id,
        )


class FakeMcpServer(http.server.ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = True

    def __init__(self):
        super().__init__(("127.0.0.1", 0), FakeMcpHandler)
        self.requests = []
        self.response_delay = 0.0


@contextlib.contextmanager
def fake_mcp_endpoint():
    server = FakeMcpServer()
    thread = threading.Thread(target=server.serve_forever, name="fake-mcp", daemon=True)
    thread.start()
    try:
        yield server, f"http://127.0.0.1:{server.server_port}/mcp"
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)


class UnityOpsTests(unittest.TestCase):
    def test_streamable_http_initialize_sse_and_operations(self):
        with fake_mcp_endpoint() as (server, endpoint):
            client = ops.StreamableHttpClient(endpoint, 1000)
            tools = client.call("tools/list", {})
            resource = client.call("resources/read", {"uri": "mcpforunity://editor/state"})
            called = client.call("tools/call", {"name": "read_console", "arguments": {"limit": 3}})

            self.assertEqual({"tools": [{"name": "read_console"}]}, tools)
            self.assertEqual("state", resource["contents"][0]["text"])
            self.assertEqual({"echo": {"name": "read_console", "arguments": {"limit": 3}}}, called)
            self.assertEqual("fake-session-42", client.session_id)

            self.assertGreaterEqual(len(server.requests), 5)
            initialize, init_headers = server.requests[0]
            self.assertEqual("initialize", initialize["method"])
            self.assertIn("application/json", init_headers["Accept"])
            self.assertIn("text/event-stream", init_headers["Accept"])
            self.assertEqual("application/json", init_headers["Content-Type"])
            self.assertEqual("notifications/initialized", server.requests[1][0]["method"])
            self.assertEqual("fake-session-42", server.requests[1][1]["Mcp-Session-Id"])
            for request, headers in server.requests[2:]:
                if request.get("id"):
                    self.assertEqual("fake-session-42", headers["Mcp-Session-Id"])

    def test_streamable_http_errors_and_timeout_are_compact(self):
        with fake_mcp_endpoint() as (server, endpoint):
            client = ops.StreamableHttpClient(endpoint, 1000)
            with self.assertRaises(ops.CommandError) as error:
                client.call("tools/call", {"name": "read_console", "arguments": {"error": True}})
            self.assertIn("MCP tools/call failed", str(error.exception))
            self.assertNotIn("\n", str(error.exception))

            server.response_delay = 0.25
            timeout_client = ops.StreamableHttpClient(endpoint, 20)
            with self.assertRaises(ops.CommandError) as timeout_error:
                timeout_client.call("timeout", {})
            self.assertIn("could not reach MCP endpoint", str(timeout_error.exception))
            self.assertNotIn("\n", str(timeout_error.exception))

    def test_broker_schema_aliases_lifecycle_and_wait(self):
        with tempfile.TemporaryDirectory(prefix="unity-ops-broker-") as raw_root:
            root = Path(raw_root)
            for alias, expected in {
                "lilith": EXPECTED_ROUTES["lilith"],
                "LILITH": EXPECTED_ROUTES["lilith"],
                "unity4": EXPECTED_ROUTES["unity4"],
                "UnityProject4": EXPECTED_ROUTES["unity4"],
                "colo": EXPECTED_ROUTES["colo"],
                "Colo-3D": EXPECTED_ROUTES["colo"],
                "arttest": EXPECTED_ROUTES["arttest"],
            }.items():
                rc, payload, _ = run_cli("route", alias)
                self.assertEqual(0, rc)
                self.assertEqual(expected[0], payload["project"])
                self.assertEqual(expected[1], payload["endpoint"])
                self.assertEqual(expected[2], payload["expected_namespace"])

            rc, payload, _ = run_cli(
                "broker",
                "submit",
                "LILITH",
                "--requester-role",
                "tester",
                "--kind",
                "resource-read",
                "--name",
                "editor_state",
                "--arguments",
                '{"uri":"mcpforunity://editor/state"}',
                "--request-id",
                "read-1",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            request = payload["request"]
            self.assertEqual("pending", payload["state"])
            self.assertEqual(2, request["schema_version"])
            self.assertEqual("read-1", request["request_id"])
            self.assertEqual("lilith", request["project"])
            self.assertEqual(EXPECTED_ROUTES["lilith"][1], request["route"]["endpoint"])
            self.assertEqual(EXPECTED_ROUTES["lilith"][2], request["route"]["expected_namespace"])
            self.assertEqual("tester", request["requester_role"])
            self.assertEqual("read", request["mode"])
            self.assertIsNone(request["lease_id"])
            self.assertEqual("resource-read", request["operation"]["kind"])
            self.assertIsInstance(request["operation"]["arguments"], dict)

            rc, payload, _ = run_cli(
                "broker",
                "submit",
                "lilith",
                "--requester-role",
                "tester",
                "--kind",
                "tool-call",
                "--name",
                "refresh_unity",
                "--mode",
                "write",
                "--request-id",
                "write-without-lease",
                "--state-root",
                str(root),
            )
            self.assertEqual(2, rc)
            self.assertIn("lease_id", payload["error"])

            rc, payload, _ = run_cli("broker", "claim", "lilith", "--state-root", str(root))
            self.assertEqual(0, rc)
            self.assertEqual("claimed", payload["state"])
            self.assertEqual("read-1", payload["request"]["request_id"])
            rc, payload, _ = run_cli(
                "broker",
                "complete",
                "read-1",
                "--result",
                '{"ok":true,"value":7}',
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            self.assertEqual("completed", payload["state"])
            self.assertEqual({"ok": True, "value": 7}, payload["request"]["result"])

            rc, payload, _ = run_cli(
                "broker",
                "submit",
                "lilith",
                "--requester-role",
                "worker",
                "--kind",
                "tool-call",
                "--name",
                "refresh_unity",
                "--arguments",
                '{"compile":"request"}',
                "--mode",
                "write",
                "--lease-id",
                "WORK-LEASE-1",
                "--preconditions",
                '{"snapshot":"fixture","prefab_stage":{"is_open":false,"is_dirty":false},"play_mode":false}',
                "--expected-readback",
                '{"kind":"resource-read","uri":"mcpforunity://editor/state"}',
                "--request-id",
                "write-1",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            self.assertEqual("pending", payload["state"])
            self.assertEqual("request", payload["request"]["operation"]["arguments"]["compile"])
            rc, payload, _ = run_cli("broker", "claim", "lilith", "--state-root", str(root))
            self.assertEqual(0, rc)
            self.assertEqual("write-1", payload["request"]["request_id"])
            rc, payload, _ = run_cli(
                "broker", "complete", "write-1", "--result", '{"ok":true}', "--state-root", str(root)
            )
            self.assertEqual(0, rc)

            rc, payload, _ = run_cli(
                "broker",
                "submit",
                "lilith",
                "--requester-role",
                "tester",
                "--kind",
                "tool-call",
                "--name",
                "read_console",
                "--request-id",
                "fail-1",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            rc, payload, _ = run_cli("broker", "claim", "lilith", "--state-root", str(root))
            self.assertEqual(0, rc)
            self.assertEqual("fail-1", payload["request"]["request_id"])
            rc, payload, _ = run_cli(
                "broker",
                "fail",
                "fail-1",
                "--error",
                "fake failure",
                "--details",
                '{"retryable":false}',
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            self.assertEqual("failed", payload["state"])
            self.assertEqual(False, payload["request"]["error"]["details"]["retryable"])

            rc, payload, _ = run_cli(
                "broker",
                "submit",
                "lilith",
                "--requester-role",
                "tester",
                "--kind",
                "tool-call",
                "--name",
                "read_console",
                "--request-id",
                "pending-1",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            rc, payload, _ = run_cli(
                "broker",
                "wait",
                "pending-1",
                "--timeout-ms",
                "25",
                "--interval-ms",
                "5",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            self.assertEqual("timeout", payload["state"])
            self.assertEqual("pending-1", payload["request_id"])

            rc, payload, _ = run_cli(
                "broker",
                "status",
                "--project",
                "Lilith",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            states = {item["state"] for item in payload["requests"]}
            self.assertEqual({"completed", "failed", "pending"}, states)
            assert_no_atomic_temps(self, root)

    def test_broker_lock_claim_validation_and_playmode_rejection(self):
        with tempfile.TemporaryDirectory(prefix="unity-ops-lock-") as raw_root:
            root = Path(raw_root)
            rc, payload, _ = run_cli(
                "broker",
                "submit",
                "lilith",
                "--requester-role",
                "tester",
                "--kind",
                "tool-call",
                "--name",
                "read_console",
                "--request-id",
                "locked-1",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            lock_name = hashlib.sha256(b"http://127.0.0.1:8082/mcp").hexdigest() + ".lock"
            lock_path = root / "locks" / lock_name
            lock_path.write_text("fixture", encoding="utf-8")
            rc, payload, _ = run_cli(
                "broker",
                "claim",
                "lilith",
                "--lock-timeout-ms",
                "10",
                "--state-root",
                str(root),
            )
            self.assertEqual(2, rc)
            self.assertIn("lock timed out", payload["error"])
            lock_path.unlink()
            rc, payload, _ = run_cli("broker", "claim", "lilith", "--state-root", str(root))
            self.assertEqual(0, rc)
            self.assertEqual("claimed", payload["state"])
            self.assertFalse(lock_path.exists())
            rc, _, _ = run_cli("broker", "complete", "locked-1", "--state-root", str(root))
            self.assertEqual(0, rc)

            invalid = {
                "schema_version": 2,
                "request_id": "invalid-write",
                "project": "lilith",
                "route": {
                    "endpoint": EXPECTED_ROUTES["lilith"][1],
                    "expected_namespace": EXPECTED_ROUTES["lilith"][2],
                },
                "operation": {"kind": "tool-call", "name": "refresh_unity", "arguments": {}},
                "mode": "write",
                "timeout_ms": 1000,
                "lease_id": None,
                "requester": "fixture",
                "requester_role": "tester",
                "preconditions": {},
                "expected_readback": {},
            }
            (root / "pending" / "invalid-write.json").write_text(
                json.dumps(invalid), encoding="utf-8"
            )
            rc, payload, _ = run_cli("broker", "claim", "lilith", "--state-root", str(root))
            self.assertEqual(2, rc)
            self.assertIn("lease_id", payload["error"])

            rc, payload, _ = run_cli(
                "broker",
                "submit",
                "lilith",
                "--requester-role",
                "tester",
                "--kind",
                "tool-call",
                "--name",
                "manage_editor",
                "--arguments",
                '{"mode":"PlayMode"}',
                "--request-id",
                "play-1",
                "--state-root",
                str(root),
            )
            self.assertEqual(2, rc)
            self.assertIn("PlayMode", payload["error"])
            assert_no_atomic_temps(self, root)

    def test_broker_cleanup_only_old_terminal_files_and_atomicity(self):
        with tempfile.TemporaryDirectory(prefix="unity-ops-cleanup-") as raw_root:
            root = Path(raw_root)

            def submit_and_claim(request_id: str):
                rc, _payload, _ = run_cli(
                    "broker",
                    "submit",
                    "lilith",
                    "--requester-role",
                    "tester",
                    "--kind",
                    "tool-call",
                    "--name",
                    "read_console",
                    "--request-id",
                    request_id,
                    "--state-root",
                    str(root),
                )
                self.assertEqual(0, rc)
                rc, claim, _ = run_cli("broker", "claim", "lilith", "--state-root", str(root))
                self.assertEqual(0, rc)
                self.assertEqual(request_id, claim["request"]["request_id"])

            submit_and_claim("old-complete")
            rc, _payload, _ = run_cli(
                "broker",
                "complete",
                "old-complete",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            submit_and_claim("old-failed")
            rc, _payload, _ = run_cli(
                "broker",
                "fail",
                "old-failed",
                "--error",
                "fixture",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)

            submit_and_claim("keep-claimed")
            rc, _payload, _ = run_cli(
                "broker",
                "submit",
                "lilith",
                "--requester-role",
                "tester",
                "--kind",
                "tool-call",
                "--name",
                "read_console",
                "--request-id",
                "keep-pending",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)

            old_timestamp = time.time() - 60
            for state, request_id in (("completed", "old-complete"), ("failed", "old-failed"), ("pending", "keep-pending"), ("claimed", "keep-claimed")):
                os.utime(root / state / f"{request_id}.json", (old_timestamp, old_timestamp))

            rc, payload, _ = run_cli(
                "broker",
                "cleanup",
                "--older-than-ms",
                "1000",
                "--state-root",
                str(root),
            )
            self.assertEqual(0, rc)
            self.assertEqual(
                {"old-complete", "old-failed"},
                {item["request_id"] for item in payload["deleted"]},
            )
            self.assertTrue((root / "pending" / "keep-pending.json").exists())
            self.assertTrue((root / "claimed" / "keep-claimed.json").exists())
            self.assertFalse((root / "completed" / "old-complete.json").exists())
            self.assertFalse((root / "failed" / "old-failed.json").exists())
            assert_no_atomic_temps(self, root)

    def test_cli_help_doctor_routes_pycompile_and_skill_quick_validate(self):
        production_cache = SCRIPT.parent / "__pycache__"
        cache_before = sorted(path.name for path in production_cache.glob("*") if path.is_file()) if production_cache.exists() else []
        with tempfile.TemporaryDirectory(prefix="unity-ops-pyc-") as raw_tmp:
            pyc = Path(raw_tmp) / "unity_ops.pyc"
            py_compile.compile(str(SCRIPT), cfile=str(pyc), doraise=True)
            self.assertTrue(pyc.exists())
        cache_after = sorted(path.name for path in production_cache.glob("*") if path.is_file()) if production_cache.exists() else []
        self.assertEqual(cache_before, cache_after, "py_compile must not change production cache")

        rc, payload, completed = run_cli("--help", expect_json=False)
        self.assertEqual(0, rc)
        self.assertIn("PlayMode is prohibited", completed.stdout)
        self.assertIsNone(payload)
        rc, doctor, _ = run_cli("doctor")
        self.assertEqual(0, rc)
        self.assertEqual("not contacted", doctor["network"])
        self.assertEqual(4, len(doctor["routes"]))
        for alias, expected in {
            "lilith": EXPECTED_ROUTES["lilith"],
            "unity4": EXPECTED_ROUTES["unity4"],
            "colo": EXPECTED_ROUTES["colo"],
            "arttest": EXPECTED_ROUTES["arttest"],
        }.items():
            rc, route, _ = run_cli("route", alias)
            self.assertEqual(0, rc)
            self.assertEqual(expected[1], route["endpoint"])

        skill = SKILL_DOC.read_text(encoding="utf-8")
        for token in ("scripts/unity_ops.py", "doctor", "route lilith", "tools-list", "broker", "PlayMode"):
            self.assertIn(token, skill)
        self.assertIn("references/broker-protocol.md", skill)



if __name__ == "__main__":
    unittest.main()
