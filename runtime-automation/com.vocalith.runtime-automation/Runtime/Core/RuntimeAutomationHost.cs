using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Vocalith.RuntimeAutomation
{
    public sealed class RuntimeAutomationHost : IDisposable
    {
        private const int EventCapacity = 2000;

        private readonly RuntimeAutomationProviderRegistry registry;
        private readonly RuntimeAutomationSessionInfo session;
        private readonly SemaphoreSlim actionGate = new(1, 1);
        private readonly object eventGate = new();
        private readonly List<RuntimeAutomationEventRecord> events = new(EventCapacity);
        private bool disposed;
        private long revision;
        private long eventSequence;
        private long sceneEpoch;

        public RuntimeAutomationHost(
            RuntimeAutomationSessionInfo sessionInfo,
            RuntimeAutomationProviderRegistry providerRegistry)
        {
            session = sessionInfo ?? throw new ArgumentNullException(nameof(sessionInfo));
            registry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            if (string.IsNullOrWhiteSpace(session.SessionId))
            {
                throw new ArgumentException("Session id must not be empty.", nameof(sessionInfo));
            }

            foreach (IRuntimeAutomationEventSource source in registry.EventSources)
            {
                source.EventPublished += OnProviderEvent;
            }
        }

        public RuntimeAutomationSessionInfo Session => session;
        public long Revision => Interlocked.Read(ref revision);
        public long EventSequence => Interlocked.Read(ref eventSequence);
        public long SceneEpoch => Interlocked.Read(ref sceneEpoch);

        public void BeginSceneEpoch(string sceneName)
        {
            ThrowIfDisposed();
            long epoch = Interlocked.Increment(ref sceneEpoch);
            PublishEvent(new RuntimeAutomationEventDraft
            {
                Type = "scene_epoch_started",
                Provider = "runtime",
                ChangesState = true,
                Data = new JObject
                {
                    ["scene"] = sceneName ?? string.Empty,
                    ["scene_epoch"] = epoch,
                },
            });
        }

        public void MarkStateChanged(string provider, string reason, JObject data = null)
        {
            PublishEvent(new RuntimeAutomationEventDraft
            {
                Type = string.IsNullOrWhiteSpace(reason) ? "state_changed" : reason,
                Provider = string.IsNullOrWhiteSpace(provider) ? "runtime" : provider,
                ChangesState = true,
                Data = data ?? new JObject(),
            });
        }

        public void PublishEvent(RuntimeAutomationEventDraft draft)
        {
            ThrowIfDisposed();
            if (draft == null || string.IsNullOrWhiteSpace(draft.Type))
            {
                throw new ArgumentException("Runtime automation events require a type.", nameof(draft));
            }

            long nextRevision = draft.ChangesState
                ? Interlocked.Increment(ref revision)
                : Revision;
            var record = new RuntimeAutomationEventRecord
            {
                Sequence = Interlocked.Increment(ref eventSequence),
                Revision = nextRevision,
                Frame = Time.frameCount,
                RealtimeSeconds = Time.realtimeSinceStartupAsDouble,
                Type = draft.Type,
                Provider = string.IsNullOrWhiteSpace(draft.Provider) ? "unknown" : draft.Provider,
                EntityId = draft.EntityId,
                Data = draft.Data?.DeepClone() as JObject ?? new JObject(),
            };

            lock (eventGate)
            {
                if (events.Count == EventCapacity)
                {
                    events.RemoveAt(0);
                }

                events.Add(record);
            }
        }

        public JObject GetCapabilities()
        {
            ThrowIfDisposed();
            var state = new JArray(registry.StateProviders.Select(provider => ProviderJson(provider, "state")));
            var entities = new JArray(registry.EntityProviders.Select(provider => ProviderJson(provider, "entity")));
            var actions = new JArray(ListActionDescriptors(registry.ActionProviders).Select(descriptor => descriptor.ToJson()));
            var visuals = new JArray(registry.VisualProviders
                .SelectMany(provider => provider.ListVisuals() ?? Array.Empty<RuntimeAutomationVisualDescriptor>())
                .Select(descriptor => descriptor.ToJson()));
            var diagnostics = new JArray(registry.DiagnosticsProviders.Select(provider => ProviderJson(provider, "diagnostics")));

            var payload = new JObject
            {
                ["protocol"] = RuntimeAutomationProtocol.SchemaVersion,
                ["mode"] = ModeName(session.Mode),
                ["state_providers"] = state,
                ["entity_providers"] = entities,
                ["actions"] = actions,
                ["visuals"] = visuals,
                ["diagnostics_providers"] = diagnostics,
                ["supports_field_masks"] = true,
                ["supports_paging"] = true,
                ["supports_expected_revision"] = true,
                ["qa_available"] = session.Mode == RuntimeAutomationMode.Qa,
            };
            if (session.Mode == RuntimeAutomationMode.Qa)
            {
                payload["qa_mutations"] = new JArray(
                    ListMutationDescriptors().Select(descriptor => descriptor.ToJson()));
            }

            return SuccessEnvelope(payload);
        }

        public JObject QueryState(string domains, string fields)
        {
            ThrowIfDisposed();
            HashSet<string> selectedDomains = ParseCsvSet(domains);
            IReadOnlyCollection<string> selectedFields = ParseCsv(fields);
            var data = new JObject();
            var errors = new JArray();
            foreach (IRuntimeAutomationStateProvider provider in registry.StateProviders)
            {
                if (selectedDomains.Count > 0 && !selectedDomains.Contains(provider.Id))
                {
                    continue;
                }

                try
                {
                    data[provider.Id] = provider.CaptureState(new RuntimeAutomationQueryContext(selectedFields)) ?? new JObject();
                }
                catch (Exception exception)
                {
                    errors.Add(ErrorJson("provider_failed", exception.Message, provider.Id));
                }
            }

            if (selectedDomains.Count > 0)
            {
                foreach (string requested in selectedDomains)
                {
                    if (data[requested] == null && !errors.OfType<JObject>().Any(error =>
                            string.Equals(error.Value<string>("provider"), requested, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add(ErrorJson("unknown_domain", $"State domain '{requested}' is not registered.", requested));
                    }
                }
            }

            return SuccessEnvelope(new JObject
            {
                ["domains"] = data,
                ["partial"] = errors.Count > 0,
                ["errors"] = errors,
            });
        }

        public JObject QueryEntities(RuntimeAutomationEntityQuery query, string cursor, int limit)
        {
            ThrowIfDisposed();
            query ??= new RuntimeAutomationEntityQuery();
            int pageSize = Math.Clamp(limit, 1, RuntimeAutomationProtocol.MaximumPageSize);
            int offset = ParseCursor(cursor);
            var all = new List<RuntimeAutomationEntitySnapshot>();
            var errors = new JArray();
            foreach (IRuntimeAutomationEntityProvider provider in registry.EntityProviders)
            {
                if (!string.IsNullOrWhiteSpace(query.Provider) &&
                    !string.Equals(query.Provider, provider.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    IEnumerable<RuntimeAutomationEntitySnapshot> snapshots = provider.QueryEntities(query);
                    if (snapshots == null) continue;
                    foreach (RuntimeAutomationEntitySnapshot snapshot in snapshots)
                    {
                        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Id)) continue;
                        snapshot.Provider ??= provider.Id;
                        all.Add(snapshot);
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(ErrorJson("provider_failed", exception.Message, provider.Id));
                }
            }

            all.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
            offset = Math.Min(offset, all.Count);
            List<RuntimeAutomationEntitySnapshot> page = all.Skip(offset).Take(pageSize).ToList();
            int nextOffset = offset + page.Count;
            return SuccessEnvelope(new JObject
            {
                ["entities"] = new JArray(page.Select(entity => entity.ToJson(query.Fields))),
                ["page"] = new JObject
                {
                    ["offset"] = offset,
                    ["limit"] = pageSize,
                    ["returned"] = page.Count,
                    ["total"] = all.Count,
                    ["next_cursor"] = nextOffset < all.Count
                        ? nextOffset.ToString(CultureInfo.InvariantCulture)
                        : null,
                },
                ["partial"] = errors.Count > 0,
                ["errors"] = errors,
            });
        }

        public JObject ListActions(string providerFilter = null)
        {
            ThrowIfDisposed();
            IEnumerable<IRuntimeAutomationActionProvider> providers = registry.ActionProviders;
            if (!string.IsNullOrWhiteSpace(providerFilter))
            {
                providers = providers.Where(provider =>
                    string.Equals(provider.Id, providerFilter, StringComparison.OrdinalIgnoreCase));
            }

            var actions = new JArray(ListActionDescriptors(providers).Select(descriptor => descriptor.ToJson()));
            return SuccessEnvelope(new JObject { ["actions"] = actions });
        }

        public async Task<JObject> ExecuteActionAsync(
            string actionId,
            string targetId,
            JObject arguments,
            long expectedRevision,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return FailureEnvelope("invalid_action", "Action id must not be empty.");
            }

            (IRuntimeAutomationActionProvider provider, RuntimeAutomationActionDescriptor descriptor) =
                FindAction(actionId);
            if (provider == null || descriptor == null)
            {
                return FailureEnvelope("unknown_action", $"Action '{actionId}' is not registered.");
            }

            return await ExecuteWithProviderAsync(
                provider.Id,
                descriptor,
                context => provider.ExecuteActionAsync(context),
                actionId,
                targetId,
                arguments,
                expectedRevision,
                timeoutMilliseconds,
                cancellationToken);
        }

        public JObject GetEvents(long afterSequence, int limit)
        {
            ThrowIfDisposed();
            int count = Math.Clamp(limit, 1, RuntimeAutomationProtocol.MaximumEventCount);
            List<RuntimeAutomationEventRecord> selected;
            long oldest;
            lock (eventGate)
            {
                oldest = events.Count > 0 ? events[0].Sequence : EventSequence + 1;
                selected = events
                    .Where(record => record.Sequence > afterSequence)
                    .Take(count)
                    .ToList();
            }

            long next = selected.Count > 0 ? selected[^1].Sequence : Math.Max(afterSequence, EventSequence);
            return SuccessEnvelope(new JObject
            {
                ["events"] = new JArray(selected.Select(record => record.ToJson())),
                ["oldest_available_sequence"] = oldest,
                ["next_sequence"] = next,
                ["truncated_before_request"] = afterSequence > 0 && afterSequence < oldest - 1,
                ["has_more"] = selected.Count == count && next < EventSequence,
            });
        }

        public JObject EvaluateCondition(
            string domain,
            string path,
            string comparison,
            JToken expected)
        {
            ThrowIfDisposed();
            IRuntimeAutomationStateProvider provider = registry.StateProviders.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, domain, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                return FailureEnvelope("unknown_domain", $"State domain '{domain}' is not registered.");
            }

            JObject state;
            try
            {
                state = provider.CaptureState(new RuntimeAutomationQueryContext(Array.Empty<string>())) ?? new JObject();
            }
            catch (Exception exception)
            {
                return FailureEnvelope("provider_failed", exception.Message, provider.Id);
            }

            JToken actual = string.IsNullOrWhiteSpace(path) ? state : state.SelectToken(path, errorWhenNoMatch: false);
            bool matched = Compare(actual, expected, comparison);
            return SuccessEnvelope(new JObject
            {
                ["matched"] = matched,
                ["domain"] = domain,
                ["path"] = path,
                ["comparison"] = NormalizeComparison(comparison),
                ["expected"] = expected?.DeepClone(),
                ["actual"] = actual?.DeepClone(),
            });
        }

        public async Task<JObject> CaptureVisualAsync(
            RuntimeAutomationVisualRequest request,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            request ??= new RuntimeAutomationVisualRequest();
            (IRuntimeAutomationVisualProvider provider, RuntimeAutomationVisualDescriptor descriptor) =
                FindVisual(request.VisualId);
            if (provider == null || descriptor == null)
            {
                return FailureEnvelope("unknown_visual", $"Visual '{request.VisualId}' is not registered.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Math.Clamp(timeoutMilliseconds, 100, 60000));
            try
            {
                RuntimeAutomationVisualCapture capture =
                    await provider.CaptureVisualAsync(request, timeout.Token);
                return SuccessEnvelope(new JObject
                {
                    ["visual_id"] = descriptor.Id,
                    ["provider"] = provider.Id,
                    ["capture"] = capture?.ToJson() ?? new RuntimeAutomationVisualCapture
                    {
                        Captured = false,
                        BlockedReason = "provider_returned_no_capture",
                    }.ToJson(),
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return FailureEnvelope("timeout", $"Visual capture exceeded {timeoutMilliseconds} ms.", provider.Id);
            }
            catch (Exception exception)
            {
                return FailureEnvelope("capture_failed", exception.Message, provider.Id);
            }
        }

        public JObject GetDiagnostics(string fields)
        {
            ThrowIfDisposed();
            IReadOnlyCollection<string> selectedFields = ParseCsv(fields);
            var data = new JObject();
            var errors = new JArray();
            foreach (IRuntimeAutomationDiagnosticsProvider provider in registry.DiagnosticsProviders)
            {
                try
                {
                    data[provider.Id] = provider.CaptureDiagnostics(
                        new RuntimeAutomationQueryContext(selectedFields)) ?? new JObject();
                }
                catch (Exception exception)
                {
                    errors.Add(ErrorJson("provider_failed", exception.Message, provider.Id));
                }
            }

            data["runtime_automation"] = new JObject
            {
                ["state_provider_count"] = registry.StateProviders.Count,
                ["entity_provider_count"] = registry.EntityProviders.Count,
                ["action_provider_count"] = registry.ActionProviders.Count,
                ["visual_provider_count"] = registry.VisualProviders.Count,
                ["qa_mutation_provider_count"] = registry.QaMutationProviders.Count,
                ["event_buffer_capacity"] = EventCapacity,
                ["event_buffer_count"] = GetEventCount(),
            };
            return SuccessEnvelope(new JObject
            {
                ["diagnostics"] = data,
                ["partial"] = errors.Count > 0,
                ["errors"] = errors,
            });
        }

        public JObject ListQaMutations()
        {
            ThrowIfDisposed();
            if (session.Mode != RuntimeAutomationMode.Qa)
            {
                return FailureEnvelope("qa_mode_required", "QA mutations are not registered in Verification mode.");
            }

            return SuccessEnvelope(new JObject
            {
                ["mutations"] = new JArray(ListMutationDescriptors().Select(descriptor => descriptor.ToJson())),
            });
        }

        public async Task<JObject> ExecuteQaMutationAsync(
            string mutationId,
            string targetId,
            JObject arguments,
            long expectedRevision,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (session.Mode != RuntimeAutomationMode.Qa)
            {
                return FailureEnvelope("qa_mode_required", "QA mutations cannot be enabled after process startup.");
            }

            (IRuntimeAutomationQaMutationProvider provider, RuntimeAutomationActionDescriptor descriptor) =
                FindMutation(mutationId);
            if (provider == null || descriptor == null)
            {
                return FailureEnvelope("unknown_mutation", $"QA mutation '{mutationId}' is not registered.");
            }

            if (descriptor.EvidenceLevel != RuntimeAutomationEvidenceLevel.DirectQa)
            {
                return FailureEnvelope("invalid_mutation", "QA mutation descriptors must declare direct_qa evidence.", provider.Id);
            }

            return await ExecuteWithProviderAsync(
                provider.Id,
                descriptor,
                context => provider.ExecuteMutationAsync(context),
                mutationId,
                targetId,
                arguments,
                expectedRevision,
                timeoutMilliseconds,
                cancellationToken);
        }

        public JObject SuccessEnvelope(JObject payload)
        {
            return Envelope(true, payload ?? new JObject(), null);
        }

        public JObject FailureEnvelope(string code, string message, string provider = null)
        {
            return Envelope(false, new JObject(), new JArray(ErrorJson(code, message, provider)));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (IRuntimeAutomationEventSource source in registry.EventSources)
            {
                source.EventPublished -= OnProviderEvent;
            }

            actionGate.Dispose();
        }

        private async Task<JObject> ExecuteWithProviderAsync(
            string providerId,
            RuntimeAutomationActionDescriptor descriptor,
            Func<RuntimeAutomationActionContext, Task<RuntimeAutomationActionEffect>> execute,
            string actionId,
            string targetId,
            JObject arguments,
            long expectedRevision,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int clampedTimeout = Math.Clamp(timeoutMilliseconds, 50, 60000);
            timeout.CancelAfter(clampedTimeout);
            bool acquired = false;
            long beforeRevision = Revision;
            long beforeSequence = EventSequence;
            try
            {
                await actionGate.WaitAsync(timeout.Token);
                acquired = true;
                beforeRevision = Revision;
                beforeSequence = EventSequence;
                if (expectedRevision >= 0 && expectedRevision != beforeRevision)
                {
                    return FailureEnvelope(
                        "stale_revision",
                        $"Expected revision {expectedRevision}, current revision is {beforeRevision}.",
                        providerId);
                }

                var context = new RuntimeAutomationActionContext(
                    this,
                    actionId,
                    targetId,
                    arguments?.DeepClone() as JObject ?? new JObject(),
                    timeout.Token);
                RuntimeAutomationActionEffect effect = await execute(context) ??
                    RuntimeAutomationActionEffect.Blocked("provider_returned_no_effect");
                if (effect.ObservedEffect && Revision == beforeRevision)
                {
                    MarkStateChanged(providerId, "action_effect_observed", new JObject
                    {
                        ["action_id"] = actionId,
                        ["target_id"] = targetId,
                    });
                }

                long afterRevision = Revision;
                long afterSequence = EventSequence;
                JArray generatedEvents = GetEventRecords(beforeSequence, RuntimeAutomationProtocol.MaximumEventCount);
                return SuccessEnvelope(new JObject
                {
                    ["action_id"] = actionId,
                    ["provider"] = providerId,
                    ["evidence_level"] = descriptor.EvidenceLevel.ToString().ToLowerInvariant(),
                    ["accepted"] = true,
                    ["dispatched"] = effect.Dispatched,
                    ["observed_effect"] = effect.ObservedEffect,
                    ["blocked_reason"] = effect.BlockedReason,
                    ["evidence"] = effect.Evidence,
                    ["before_revision"] = beforeRevision,
                    ["after_revision"] = afterRevision,
                    ["before_event_sequence"] = beforeSequence,
                    ["after_event_sequence"] = afterSequence,
                    ["events"] = generatedEvents,
                    ["result"] = effect.Result?.DeepClone() ?? new JObject(),
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return FailureEnvelope("timeout", $"Action '{actionId}' exceeded {clampedTimeout} ms.", providerId);
            }
            catch (OperationCanceledException)
            {
                return FailureEnvelope("cancelled", $"Action '{actionId}' was cancelled.", providerId);
            }
            catch (Exception exception)
            {
                return FailureEnvelope("action_failed", exception.Message, providerId);
            }
            finally
            {
                if (acquired) actionGate.Release();
            }
        }

        private JObject Envelope(bool ok, JObject payload, JArray errors)
        {
            return new JObject
            {
                ["ok"] = ok,
                ["schema_version"] = RuntimeAutomationProtocol.SchemaVersion,
                ["session"] = session.SessionId,
                ["build"] = new JObject
                {
                    ["id"] = session.BuildId ?? string.Empty,
                    ["mode"] = session.BuildMode ?? string.Empty,
                    ["git_revision"] = session.GitRevision ?? string.Empty,
                    ["dirty"] = session.DirtyBuild,
                },
                ["runtime_mode"] = ModeName(session.Mode),
                ["scene_epoch"] = SceneEpoch,
                ["frame"] = Time.frameCount,
                ["state_revision"] = Revision,
                ["event_sequence"] = EventSequence,
                ["payload"] = payload ?? new JObject(),
                ["errors"] = errors ?? new JArray(),
            };
        }

        private static JObject ProviderJson(IRuntimeAutomationProvider provider, string kind)
        {
            return new JObject
            {
                ["id"] = provider.Id,
                ["version"] = provider.Version,
                ["kind"] = kind,
            };
        }

        private static JObject ErrorJson(string code, string message, string provider = null)
        {
            var error = new JObject
            {
                ["code"] = code,
                ["message"] = message ?? string.Empty,
            };
            if (!string.IsNullOrWhiteSpace(provider)) error["provider"] = provider;
            return error;
        }

        private static HashSet<string> ParseCsvSet(string value)
        {
            return new HashSet<string>(ParseCsv(value), StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyCollection<string> ParseCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
            return value.Split(',')
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int ParseCursor(string cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor)) return 0;
            if (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 0)
            {
                throw new ArgumentException("Cursor must be a non-negative integer offset.", nameof(cursor));
            }

            return value;
        }

        private static string ModeName(RuntimeAutomationMode mode)
        {
            return mode == RuntimeAutomationMode.Qa ? "qa" : "verification";
        }

        private static IEnumerable<RuntimeAutomationActionDescriptor> ListActionDescriptors(
            IEnumerable<IRuntimeAutomationActionProvider> providers)
        {
            foreach (IRuntimeAutomationActionProvider provider in providers)
            {
                IEnumerable<RuntimeAutomationActionDescriptor> descriptors = provider.ListActions();
                if (descriptors == null) continue;
                foreach (RuntimeAutomationActionDescriptor descriptor in descriptors)
                {
                    if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id)) continue;
                    descriptor.Provider ??= provider.Id;
                    yield return descriptor;
                }
            }
        }

        private IEnumerable<RuntimeAutomationActionDescriptor> ListMutationDescriptors()
        {
            foreach (IRuntimeAutomationQaMutationProvider provider in registry.QaMutationProviders)
            {
                IEnumerable<RuntimeAutomationActionDescriptor> descriptors = provider.ListMutations();
                if (descriptors == null) continue;
                foreach (RuntimeAutomationActionDescriptor descriptor in descriptors)
                {
                    if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id)) continue;
                    descriptor.Provider ??= provider.Id;
                    yield return descriptor;
                }
            }
        }

        private (IRuntimeAutomationActionProvider, RuntimeAutomationActionDescriptor) FindAction(string id)
        {
            foreach (IRuntimeAutomationActionProvider provider in registry.ActionProviders)
            {
                RuntimeAutomationActionDescriptor descriptor = provider.ListActions()?.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
                if (descriptor == null) continue;
                descriptor.Provider ??= provider.Id;
                return (provider, descriptor);
            }

            return (null, null);
        }

        private (IRuntimeAutomationQaMutationProvider, RuntimeAutomationActionDescriptor) FindMutation(string id)
        {
            foreach (IRuntimeAutomationQaMutationProvider provider in registry.QaMutationProviders)
            {
                RuntimeAutomationActionDescriptor descriptor = provider.ListMutations()?.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
                if (descriptor == null) continue;
                descriptor.Provider ??= provider.Id;
                return (provider, descriptor);
            }

            return (null, null);
        }

        private (IRuntimeAutomationVisualProvider, RuntimeAutomationVisualDescriptor) FindVisual(string id)
        {
            foreach (IRuntimeAutomationVisualProvider provider in registry.VisualProviders)
            {
                RuntimeAutomationVisualDescriptor descriptor = provider.ListVisuals()?.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
                if (descriptor == null) continue;
                descriptor.Provider ??= provider.Id;
                return (provider, descriptor);
            }

            return (null, null);
        }

        private JArray GetEventRecords(long afterSequence, int limit)
        {
            lock (eventGate)
            {
                return new JArray(events
                    .Where(record => record.Sequence > afterSequence)
                    .Take(limit)
                    .Select(record => record.ToJson()));
            }
        }

        private int GetEventCount()
        {
            lock (eventGate) return events.Count;
        }

        private static bool Compare(JToken actual, JToken expected, string comparison)
        {
            string operation = NormalizeComparison(comparison);
            if (operation == "exists") return actual != null && actual.Type != JTokenType.Null;
            if (operation == "not_exists") return actual == null || actual.Type == JTokenType.Null;
            if (operation == "equals") return JToken.DeepEquals(actual, expected) ||
                string.Equals(actual?.ToString(), expected?.ToString(), StringComparison.OrdinalIgnoreCase);
            if (operation == "not_equals") return !Compare(actual, expected, "equals");
            if (operation == "contains") return actual?.ToString().IndexOf(
                expected?.ToString() ?? string.Empty,
                StringComparison.OrdinalIgnoreCase) >= 0;

            if (!double.TryParse(actual?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double left) ||
                !double.TryParse(expected?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double right))
            {
                return false;
            }

            return operation switch
            {
                "greater" => left > right,
                "greater_or_equal" => left >= right,
                "less" => left < right,
                "less_or_equal" => left <= right,
                _ => false,
            };
        }

        private static string NormalizeComparison(string comparison)
        {
            return string.IsNullOrWhiteSpace(comparison)
                ? "equals"
                : comparison.Trim().ToLowerInvariant();
        }

        private void OnProviderEvent(RuntimeAutomationEventDraft draft)
        {
            PublishEvent(draft);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(RuntimeAutomationHost));
        }
    }
}
