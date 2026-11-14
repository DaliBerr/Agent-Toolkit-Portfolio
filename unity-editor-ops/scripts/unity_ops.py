#!/usr/bin/env python3
"""Bounded, stdlib-only Unity MCP client and file-backed request broker."""

import argparse
import contextlib
import errno
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid


VERSION = "0.2.0"
MAX_TIMEOUT_MS = 120_000
MAX_RESPONSE_BYTES = 5 * 1024 * 1024
PROJECTS = {
    "unity4": ("UnityProject4", "http://127.0.0.1:8080/mcp", "unityMCP_0"),
    "unityproject4": ("UnityProject4", "http://127.0.0.1:8080/mcp", "unityMCP_0"),
    "colo": ("Colo-3D", "http://127.0.0.1:8081/mcp", "unityMCP_1"),
    "colo-3d": ("Colo-3D", "http://127.0.0.1:8081/mcp", "unityMCP_1"),
    "lilith": ("lilith", "http://127.0.0.1:8082/mcp", "unityMCP_2"),
    "arttest": ("arttest", "http://127.0.0.1:8083/mcp", "unityMCP_3"),
}
STATES = ("pending", "claimed", "completed", "failed")
REQUEST_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")


class CommandError(RuntimeError):
    """A concise, user-actionable CLI error."""


def compact(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)


def emit(value):
    print(compact(value))


def parse_object(raw, label):
    value = parse_json(raw, label)
    if not isinstance(value, dict):
        raise CommandError(f"{label} must be a JSON object")
    return value


def parse_json(raw, label):
    try:
        return json.loads(raw)
    except json.JSONDecodeError as exc:
        raise CommandError(f"{label} must be valid JSON: {exc.msg}") from exc


def bounded_timeout(timeout_ms):
    if not 1 <= timeout_ms <= MAX_TIMEOUT_MS:
        raise CommandError(f"timeout_ms must be between 1 and {MAX_TIMEOUT_MS}")
    return timeout_ms / 1000.0


def resolve_project(name):
    key = name.strip().casefold()
    try:
        project, endpoint, namespace = PROJECTS[key]
    except KeyError as exc:
        raise CommandError(f"unknown project alias: {name}") from exc
    return {"project": project, "endpoint": endpoint, "expected_namespace": namespace}


def _contains_play_mode(value):
    if isinstance(value, dict):
        return any(_contains_play_mode(item) for item in value.values())
    if isinstance(value, list):
        return any(_contains_play_mode(item) for item in value)
    if not isinstance(value, str):
        return False
    normalized = re.sub(r"[^a-z0-9]", "", value.casefold())
    return normalized in {"play", "playmode", "enterplay", "enterplaymode", "startplaymode"} or "playmode" in normalized


def reject_play_mode(value):
    if _contains_play_mode(value):
        raise CommandError("PlayMode operations are prohibited by unity-editor-ops")


def validate_test_selection(project, arguments):
    """Fail closed on broad Lilith filters; fixture side effects still need review."""
    if resolve_project(project)["project"] != "lilith":
        return
    names = arguments.get("test_names")
    if not isinstance(names, list) or not names:
        raise CommandError("Lilith tests require non-empty exact test_names; review fixtures for room generation first")
    for name in names:
        if not isinstance(name, str) or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*", name):
            raise CommandError("Lilith test_names must be exact class/method names, not wildcard or expression filters")
        if "roomgeneration" in name.casefold() or "roomlayoutgenerator" in name.casefold():
            raise CommandError("Lilith room generation tests are prohibited")
        if not any(part.endswith("Tests") and len(part) > 5 for part in name.split(".")[-2:]):
            raise CommandError("Lilith selection must identify a concrete *Tests class or its method, not a namespace")
    if any(arguments.get(key) for key in ("assembly_names", "category_names", "categories", "filter", "test_filter", "group_names")):
        raise CommandError("Lilith tests cannot combine exact test_names with broad filters")
    if set(arguments) - {"mode", "test_names"}:
        raise CommandError("unsupported Lilith test options; only mode and exact test_names are allowed")


def validate_operation(project, kind, name, arguments, depth=0):
    """Apply the same test/PlayMode gate to wrappers, raw calls and batches."""
    if depth > 8:
        raise CommandError("batch nesting exceeds 8 levels")
    if kind not in {"tool-call", "resource-read", "tools-list"}:
        raise CommandError(f"unsupported operation kind: {kind}")
    if not isinstance(name, str) or not name.strip() or not isinstance(arguments, dict):
        raise CommandError("operation requires a non-empty name and object arguments")
    reject_play_mode({"name": name, "arguments": arguments})
    if kind == "tool-call" and name == "run_tests":
        validate_test_selection(project, arguments)
    if kind == "tool-call" and name == "batch_execute":
        commands = arguments.get("commands")
        if not isinstance(commands, list) or not commands:
            raise CommandError("batch_execute requires non-empty commands")
        for command in commands:
            if not isinstance(command, dict) or set(command) - {"tool", "params"}:
                raise CommandError("batch commands must contain only tool and params")
            validate_operation(project, "tool-call", command.get("tool"), command.get("params", {}), depth + 1)


def operation_is_readonly(kind, name, arguments):
    if kind in {"resource-read", "tools-list"}:
        return True
    if name == "batch_execute":
        return all(operation_is_readonly("tool-call", c["tool"], c.get("params", {})) for c in arguments["commands"])
    if name == "read_console":
        return arguments.get("action", "get") in {"get", "read"} and not arguments.get("clear")
    # Unknown tools and action-dependent tools require write preconditions.
    return name in {"find_gameobjects", "get_test_job", "get_sha"}


def _decode_sse(text):
    messages, data_lines = [], []

    def flush():
        if not data_lines:
            return
        data = "\n".join(data_lines)
        data_lines.clear()
        try:
            messages.append(json.loads(data))
        except json.JSONDecodeError as exc:
            raise CommandError(f"invalid JSON in SSE event: {exc.msg}") from exc

    for line in text.splitlines():
        if not line:
            flush()
        elif line.startswith("data:"):
            data_lines.append(line[5:].lstrip())
    flush()
    return messages


def _parse_reply(data, content_type):
    if not data:
        return None
    text = data.decode("utf-8")
    if "text/event-stream" in content_type.casefold() or text.lstrip().startswith("data:"):
        return _decode_sse(text)
    try:
        return json.loads(text)
    except json.JSONDecodeError as exc:
        raise CommandError(f"MCP response was not JSON: {exc.msg}") from exc


def _is_timeout_error(value):
    """Recognize direct socket timeouts and urllib's wrapped timeout reasons."""
    seen = set()
    while value is not None and id(value) not in seen:
        seen.add(id(value))
        if isinstance(value, (TimeoutError, socket.timeout)):
            return True
        if isinstance(value, OSError) and value.errno in {errno.ETIMEDOUT, 10060}:
            return True
        if isinstance(value, str):
            return "timed out" in value.casefold() or "timeout" in value.casefold()
        reason = getattr(value, "reason", None)
        if reason is not None and reason is not value:
            value = reason
            continue
        value = getattr(value, "__cause__", None) or getattr(value, "__context__", None)
    return False


def _matching_message(payload, request_id):
    candidates = payload if isinstance(payload, list) else [payload]
    for message in candidates:
        if isinstance(message, dict) and message.get("id") == request_id:
            return message
    raise CommandError("MCP response did not contain a result matching the request id")


class StreamableHttpClient:
    """One bounded Streamable HTTP session. A fresh CLI run owns one session."""

    def __init__(self, endpoint, timeout_ms):
        self.endpoint = endpoint
        self.timeout = bounded_timeout(timeout_ms)
        self.session_id = None

    def _post(self, payload, session_id=None):
        headers = {
            "Accept": "application/json, text/event-stream",
            "Content-Type": "application/json",
            "User-Agent": f"unity-editor-ops/{VERSION}",
        }
        if session_id:
            headers["Mcp-Session-Id"] = session_id
        request = urllib.request.Request(
            self.endpoint, data=compact(payload).encode("utf-8"), headers=headers, method="POST"
        )
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                body = response.read(MAX_RESPONSE_BYTES + 1)
                if len(body) > MAX_RESPONSE_BYTES:
                    raise CommandError("MCP response exceeded the 5 MiB safety limit")
                header_values = response.headers.get_all("mcp-session-id") or []
                return body, response.headers.get("Content-Type", ""), header_values[0] if header_values else None
        except urllib.error.HTTPError as exc:
            detail = exc.read(4096).decode("utf-8", "replace")
            raise CommandError(f"MCP HTTP {exc.code}: {detail}") from exc
        except (TimeoutError, socket.timeout) as exc:
            raise CommandError(f"could not reach MCP endpoint {self.endpoint}: timed out after {int(self.timeout * 1000)} ms") from exc
        except urllib.error.URLError as exc:
            if _is_timeout_error(exc.reason):
                raise CommandError(f"could not reach MCP endpoint {self.endpoint}: timed out after {int(self.timeout * 1000)} ms") from exc
            raise CommandError(f"could not reach MCP endpoint {self.endpoint}: {exc.reason}") from exc
        except OSError as exc:
            if _is_timeout_error(exc):
                raise CommandError(f"could not reach MCP endpoint {self.endpoint}: timed out after {int(self.timeout * 1000)} ms") from exc
            raise

    def initialize(self):
        if self.session_id:
            return
        request_id = f"initialize-{uuid.uuid4()}"
        payload = {
            "jsonrpc": "2.0",
            "id": request_id,
            "method": "initialize",
            "params": {
                "protocolVersion": "2025-03-26",
                "capabilities": {},
                "clientInfo": {"name": "unity-editor-ops", "version": VERSION},
            },
        }
        body, content_type, session_id = self._post(payload)
        message = _matching_message(_parse_reply(body, content_type), request_id)
        if "error" in message:
            raise CommandError(f"MCP initialize failed: {compact(message['error'])}")
        if not session_id:
            raise CommandError("MCP initialize response did not include mcp-session-id")
        self.session_id = session_id
        notification = {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}
        self._post(notification, self.session_id)

    def call(self, method, params):
        self.initialize()
        request_id = f"request-{uuid.uuid4()}"
        payload = {"jsonrpc": "2.0", "id": request_id, "method": method, "params": params}
        body, content_type, _ = self._post(payload, self.session_id)
        message = _matching_message(_parse_reply(body, content_type), request_id)
        if "error" in message:
            raise CommandError(f"MCP {method} failed: {compact(message['error'])}")
        if "result" not in message:
            raise CommandError(f"MCP {method} returned neither result nor error")
        return message["result"]


def default_state_root():
    base = os.environ.get("LOCALAPPDATA")
    if base:
        return Path(base) / "OpenAI" / "Codex" / "unity-mcp-broker"
    return Path(tempfile.gettempdir()) / "OpenAI" / "Codex" / "unity-mcp-broker"


def broker_root(args):
    return Path(args.state_root).expanduser() if getattr(args, "state_root", None) else default_state_root()


def ensure_layout(root):
    def create(candidate):
        for name in (*STATES, "locks"):
            (candidate / name).mkdir(parents=True, exist_ok=True)
        return candidate

    try:
        return create(root)
    except OSError as exc:
        fallback = Path(tempfile.gettempdir()) / "OpenAI" / "Codex" / "unity-mcp-broker"
        if fallback == root:
            raise CommandError(f"cannot create broker state root {root}: {exc}") from exc
        try:
            return create(fallback)
        except OSError as fallback_exc:
            raise CommandError(f"cannot create broker state root or temp fallback: {fallback_exc}") from fallback_exc


def _atomic_write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp_name = None
    try:
        with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=path.parent, prefix=".tmp-", suffix=".json", delete=False) as handle:
            temp_name = handle.name
            handle.write(compact(value))
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temp_name, path)
    finally:
        if temp_name and os.path.exists(temp_name):
            os.unlink(temp_name)


def _read_request(path):
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise CommandError(f"invalid broker request {path.name}: {exc}") from exc
    if not isinstance(value, dict):
        raise CommandError(f"invalid broker request {path.name}: expected object")
    return value


def _validate_request(value):
    required = {
        "schema_version",
        "request_id",
        "project",
        "route",
        "operation",
        "mode",
        "timeout_ms",
        "lease_id",
        "requester",
        "requester_role",
        "preconditions",
        "expected_readback",
    }
    missing = sorted(required - set(value))
    if missing:
        raise CommandError(f"broker request is missing: {', '.join(missing)}")
    if value["schema_version"] != 2 or not REQUEST_ID_RE.fullmatch(str(value["request_id"])):
        raise CommandError("broker request has an invalid schema_version or request_id")
    resolved = resolve_project(str(value["project"]))
    route = value["route"]
    if not isinstance(route, dict) or route.get("endpoint") != resolved["endpoint"] or route.get("expected_namespace") != resolved["expected_namespace"]:
        raise CommandError("broker request route does not match the configured project route")
    operation = value["operation"]
    if not isinstance(operation, dict) or not isinstance(operation.get("kind"), str) or not isinstance(operation.get("name"), str) or not isinstance(operation.get("arguments"), dict):
        raise CommandError("broker operation must contain kind, name, and object arguments")
    validate_operation(value["project"], operation["kind"], operation["name"], operation["arguments"])
    if value["mode"] not in {"read", "write"} or not isinstance(value["timeout_ms"], int):
        raise CommandError("broker mode must be read/write and timeout_ms must be an integer")
    bounded_timeout(value["timeout_ms"])
    if value["mode"] == "read" and not operation_is_readonly(operation["kind"], operation["name"], operation["arguments"]):
        raise CommandError("operation is not known read-only; use mode=write with lease and safety preconditions")
    if value["mode"] == "write" and not value["lease_id"]:
        raise CommandError("write broker requests require lease_id")
    if not isinstance(value["requester"], str) or not value["requester"].strip():
        raise CommandError("broker requester must be a non-empty string")
    if not isinstance(value["requester_role"], str) or not value["requester_role"].strip():
        raise CommandError("broker requester_role must be a non-empty string")
    if not isinstance(value["preconditions"], dict) or not isinstance(value["expected_readback"], dict):
        raise CommandError("broker preconditions and expected_readback must be objects")
    if value["mode"] == "write":
        preconditions = value["preconditions"]
        prefab_stage = preconditions.get("prefab_stage")
        if not isinstance(preconditions.get("snapshot"), str) or not preconditions["snapshot"].strip():
            raise CommandError("write broker requests require a snapshot precondition")
        if not isinstance(prefab_stage, dict) or prefab_stage.get("is_dirty") is not False:
            raise CommandError("write broker requests require prefab_stage.is_dirty=false")
        if preconditions.get("play_mode") is not False:
            raise CommandError("write broker requests require play_mode=false")
    reject_play_mode(operation)


@contextlib.contextmanager
def endpoint_lock(root, project, timeout_ms):
    timeout = bounded_timeout(timeout_ms)
    endpoint = resolve_project(project)["endpoint"]
    lock_path = root / "locks" / f"{hashlib.sha256(endpoint.encode()).hexdigest()}.lock"
    deadline = time.monotonic() + timeout
    descriptor = None
    while descriptor is None:
        try:
            descriptor = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            os.write(descriptor, compact({"pid": os.getpid(), "project": project}).encode("utf-8"))
        except FileExistsError:
            if time.monotonic() >= deadline:
                raise CommandError(f"broker endpoint lock timed out for {project}")
            time.sleep(min(0.05, max(0.0, deadline - time.monotonic())))
    try:
        yield
    finally:
        os.close(descriptor)
        try:
            lock_path.unlink()
        except FileNotFoundError:
            pass


def broker_submit(args):
    route = resolve_project(args.project)
    root = ensure_layout(broker_root(args))
    request_id = args.request_id or uuid.uuid4().hex
    request = {
        "schema_version": 2,
        "request_id": request_id,
        "project": route["project"],
        "route": {
            "endpoint": route["endpoint"],
            "expected_namespace": route["expected_namespace"],
        },
        "operation": {"kind": args.kind, "name": args.name, "arguments": parse_object(args.arguments, "arguments")},
        "mode": args.mode,
        "timeout_ms": args.timeout_ms,
        "lease_id": args.lease_id,
        "requester": args.requester or socket.gethostname(),
        "requester_role": args.requester_role,
        "preconditions": parse_object(args.preconditions, "preconditions"),
        "expected_readback": parse_object(args.expected_readback, "expected_readback"),
        "submitted_at": int(time.time() * 1000),
    }
    _validate_request(request)
    target = root / "pending" / f"{request_id}.json"
    with endpoint_lock(root, route["project"], 2000):
        if any((root / state / f"{request_id}.json").exists() for state in STATES):
            raise CommandError(f"broker request already exists: {request_id}")
        _atomic_write(target, request)
    return {"state": "pending", "request": request, "state_root": str(root)}


def broker_claim(args):
    route = resolve_project(args.project)
    root = ensure_layout(broker_root(args))
    with endpoint_lock(root, route["project"], args.lock_timeout_ms):
        for active_path in (root / "claimed").glob("*.json"):
            active = _read_request(active_path)
            if resolve_project(str(active.get("project", "")))["project"] == route["project"]:
                raise CommandError(f"endpoint already has an active request: {active['request_id']}; complete/fail it before claiming another")
        requested_id = getattr(args, "request_id", None)
        if requested_id and not REQUEST_ID_RE.fullmatch(requested_id):
            raise CommandError("invalid request_id")
        candidates = sorted((root / "pending").glob("*.json"), key=lambda path: path.stat().st_mtime)
        for source in candidates:
            if requested_id and source.stem != requested_id:
                continue
            request = _read_request(source)
            if resolve_project(str(request.get("project", "")))["project"] != route["project"]:
                continue
            _validate_request(request)
            target = root / "claimed" / source.name
            os.replace(source, target)
            request["claimed_at"] = int(time.time() * 1000)
            request["claimed_by"] = args.claimer or socket.gethostname()
            _atomic_write(target, request)
            return {"state": "claimed", "request": request, "state_root": str(root)}
    return {"state": "empty", "project": route["project"], "state_root": str(root)}


def broker_status(args):
    root = ensure_layout(broker_root(args))
    selected = []
    for state in STATES:
        for path in sorted((root / state).glob("*.json")):
            request = _read_request(path)
            if args.request_id and request.get("request_id") != args.request_id:
                continue
            if args.project and resolve_project(str(request.get("project", "")))["project"] != resolve_project(args.project)["project"]:
                continue
            selected.append({"state": state, "request": request})
    return {"state_root": str(root), "requests": selected}


def _claimed_request(root, request_id):
    if not REQUEST_ID_RE.fullmatch(request_id):
        raise CommandError("invalid request_id")
    source = root / "claimed" / f"{request_id}.json"
    if not source.is_file():
        raise CommandError(f"claimed broker request not found: {request_id}")
    request = _read_request(source)
    _validate_request(request)
    return source, request


def _finish_request(args, final_state):
    root = ensure_layout(broker_root(args))
    source, request = _claimed_request(root, args.request_id)
    with endpoint_lock(root, request["project"], 2000):
        return _finish_request_locked(args, final_state, root)


def _finish_request_locked(args, final_state, root):
    source, request = _claimed_request(root, args.request_id)
    target = root / final_state / source.name
    if target.exists():
        raise CommandError(f"terminal broker request already exists: {args.request_id}")
    now = int(time.time() * 1000)
    if final_state == "completed":
        request["result"] = parse_json(args.result, "result")
        request["completed_at"] = now
    else:
        if not args.error.strip():
            raise CommandError("error must not be empty")
        request["error"] = {"message": args.error, "details": parse_object(args.details, "details")}
        request["failed_at"] = now
    _atomic_write(source, request)
    os.replace(source, target)
    return {"state": final_state, "request": request, "state_root": str(root)}


def broker_complete(args):
    return _finish_request(args, "completed")


def broker_fail(args):
    return _finish_request(args, "failed")


def _find_request(root, request_id):
    if not REQUEST_ID_RE.fullmatch(request_id):
        raise CommandError("invalid request_id")
    for state in STATES:
        path = root / state / f"{request_id}.json"
        if path.is_file():
            return state, _read_request(path)
    return None, None


def broker_wait(args):
    root = ensure_layout(broker_root(args))
    timeout = bounded_timeout(args.timeout_ms)
    interval = bounded_timeout(args.interval_ms)
    deadline = time.monotonic() + timeout
    while True:
        state, request = _find_request(root, args.request_id)
        if state in {"completed", "failed"}:
            return {"state": state, "request": request, "state_root": str(root)}
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return {"state": "timeout", "request_id": args.request_id, "state_root": str(root)}
        time.sleep(min(interval, remaining))


def broker_cleanup(args):
    root = ensure_layout(broker_root(args))
    if args.older_than_ms < 1:
        raise CommandError("older_than_ms must be positive")
    cutoff = time.time() - (args.older_than_ms / 1000.0)
    deleted = []
    for state in ("completed", "failed"):
        directory = (root / state).resolve()
        for path in directory.glob("*.json"):
            if path.stat().st_mtime >= cutoff or path.resolve().parent != directory:
                continue
            path.unlink()
            deleted.append({"state": state, "request_id": path.stem})
    return {"state_root": str(root), "deleted": deleted}


def direct_call(args):
    route = resolve_project(args.project)
    client = StreamableHttpClient(route["endpoint"], args.timeout_ms)
    if args.command == "tools-list":
        result = client.call("tools/list", {})
    elif args.command == "resource-read":
        result = client.call("resources/read", {"uri": args.uri})
    elif args.command == "tool-call":
        arguments = parse_object(args.arguments, "arguments")
        validate_operation(args.project, "tool-call", args.name, arguments)
        result = client.call("tools/call", {"name": args.name, "arguments": arguments})
    elif args.command == "status":
        instances = client.call("resources/read", {"uri": "mcpforunity://instances"})
        editor_state = client.call("resources/read", {"uri": "mcpforunity://editor/state"})
        result = {"instances": instances, "editor_state": editor_state}
    elif args.command == "console":
        arguments = parse_object(args.arguments, "arguments")
        result = client.call("tools/call", {"name": "read_console", "arguments": arguments})
    elif args.command == "refresh":
        arguments = parse_object(args.arguments, "arguments")
        reject_play_mode(arguments)
        result = client.call("tools/call", {"name": "refresh_unity", "arguments": arguments})
    else:
        raise CommandError(f"unsupported direct command: {args.command}")
    return {"route": route, "result": result}


def _test_job_id(result):
    if not isinstance(result, dict):
        return None
    for key in ("job_id", "jobId", "id"):
        value = result.get(key)
        if isinstance(value, (str, int)):
            return value
    return None


def _terminal_test_result(result):
    if not isinstance(result, dict):
        return False
    status = str(result.get("status", result.get("state", ""))).casefold()
    return status in {"completed", "failed", "cancelled", "canceled", "error", "passed", "succeeded"}


def run_tests(args):
    route = resolve_project(args.project)
    arguments = parse_object(args.arguments, "arguments")
    reject_play_mode(arguments)
    arguments["mode"] = args.mode
    validate_operation(args.project, "tool-call", "run_tests", arguments)
    client = StreamableHttpClient(route["endpoint"], args.timeout_ms)
    run_result = client.call("tools/call", {"name": "run_tests", "arguments": arguments})
    output = {"route": route, "run": run_result}
    if not args.poll:
        return output
    job_id = _test_job_id(run_result)
    if job_id is None:
        output["poll"] = {"state": "not_started", "reason": "run_tests did not return a job id"}
        return output
    deadline = time.monotonic() + bounded_timeout(args.poll_timeout_ms)
    interval = bounded_timeout(args.poll_interval_ms)
    while True:
        poll_result = client.call("tools/call", {"name": "get_test_job", "arguments": {"job_id": job_id}})
        if _terminal_test_result(poll_result):
            output["poll"] = poll_result
            return output
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            output["poll"] = {"state": "timeout", "job_id": job_id}
            return output
        time.sleep(min(interval, remaining))


def doctor(_args):
    return {
        "version": VERSION,
        "python": sys.version.split()[0],
        "broker_state_root_default": str(default_state_root()),
        "broker_state_root_exists": default_state_root().exists(),
        "routes": [resolve_project(name) for name in ("unity4", "colo", "lilith", "arttest")],
        "network": "not contacted",
    }


def route(args):
    return resolve_project(args.project)


def add_direct_parser(commands, name):
    parser = commands.add_parser(name)
    parser.add_argument("project")
    parser.add_argument("--timeout-ms", type=int, default=10_000)
    parser.set_defaults(handler=direct_call)
    return parser


def build_parser():
    parser = argparse.ArgumentParser(description="Bounded Unity MCP operations; PlayMode is prohibited.")
    parser.add_argument("--version", action="version", version=VERSION)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("doctor", help="validate local configuration without contacting Unity").set_defaults(handler=doctor)
    route_parser = commands.add_parser("route", help="resolve a project alias")
    route_parser.add_argument("project")
    route_parser.set_defaults(handler=route)
    add_direct_parser(commands, "tools-list")
    resource = add_direct_parser(commands, "resource-read")
    resource.add_argument("uri")
    tool = add_direct_parser(commands, "tool-call")
    tool.add_argument("name")
    tool.add_argument("--arguments", default="{}")
    add_direct_parser(commands, "status")
    console = add_direct_parser(commands, "console")
    console.add_argument("--arguments", default="{}")
    refresh = add_direct_parser(commands, "refresh")
    refresh.add_argument("--arguments", default="{}")
    tests = add_direct_parser(commands, "tests")
    tests.add_argument("--mode", choices=("EditMode",), default="EditMode")
    tests.add_argument("--arguments", default="{}")
    tests.add_argument("--poll", action="store_true")
    tests.add_argument("--poll-timeout-ms", type=int, default=30_000)
    tests.add_argument("--poll-interval-ms", type=int, default=500)
    tests.set_defaults(handler=run_tests)

    broker = commands.add_parser("broker", help="file-backed bounded broker")
    broker_commands = broker.add_subparsers(dest="broker_command", required=True)
    submit = broker_commands.add_parser("submit")
    submit.add_argument("project")
    submit.add_argument("--kind", required=True)
    submit.add_argument("--name", required=True)
    submit.add_argument("--arguments", default="{}")
    submit.add_argument("--mode", choices=("read", "write"), default="read")
    submit.add_argument("--timeout-ms", type=int, default=10_000)
    submit.add_argument("--lease-id")
    submit.add_argument("--requester")
    submit.add_argument("--requester-role", required=True)
    submit.add_argument("--preconditions", default="{}")
    submit.add_argument("--expected-readback", default="{}")
    submit.add_argument("--request-id")
    submit.add_argument("--state-root")
    submit.set_defaults(handler=broker_submit)
    claim = broker_commands.add_parser("claim")
    claim.add_argument("project")
    claim.add_argument("--request-id", help="claim the exact queued request named by the caller")
    claim.add_argument("--claimer")
    claim.add_argument("--lock-timeout-ms", type=int, default=2_000)
    claim.add_argument("--state-root")
    claim.set_defaults(handler=broker_claim)
    complete = broker_commands.add_parser("complete")
    complete.add_argument("request_id")
    complete.add_argument("--result", default="{}")
    complete.add_argument("--state-root")
    complete.set_defaults(handler=broker_complete)
    fail = broker_commands.add_parser("fail")
    fail.add_argument("request_id")
    fail.add_argument("--error", required=True)
    fail.add_argument("--details", default="{}")
    fail.add_argument("--state-root")
    fail.set_defaults(handler=broker_fail)
    status = broker_commands.add_parser("status")
    status.add_argument("--request-id")
    status.add_argument("--project")
    status.add_argument("--state-root")
    status.set_defaults(handler=broker_status)
    wait = broker_commands.add_parser("wait")
    wait.add_argument("request_id")
    wait.add_argument("--timeout-ms", type=int, default=30_000)
    wait.add_argument("--interval-ms", type=int, default=250)
    wait.add_argument("--state-root")
    wait.set_defaults(handler=broker_wait)
    cleanup = broker_commands.add_parser("cleanup")
    cleanup.add_argument("--older-than-ms", type=int, default=86_400_000)
    cleanup.add_argument("--state-root")
    cleanup.set_defaults(handler=broker_cleanup)
    return parser


def main(argv=None):
    try:
        args = build_parser().parse_args(argv)
        emit(args.handler(args))
        return 0
    except CommandError as exc:
        emit({"error": str(exc)})
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
