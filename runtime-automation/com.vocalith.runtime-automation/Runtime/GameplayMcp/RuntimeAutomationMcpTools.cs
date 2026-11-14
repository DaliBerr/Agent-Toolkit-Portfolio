using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace Vocalith.RuntimeAutomation.GameplayMcp
{
    [McpServerToolType]
    public static class RuntimeAutomationMcpTools
    {
        public const string QaListToolName = "qa_list_mutations";
        public const string QaExecuteToolName = "qa_execute_mutation";

        [McpServerTool(Name = "get_capabilities", ReadOnly = true, Destructive = false)]
        [Description("Returns the versioned runtime automation protocol, providers, actions, visual captures, and mode gates.")]
        [Preserve]
        public static async Task<string> GetCapabilities(CancellationToken cancellationToken = default)
        {
            return await ExecuteReadAsync(host => host.GetCapabilities(), cancellationToken);
        }

        [McpServerTool(Name = "query_state", ReadOnly = true, Destructive = false)]
        [Description("Queries one or more structured state domains. Screenshot interpretation is not used as a state fallback.")]
        [Preserve]
        public static async Task<string> QueryState(
            [Description("Comma-separated provider domain ids. Empty selects every registered state domain.")] string domains = "",
            [Description("Optional comma-separated field mask passed to each provider.")] string fields = "",
            CancellationToken cancellationToken = default)
        {
            return await ExecuteReadAsync(host => host.QueryState(domains, fields), cancellationToken);
        }

        [McpServerTool(Name = "query_entities", ReadOnly = true, Destructive = false)]
        [Description("Queries semantic runtime entities using stable ids, kinds, tags, proximity, paging, and field masks.")]
        [Preserve]
        public static async Task<string> QueryEntities(
            [Description("Exact semantic entity id, or empty.")] string id = "",
            [Description("Semantic entity kind, or empty.")] string kind = "",
            [Description("Required semantic tag, or empty.")] string tag = "",
            [Description("Specific entity provider id, or empty.")] string provider = "",
            [Description("Optional proximity origin X. Use with near_y and radius.")] float? near_x = null,
            [Description("Optional proximity origin Y. Use with near_x and radius.")] float? near_y = null,
            [Description("Optional proximity radius.")] float? radius = null,
            [Description("Comma-separated fields: tags, transform, bounds, components, state. Empty selects all.")] string fields = "",
            [Description("Opaque paging cursor returned by the previous response.")] string cursor = "",
            [Description("Page size from 1 to 500.")] int limit = RuntimeAutomationProtocol.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteReadAsync(host => host.QueryEntities(
                new RuntimeAutomationEntityQuery
                {
                    Id = EmptyToNull(id),
                    Kind = EmptyToNull(kind),
                    Tag = EmptyToNull(tag),
                    Provider = EmptyToNull(provider),
                    NearX = near_x,
                    NearY = near_y,
                    Radius = radius,
                    Fields = ParseCsv(fields),
                },
                cursor,
                limit), cancellationToken);
        }

        [McpServerTool(Name = "list_actions", ReadOnly = true, Destructive = false)]
        [Description("Lists bounded atomic-input and semantic-input actions with parameters, preconditions, expected effects, and evidence level.")]
        [Preserve]
        public static async Task<string> ListActions(
            [Description("Optional action provider id.")] string provider = "",
            CancellationToken cancellationToken = default)
        {
            return await ExecuteReadAsync(host => host.ListActions(EmptyToNull(provider)), cancellationToken);
        }

        [McpServerTool(Name = "execute_action", ReadOnly = false, Destructive = false)]
        [Description("Executes one bounded action and reports accepted, dispatched, observed effect, revisions, events, blocking reason, and evidence level.")]
        [Preserve]
        public static async Task<string> ExecuteAction(
            [Description("Action id returned by list_actions.")] string action_id,
            [Description("Optional stable semantic target id.")] string target_id = "",
            [Description("Action arguments as a JSON object.")] string arguments_json = "{}",
            [Description("Expected state revision, or -1 to skip optimistic concurrency validation.")] long expected_revision = -1,
            [Description("Action timeout in milliseconds, clamped to 50-60000.")] int timeout_ms = 5000,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(async host => await host.ExecuteActionAsync(
                action_id,
                EmptyToNull(target_id),
                ParseObject(arguments_json, nameof(arguments_json)),
                expected_revision,
                timeout_ms,
                cancellationToken), cancellationToken);
        }

        [McpServerTool(Name = "get_events", ReadOnly = true, Destructive = false)]
        [Description("Returns ordered runtime events after a sequence number from the bounded session event journal.")]
        [Preserve]
        public static async Task<string> GetEvents(
            [Description("Return events with sequence greater than this value.")] long after_sequence = 0,
            [Description("Maximum events from 1 to 500.")] int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteReadAsync(host => host.GetEvents(Math.Max(0, after_sequence), limit), cancellationToken);
        }

        [McpServerTool(Name = "wait_for", ReadOnly = true, Destructive = false)]
        [Description("Waits on a structured state field using a bounded real-time poll; never reads screenshots to decide progress.")]
        [Preserve]
        public static async Task<string> WaitFor(
            [Description("Registered state domain id.")] string domain,
            [Description("Newtonsoft JSONPath inside the domain payload; empty selects the full payload.")] string path,
            [Description("Comparison: equals, not_equals, exists, not_exists, contains, greater, greater_or_equal, less, less_or_equal.")] string comparison = "equals",
            [Description("Expected value encoded as JSON, for example true, 3, or \"Main\".")] string expected_json = "null",
            [Description("Timeout in milliseconds, clamped to 50-60000.")] int timeout_ms = 5000,
            [Description("Polling interval in milliseconds, clamped to 16-1000.")] int poll_ms = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                RuntimeAutomationHost host = RequireHost();
                JToken expected = ParseToken(expected_json, nameof(expected_json));
                int timeout = Math.Clamp(timeout_ms, 50, 60000);
                int poll = Math.Clamp(poll_ms, 16, 1000);
                double deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + timeout / 1000d;
                JObject last = null;
                do
                {
                    await UniTask.SwitchToMainThread(cancellationToken);
                    last = host.EvaluateCondition(domain, path, comparison, expected);
                    if (!(last.Value<bool?>("ok") ?? false)) return Serialize(last);
                    if (last.SelectToken("payload.matched")?.Value<bool>() == true)
                    {
                        last["payload"]["timed_out"] = false;
                        return Serialize(last);
                    }

                    if (UnityEngine.Time.realtimeSinceStartupAsDouble >= deadline) break;
                    await UniTask.Delay(poll, ignoreTimeScale: true, cancellationToken: cancellationToken);
                }
                while (true);

                last ??= host.EvaluateCondition(domain, path, comparison, expected);
                last["payload"]["timed_out"] = true;
                return Serialize(last);
            }
            catch (Exception exception)
            {
                return SerializeFailure(exception);
            }
        }

        [McpServerTool(Name = "capture_visual", ReadOnly = true, Destructive = false)]
        [Description("Captures a provider-defined PNG visual artifact after optional semantic targeting and returns its structured manifest and content hash.")]
        [Preserve]
        public static async Task<string> CaptureVisual(
            [Description("Visual capture id returned by get_capabilities.")] string visual_id = "screen",
            [Description("Optional stable semantic target id used for framing.")] string target_id = "",
            [Description("Optional named camera bookmark.")] string bookmark = "",
            [Description("Optional semantic UI region.")] string region = "",
            [Description("Additional capture arguments as a JSON object.")] string arguments_json = "{}",
            [Description("Capture timeout in milliseconds, clamped to 100-60000.")] int timeout_ms = 10000,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(async host => await host.CaptureVisualAsync(
                new RuntimeAutomationVisualRequest
                {
                    VisualId = visual_id,
                    TargetId = EmptyToNull(target_id),
                    Bookmark = EmptyToNull(bookmark),
                    Region = EmptyToNull(region),
                    Format = "png",
                    Arguments = ParseObject(arguments_json, nameof(arguments_json)),
                },
                timeout_ms,
                cancellationToken), cancellationToken);
        }

        [McpServerTool(Name = "get_diagnostics", ReadOnly = true, Destructive = false)]
        [Description("Returns provider diagnostics for input, logs, service health, missing references, and runtime performance.")]
        [Preserve]
        public static async Task<string> GetDiagnostics(
            [Description("Optional comma-separated diagnostics field mask.")] string fields = "",
            CancellationToken cancellationToken = default)
        {
            return await ExecuteReadAsync(host => host.GetDiagnostics(fields), cancellationToken);
        }

        [McpServerTool(Name = QaListToolName, ReadOnly = true, Destructive = false)]
        [Description("QA-mode only. Lists explicitly registered low-level direct mutations. Hidden in Verification mode.")]
        [Preserve]
        public static async Task<string> QaListMutations(CancellationToken cancellationToken = default)
        {
            return await ExecuteReadAsync(host => host.ListQaMutations(), cancellationToken);
        }

        [McpServerTool(Name = QaExecuteToolName, ReadOnly = false, Destructive = true)]
        [Description("QA-mode only. Executes one allowlisted low-level direct mutation; arbitrary reflection, paths, and result commands are rejected.")]
        [Preserve]
        public static async Task<string> QaExecuteMutation(
            [Description("Mutation id returned by qa_list_mutations.")] string mutation_id,
            [Description("Optional stable semantic target id.")] string target_id = "",
            [Description("Mutation arguments as a JSON object.")] string arguments_json = "{}",
            [Description("Expected state revision, or -1.")] long expected_revision = -1,
            [Description("Mutation timeout in milliseconds, clamped to 50-60000.")] int timeout_ms = 5000,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteMutationAsync(async host => await host.ExecuteQaMutationAsync(
                mutation_id,
                EmptyToNull(target_id),
                ParseObject(arguments_json, nameof(arguments_json)),
                expected_revision,
                timeout_ms,
                cancellationToken), cancellationToken);
        }

        private static async Task<string> ExecuteReadAsync(
            Func<RuntimeAutomationHost, JObject> action,
            CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.SwitchToMainThread(cancellationToken);
                return Serialize(action(RequireHost()));
            }
            catch (Exception exception)
            {
                return SerializeFailure(exception);
            }
        }

        private static async Task<string> ExecuteMutationAsync(
            Func<RuntimeAutomationHost, Task<JObject>> action,
            CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.SwitchToMainThread(cancellationToken);
                return Serialize(await action(RequireHost()));
            }
            catch (Exception exception)
            {
                return SerializeFailure(exception);
            }
        }

        private static RuntimeAutomationHost RequireHost()
        {
            return RuntimeAutomationMcpBridge.Current ??
                throw new InvalidOperationException("Runtime automation host is not installed.");
        }

        private static string Serialize(JObject value)
        {
            return (value ?? new JObject()).ToString(Formatting.None);
        }

        private static string SerializeFailure(Exception exception)
        {
            return new JObject
            {
                ["ok"] = false,
                ["schema_version"] = RuntimeAutomationProtocol.SchemaVersion,
                ["payload"] = new JObject(),
                ["errors"] = new JArray(new JObject
                {
                    ["code"] = "tool_failed",
                    ["message"] = exception.Message,
                    ["error_type"] = exception.GetType().Name,
                }),
            }.ToString(Formatting.None);
        }

        private static JObject ParseObject(string json, string parameterName)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"{parameterName} must be a JSON object: {exception.Message}", parameterName);
            }
        }

        private static JToken ParseToken(string json, string parameterName)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json) ? JValue.CreateNull() : JToken.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"{parameterName} must contain valid JSON: {exception.Message}", parameterName);
            }
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string[] ParseCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
            string[] parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < parts.Length; index++) parts[index] = parts[index].Trim();
            return parts;
        }
    }
}
