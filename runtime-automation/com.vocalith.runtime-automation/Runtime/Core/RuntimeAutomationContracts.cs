using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Vocalith.RuntimeAutomation
{
    public static class RuntimeAutomationProtocol
    {
        public const string SchemaVersion = "1.0";
        public const int DefaultPageSize = 100;
        public const int MaximumPageSize = 500;
        public const int MaximumEventCount = 500;
    }

    public enum RuntimeAutomationMode
    {
        Verification,
        Qa,
    }

    public enum RuntimeAutomationEvidenceLevel
    {
        Observation,
        AtomicInput,
        SemanticInput,
        DirectQa,
    }

    public sealed class RuntimeAutomationSessionInfo
    {
        public string SessionId { get; set; }
        public string BuildId { get; set; }
        public string BuildMode { get; set; }
        public string GitRevision { get; set; }
        public bool DirtyBuild { get; set; }
        public RuntimeAutomationMode Mode { get; set; }
        public string ArtifactRoot { get; set; }
    }

    public sealed class RuntimeAutomationProviderRegistry
    {
        private readonly List<IRuntimeAutomationStateProvider> stateProviders = new();
        private readonly List<IRuntimeAutomationEntityProvider> entityProviders = new();
        private readonly List<IRuntimeAutomationActionProvider> actionProviders = new();
        private readonly List<IRuntimeAutomationEventSource> eventSources = new();
        private readonly List<IRuntimeAutomationVisualProvider> visualProviders = new();
        private readonly List<IRuntimeAutomationQaMutationProvider> qaMutationProviders = new();
        private readonly List<IRuntimeAutomationDiagnosticsProvider> diagnosticsProviders = new();

        public IReadOnlyList<IRuntimeAutomationStateProvider> StateProviders => stateProviders;
        public IReadOnlyList<IRuntimeAutomationEntityProvider> EntityProviders => entityProviders;
        public IReadOnlyList<IRuntimeAutomationActionProvider> ActionProviders => actionProviders;
        public IReadOnlyList<IRuntimeAutomationEventSource> EventSources => eventSources;
        public IReadOnlyList<IRuntimeAutomationVisualProvider> VisualProviders => visualProviders;
        public IReadOnlyList<IRuntimeAutomationQaMutationProvider> QaMutationProviders => qaMutationProviders;
        public IReadOnlyList<IRuntimeAutomationDiagnosticsProvider> DiagnosticsProviders => diagnosticsProviders;

        public RuntimeAutomationProviderRegistry Register(IRuntimeAutomationStateProvider provider)
        {
            AddUnique(stateProviders, provider, nameof(provider));
            return this;
        }

        public RuntimeAutomationProviderRegistry Register(IRuntimeAutomationEntityProvider provider)
        {
            AddUnique(entityProviders, provider, nameof(provider));
            return this;
        }

        public RuntimeAutomationProviderRegistry Register(IRuntimeAutomationActionProvider provider)
        {
            AddUnique(actionProviders, provider, nameof(provider));
            return this;
        }

        public RuntimeAutomationProviderRegistry Register(IRuntimeAutomationEventSource source)
        {
            AddUnique(eventSources, source, nameof(source));
            return this;
        }

        public RuntimeAutomationProviderRegistry Register(IRuntimeAutomationVisualProvider provider)
        {
            AddUnique(visualProviders, provider, nameof(provider));
            return this;
        }

        public RuntimeAutomationProviderRegistry Register(IRuntimeAutomationQaMutationProvider provider)
        {
            AddUnique(qaMutationProviders, provider, nameof(provider));
            return this;
        }

        public RuntimeAutomationProviderRegistry Register(IRuntimeAutomationDiagnosticsProvider provider)
        {
            AddUnique(diagnosticsProviders, provider, nameof(provider));
            return this;
        }

        private static void AddUnique<T>(List<T> providers, T provider, string argumentName)
            where T : IRuntimeAutomationProvider
        {
            if (provider == null)
            {
                throw new ArgumentNullException(argumentName);
            }

            if (string.IsNullOrWhiteSpace(provider.Id))
            {
                throw new ArgumentException("Provider id must not be empty.", argumentName);
            }

            foreach (T existing in providers)
            {
                if (string.Equals(existing.Id, provider.Id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Provider '{provider.Id}' is already registered for {typeof(T).Name}.");
                }
            }

            providers.Add(provider);
        }
    }

    public interface IRuntimeAutomationProvider
    {
        string Id { get; }
        string Version { get; }
    }

    public interface IRuntimeAutomationStateProvider : IRuntimeAutomationProvider
    {
        JObject CaptureState(RuntimeAutomationQueryContext context);
    }

    public interface IRuntimeAutomationEntityProvider : IRuntimeAutomationProvider
    {
        IEnumerable<RuntimeAutomationEntitySnapshot> QueryEntities(RuntimeAutomationEntityQuery query);
    }

    public interface IRuntimeAutomationActionProvider : IRuntimeAutomationProvider
    {
        IEnumerable<RuntimeAutomationActionDescriptor> ListActions();
        Task<RuntimeAutomationActionEffect> ExecuteActionAsync(RuntimeAutomationActionContext context);
    }

    public interface IRuntimeAutomationEventSource : IRuntimeAutomationProvider
    {
        event Action<RuntimeAutomationEventDraft> EventPublished;
    }

    public interface IRuntimeAutomationVisualProvider : IRuntimeAutomationProvider
    {
        IEnumerable<RuntimeAutomationVisualDescriptor> ListVisuals();
        Task<RuntimeAutomationVisualCapture> CaptureVisualAsync(RuntimeAutomationVisualRequest request, CancellationToken cancellationToken);
    }

    public interface IRuntimeAutomationQaMutationProvider : IRuntimeAutomationProvider
    {
        IEnumerable<RuntimeAutomationActionDescriptor> ListMutations();
        Task<RuntimeAutomationActionEffect> ExecuteMutationAsync(RuntimeAutomationActionContext context);
    }

    public interface IRuntimeAutomationDiagnosticsProvider : IRuntimeAutomationProvider
    {
        JObject CaptureDiagnostics(RuntimeAutomationQueryContext context);
    }

    public sealed class RuntimeAutomationQueryContext
    {
        public RuntimeAutomationQueryContext(IReadOnlyCollection<string> fields)
        {
            Fields = fields ?? Array.Empty<string>();
        }

        public IReadOnlyCollection<string> Fields { get; }
        public bool Includes(string field)
        {
            if (Fields.Count == 0)
            {
                return true;
            }

            foreach (string candidate in Fields)
            {
                if (string.Equals(candidate, field, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class RuntimeAutomationEntityQuery
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Tag { get; set; }
        public string Provider { get; set; }
        public float? NearX { get; set; }
        public float? NearY { get; set; }
        public float? Radius { get; set; }
        public IReadOnlyCollection<string> Fields { get; set; } = Array.Empty<string>();
    }

    public sealed class RuntimeAutomationEntitySnapshot
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public string Provider { get; set; }
        public bool SessionScoped { get; set; }
        public JArray Tags { get; set; } = new();
        public JObject Transform { get; set; } = new();
        public JObject Bounds { get; set; } = new();
        public JObject Components { get; set; } = new();
        public JObject State { get; set; } = new();

        public JObject ToJson(IReadOnlyCollection<string> fields)
        {
            bool all = fields == null || fields.Count == 0;
            var json = new JObject
            {
                ["id"] = Id,
                ["kind"] = Kind,
                ["name"] = Name,
                ["provider"] = Provider,
                ["session_scoped"] = SessionScoped,
            };
            if (!string.IsNullOrWhiteSpace(ParentId)) json["parent_id"] = ParentId;
            if (all || Contains(fields, "tags")) json["tags"] = Tags?.DeepClone() ?? new JArray();
            if (all || Contains(fields, "transform")) json["transform"] = Transform?.DeepClone() ?? new JObject();
            if (all || Contains(fields, "bounds")) json["bounds"] = Bounds?.DeepClone() ?? new JObject();
            if (all || Contains(fields, "components")) json["components"] = Components?.DeepClone() ?? new JObject();
            if (all || Contains(fields, "state")) json["state"] = State?.DeepClone() ?? new JObject();
            return json;
        }

        private static bool Contains(IReadOnlyCollection<string> fields, string value)
        {
            foreach (string field in fields)
            {
                if (string.Equals(field, value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    public sealed class RuntimeAutomationActionDescriptor
    {
        public string Id { get; set; }
        public string Provider { get; set; }
        public string Description { get; set; }
        public RuntimeAutomationEvidenceLevel EvidenceLevel { get; set; }
        public JObject Parameters { get; set; } = new();
        public JArray Preconditions { get; set; } = new();
        public JArray ExpectedEffects { get; set; } = new();

        public JObject ToJson()
        {
            return new JObject
            {
                ["id"] = Id,
                ["provider"] = Provider,
                ["description"] = Description,
                ["evidence_level"] = EvidenceLevel.ToString().ToLowerInvariant(),
                ["parameters"] = Parameters?.DeepClone() ?? new JObject(),
                ["preconditions"] = Preconditions?.DeepClone() ?? new JArray(),
                ["expected_effects"] = ExpectedEffects?.DeepClone() ?? new JArray(),
            };
        }
    }

    public sealed class RuntimeAutomationActionContext
    {
        internal RuntimeAutomationActionContext(
            RuntimeAutomationHost host,
            string actionId,
            string targetId,
            JObject arguments,
            CancellationToken cancellationToken)
        {
            Host = host;
            ActionId = actionId;
            TargetId = targetId;
            Arguments = arguments ?? new JObject();
            CancellationToken = cancellationToken;
        }

        public RuntimeAutomationHost Host { get; }
        public string ActionId { get; }
        public string TargetId { get; }
        public JObject Arguments { get; }
        public CancellationToken CancellationToken { get; }
    }

    public sealed class RuntimeAutomationActionEffect
    {
        public bool Dispatched { get; set; }
        public bool ObservedEffect { get; set; }
        public string BlockedReason { get; set; }
        public string Evidence { get; set; }
        public JObject Result { get; set; } = new();

        public static RuntimeAutomationActionEffect Blocked(string reason)
        {
            return new RuntimeAutomationActionEffect
            {
                Dispatched = false,
                ObservedEffect = false,
                BlockedReason = reason,
                Evidence = "precondition",
            };
        }
    }

    public sealed class RuntimeAutomationEventDraft
    {
        public string Type { get; set; }
        public string Provider { get; set; }
        public string EntityId { get; set; }
        public bool ChangesState { get; set; }
        public JObject Data { get; set; } = new();
    }

    public sealed class RuntimeAutomationEventRecord
    {
        public long Sequence { get; set; }
        public long Revision { get; set; }
        public int Frame { get; set; }
        public double RealtimeSeconds { get; set; }
        public string Type { get; set; }
        public string Provider { get; set; }
        public string EntityId { get; set; }
        public JObject Data { get; set; }

        public JObject ToJson()
        {
            var json = new JObject
            {
                ["sequence"] = Sequence,
                ["revision"] = Revision,
                ["frame"] = Frame,
                ["realtime_seconds"] = RealtimeSeconds,
                ["type"] = Type,
                ["provider"] = Provider,
                ["data"] = Data?.DeepClone() ?? new JObject(),
            };
            if (!string.IsNullOrWhiteSpace(EntityId)) json["entity_id"] = EntityId;
            return json;
        }
    }

    public sealed class RuntimeAutomationVisualDescriptor
    {
        public string Id { get; set; }
        public string Provider { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; } = new();

        public JObject ToJson()
        {
            return new JObject
            {
                ["id"] = Id,
                ["provider"] = Provider,
                ["description"] = Description,
                ["parameters"] = Parameters?.DeepClone() ?? new JObject(),
            };
        }
    }

    public sealed class RuntimeAutomationVisualRequest
    {
        public string VisualId { get; set; }
        public string TargetId { get; set; }
        public string Bookmark { get; set; }
        public string Region { get; set; }
        public string Format { get; set; } = "png";
        public JObject Arguments { get; set; } = new();
    }

    public sealed class RuntimeAutomationVisualCapture
    {
        public bool Captured { get; set; }
        public string BlockedReason { get; set; }
        public string ArtifactPath { get; set; }
        public string ContentHash { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public JObject Metadata { get; set; } = new();

        public JObject ToJson()
        {
            return new JObject
            {
                ["captured"] = Captured,
                ["blocked_reason"] = BlockedReason,
                ["artifact_path"] = ArtifactPath,
                ["content_hash"] = ContentHash,
                ["width"] = Width,
                ["height"] = Height,
                ["metadata"] = Metadata?.DeepClone() ?? new JObject(),
            };
        }
    }
}
