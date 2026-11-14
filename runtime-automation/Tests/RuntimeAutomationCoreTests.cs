using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Vocalith.RuntimeAutomation;
using Vocalith.RuntimeAutomation.InputSystemSupport;

public sealed class RuntimeAutomationCoreTests
{
    [Test]
    public void QueryState_FailingProvider_ReturnsPartialErrorWithoutDroppingHealthyProvider()
    {
        using RuntimeAutomationHost host = CreateHost(
            RuntimeAutomationMode.Verification,
            registry => registry
                .Register(new StateProvider("healthy", () => new JObject { ["value"] = 7 }))
                .Register(new StateProvider("broken", () => throw new InvalidOperationException("boom"))));

        JObject response = host.QueryState(string.Empty, string.Empty);

        Assert.That(response.Value<bool>("ok"), Is.True);
        Assert.That(response.SelectToken("payload.partial")?.Value<bool>(), Is.True);
        Assert.That(response.SelectToken("payload.domains.healthy.value")?.Value<int>(), Is.EqualTo(7));
        Assert.That(response.SelectToken("payload.errors[0].provider")?.Value<string>(), Is.EqualTo("broken"));
    }

    [Test]
    public void QueryEntities_PagesInStableIdOrder()
    {
        using RuntimeAutomationHost host = CreateHost(
            RuntimeAutomationMode.Verification,
            registry => registry.Register(new EntityProvider("c", "a", "b")));

        JObject first = host.QueryEntities(new RuntimeAutomationEntityQuery(), string.Empty, 2);
        JObject second = host.QueryEntities(
            new RuntimeAutomationEntityQuery(),
            first.SelectToken("payload.page.next_cursor")?.Value<string>(),
            2);

        CollectionAssert.AreEqual(
            new[] { "a", "b" },
            first.SelectToken("payload.entities")?.Values<string>("id").ToArray());
        CollectionAssert.AreEqual(
            new[] { "c" },
            second.SelectToken("payload.entities")?.Values<string>("id").ToArray());
    }

    [Test]
    public void Events_PreserveSequenceAndAdvanceRevisionOnlyForStateChanges()
    {
        using RuntimeAutomationHost host = CreateHost(RuntimeAutomationMode.Verification, _ => { });

        host.PublishEvent(new RuntimeAutomationEventDraft { Type = "observed", Provider = "test", ChangesState = false });
        host.PublishEvent(new RuntimeAutomationEventDraft { Type = "changed", Provider = "test", ChangesState = true });

        JObject response = host.GetEvents(0, 10);
        JArray events = (JArray)response.SelectToken("payload.events");
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0].Value<long>("sequence"), Is.EqualTo(1));
        Assert.That(events[0].Value<long>("revision"), Is.EqualTo(0));
        Assert.That(events[1].Value<long>("sequence"), Is.EqualTo(2));
        Assert.That(events[1].Value<long>("revision"), Is.EqualTo(1));
    }

    [Test]
    public void QaMutations_VerificationMode_IsHardGated()
    {
        using RuntimeAutomationHost host = CreateHost(
            RuntimeAutomationMode.Verification,
            registry => registry.Register(new MutationProvider()));

        JObject list = host.ListQaMutations();
        JObject execute = host.ExecuteQaMutationAsync(
                "qa.test",
                null,
                new JObject(),
                -1,
                1000,
                default)
            .GetAwaiter()
            .GetResult();

        Assert.That(list.Value<bool>("ok"), Is.False);
        Assert.That(execute.Value<bool>("ok"), Is.False);
        Assert.That(execute.SelectToken("errors[0].code")?.Value<string>(), Is.EqualTo("qa_mode_required"));
    }

    [Test]
    public void ExecuteAction_StaleRevision_IsRejectedBeforeDispatch()
    {
        var provider = new ActionProvider();
        using RuntimeAutomationHost host = CreateHost(
            RuntimeAutomationMode.Verification,
            registry => registry.Register(provider));
        host.MarkStateChanged("test", "precondition_changed");

        JObject response = host.ExecuteActionAsync(
                "test.action",
                null,
                new JObject(),
                0,
                1000,
                default)
            .GetAwaiter()
            .GetResult();

        Assert.That(response.Value<bool>("ok"), Is.False);
        Assert.That(response.SelectToken("errors[0].code")?.Value<string>(), Is.EqualTo("stale_revision"));
        Assert.That(provider.ExecutionCount, Is.Zero);
    }

    [Test]
    public void ExecuteAction_ReceiptSeparatesDispatchFromObservedEffect()
    {
        using RuntimeAutomationHost host = CreateHost(
            RuntimeAutomationMode.Verification,
            registry => registry.Register(new ActionProvider(observed: false)));

        JObject response = host.ExecuteActionAsync(
                "test.action",
                null,
                new JObject(),
                -1,
                1000,
                default)
            .GetAwaiter()
            .GetResult();

        Assert.That(response.Value<bool>("ok"), Is.True);
        Assert.That(response.SelectToken("payload.accepted")?.Value<bool>(), Is.True);
        Assert.That(response.SelectToken("payload.dispatched")?.Value<bool>(), Is.True);
        Assert.That(response.SelectToken("payload.observed_effect")?.Value<bool>(), Is.False);
        Assert.That(response.SelectToken("payload.blocked_reason")?.Value<string>(), Is.EqualTo("no_effect"));
    }

    [Test]
    public async Task ExecuteAction_TimeoutReleasesSerialGateForNextAction()
    {
        var provider = new FirstCallBlocksActionProvider();
        using RuntimeAutomationHost host = CreateHost(
            RuntimeAutomationMode.Verification,
            registry => registry.Register(provider));

        JObject timedOut = await host.ExecuteActionAsync(
                "test.block-once",
                null,
                new JObject(),
                -1,
                50,
                default);
        JObject next = await host.ExecuteActionAsync(
                "test.block-once",
                null,
                new JObject(),
                -1,
                1000,
                default);

        Assert.That(timedOut.SelectToken("errors[0].code")?.Value<string>(), Is.EqualTo("timeout"));
        Assert.That(next.Value<bool>("ok"), Is.True);
        Assert.That(next.SelectToken("payload.observed_effect")?.Value<bool>(), Is.True);
    }

    [Test]
    public void ExecuteAction_ExternalCancellationHasDedicatedError()
    {
        using RuntimeAutomationHost host = CreateHost(
            RuntimeAutomationMode.Verification,
            registry => registry.Register(new ActionProvider()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        JObject response = host.ExecuteActionAsync(
                "test.action",
                null,
                new JObject(),
                -1,
                1000,
                cancellation.Token)
            .GetAwaiter()
            .GetResult();

        Assert.That(response.Value<bool>("ok"), Is.False);
        Assert.That(response.SelectToken("errors[0].code")?.Value<string>(), Is.EqualTo("cancelled"));
    }

    [Test]
    public void BackgroundInputLease_DisposeRemovesAutomationDevicesAndRestoresCurrentDevices()
    {
        Keyboard previousKeyboard = Keyboard.current;
        Mouse previousMouse = Mouse.current;
        var lease = new RuntimeAutomationInputDeviceLease("RuntimeAutomationTest");
        RuntimeAutomationKeyboard automationKeyboard = lease.Keyboard;
        RuntimeAutomationMouse automationMouse = lease.Mouse;
        try
        {
            Assert.That(automationKeyboard.added, Is.True);
            Assert.That(automationMouse.added, Is.True);
            Assert.That(InputSystem.LoadLayout(automationKeyboard.layout).canRunInBackground, Is.True);
            Assert.That(InputSystem.LoadLayout(automationMouse.layout).canRunInBackground, Is.True);
        }
        finally
        {
            lease.Dispose();
        }

        Assert.That(automationKeyboard.added, Is.False);
        Assert.That(automationMouse.added, Is.False);
        if (previousKeyboard != null && previousKeyboard.added)
        {
            Assert.That(Keyboard.current, Is.SameAs(previousKeyboard));
        }
        if (previousMouse != null && previousMouse.added)
        {
            Assert.That(Mouse.current, Is.SameAs(previousMouse));
        }
    }

    private static RuntimeAutomationHost CreateHost(
        RuntimeAutomationMode mode,
        Action<RuntimeAutomationProviderRegistry> configure)
    {
        var registry = new RuntimeAutomationProviderRegistry();
        configure(registry);
        return new RuntimeAutomationHost(
            new RuntimeAutomationSessionInfo
            {
                SessionId = "test-session",
                BuildId = "test-build",
                BuildMode = mode.ToString(),
                Mode = mode,
                ArtifactRoot = "Temp/RuntimeAutomation/test-session",
            },
            registry);
    }

    private sealed class StateProvider : IRuntimeAutomationStateProvider
    {
        private readonly Func<JObject> capture;
        internal StateProvider(string id, Func<JObject> captureState) { Id = id; capture = captureState; }
        public string Id { get; }
        public string Version => "1";
        public JObject CaptureState(RuntimeAutomationQueryContext context) => capture();
    }

    private sealed class EntityProvider : IRuntimeAutomationEntityProvider
    {
        private readonly string[] ids;
        internal EntityProvider(params string[] values) { ids = values; }
        public string Id => "entities";
        public string Version => "1";
        public IEnumerable<RuntimeAutomationEntitySnapshot> QueryEntities(RuntimeAutomationEntityQuery query)
        {
            return ids.Select(id => new RuntimeAutomationEntitySnapshot { Id = id, Kind = "test", Name = id });
        }
    }

    private sealed class ActionProvider : IRuntimeAutomationActionProvider
    {
        private readonly bool observed;
        internal ActionProvider(bool observed = true) { this.observed = observed; }
        internal int ExecutionCount { get; private set; }
        public string Id => "actions";
        public string Version => "1";
        public IEnumerable<RuntimeAutomationActionDescriptor> ListActions()
        {
            yield return new RuntimeAutomationActionDescriptor
            {
                Id = "test.action",
                EvidenceLevel = RuntimeAutomationEvidenceLevel.SemanticInput,
            };
        }

        public Task<RuntimeAutomationActionEffect> ExecuteActionAsync(RuntimeAutomationActionContext context)
        {
            ExecutionCount++;
            return Task.FromResult(new RuntimeAutomationActionEffect
            {
                Dispatched = true,
                ObservedEffect = observed,
                BlockedReason = observed ? null : "no_effect",
                Evidence = "test",
            });
        }
    }

    private sealed class MutationProvider : IRuntimeAutomationQaMutationProvider
    {
        public string Id => "mutations";
        public string Version => "1";
        public IEnumerable<RuntimeAutomationActionDescriptor> ListMutations()
        {
            yield return new RuntimeAutomationActionDescriptor
            {
                Id = "qa.test",
                EvidenceLevel = RuntimeAutomationEvidenceLevel.DirectQa,
            };
        }

        public Task<RuntimeAutomationActionEffect> ExecuteMutationAsync(RuntimeAutomationActionContext context)
        {
            return Task.FromResult(new RuntimeAutomationActionEffect { Dispatched = true, ObservedEffect = true });
        }
    }

    private sealed class FirstCallBlocksActionProvider : IRuntimeAutomationActionProvider
    {
        private int calls;
        public string Id => "blocking-actions";
        public string Version => "1";

        public IEnumerable<RuntimeAutomationActionDescriptor> ListActions()
        {
            yield return new RuntimeAutomationActionDescriptor
            {
                Id = "test.block-once",
                EvidenceLevel = RuntimeAutomationEvidenceLevel.SemanticInput,
            };
        }

        public async Task<RuntimeAutomationActionEffect> ExecuteActionAsync(RuntimeAutomationActionContext context)
        {
            calls++;
            if (calls == 1)
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            }

            return new RuntimeAutomationActionEffect
            {
                Dispatched = true,
                ObservedEffect = true,
                Evidence = "second_call_completed",
            };
        }
    }
}
