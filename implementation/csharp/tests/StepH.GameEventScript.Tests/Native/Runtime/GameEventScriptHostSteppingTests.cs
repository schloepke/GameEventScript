// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime.VM;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.Native.Runtime;

[TestClass]
public sealed class GameEventScriptHostSteppingTests
{
    [TestMethod]
    public void HostReusesOneContext()
    {
        var contexts = new List<GameEventScriptContext>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) => contexts.Add(context));

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();

        Assert.HasCount(2, contexts);
        Assert.AreSame(contexts[0], contexts[1]);
    }

    [TestMethod]
    public void LoadedProgramsShareOneFullyResetHostVmState()
    {
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(Compile("on First { emit Done }"));
        var vmState = host.VmState;
        host.Load(Compile("on Second { emit Done }"));
        host.Subscribe("Done", [], (_, _) => { });

        Assert.AreSame(vmState, host.VmState);
        host.Receive(Create("First"));
        host.Receive(Create("Second"));
        host.RunToCompletion();

        Assert.IsNotNull(vmState);
        Assert.AreEqual(GesVmState.StateValue.Ready, vmState.State);
        Assert.IsNull(vmState.ActiveProgram);
        Assert.IsNull(vmState.ProcessingMessage);
        Assert.IsTrue(vmState.RegisterValues.All(value => value.IsNothing));
    }

    [TestMethod]
    public void HostsBuiltFromTheSameConfigurationOwnIndependentRandomStreams()
    {
        var builder = GameEventScriptHost.CreateBuilder().WithRandomSeed(42L);
        var firstHost = builder.Build();
        var secondHost = builder.Build();
        var firstValues = new List<long>();
        var secondValues = new List<long>();
        firstHost.Subscribe("Take", [], (_, context) => firstValues.Add(context.Random.NextInclusiveInteger(long.MinValue, long.MaxValue)));
        secondHost.Subscribe("Take", [], (_, context) => secondValues.Add(context.Random.NextInclusiveInteger(long.MinValue, long.MaxValue)));

        firstHost.Receive(Create("Take"));
        firstHost.Receive(Create("Take"));
        secondHost.Receive(Create("Take"));
        firstHost.RunToCompletion();
        secondHost.RunToCompletion();

        Assert.HasCount(2, firstValues);
        Assert.HasCount(1, secondValues);
        Assert.AreEqual(firstValues[0], secondValues[0]);
        Assert.AreNotEqual(firstValues[0], firstValues[1]);
    }

    [TestMethod]
    public void NativeRandomOverpushFaultsTheHandlerAndRestoresTheHostStream()
    {
        var limits = new List<TestRuntimeLimitEvent>();
        var rejectedEmitAccepted = true;
        long? followingValue = null;
        var observer = TestRuntimeObserver.ObserveMessages(runtimeLimitReached: (name, detail, limit) => limits.Add(new TestRuntimeLimitEvent(name, detail, limit)));
        var host = GameEventScriptHost.CreateBuilder()
            .WithRandomSeed(0L)
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxRandomScopeDepth = 1 })
            .WithRuntimeObserver(observer)
            .Build();
        host.Subscribe("Start", [], (message, context) =>
        {
            _ = message;
            Assert.IsTrue(context.Random.Push(7L));
            Assert.IsFalse(context.Random.Push(8L));
            _ = context.Random.NextInclusiveInteger(1, 100);
            rejectedEmitAccepted = context.Emit("Rejected");
            Assert.IsFalse(context.Random.Pop());
            Assert.IsFalse(context.Random.Pop());
        });
        host.Subscribe("Rejected", [], (_, _) => Assert.Fail("A faulted native handler must not enqueue messages."));
        host.Subscribe("Check", [], (_, context) => followingValue = context.Random.NextInclusiveInteger(long.MinValue, long.MaxValue));
        host.Receive(Create("Start"));
        host.Receive(Create("Check"));

        var limited = host.RunToCompletion();
        var completed = host.RunToCompletion();

        var expected = GameEventScriptRandomGenerator.FromSeed(0L).NextInclusiveInteger(long.MinValue, long.MaxValue);
        Assert.AreEqual(GameEventScriptExecutionState.RuntimeLimitReached, limited.State);
        Assert.AreEqual(GameEventScriptExecutionState.Completed, completed.State);
        Assert.IsFalse(rejectedEmitAccepted);
        Assert.AreEqual(expected, followingValue);
        Assert.HasCount(1, limits);
        Assert.AreEqual(nameof(GameEventScriptRuntimeLimits.MaxRandomScopeDepth), limits[0].Name);
        Assert.AreEqual(1, limits[0].Limit);
    }

    [TestMethod]
    public void ExtensionRandomOverpushFaultsTheVmAndRestoresTheHostStream()
    {
        var emitted = new List<GameEventScriptMessage>();
        var limits = new List<TestRuntimeLimitEvent>();
        var observer = TestRuntimeObserver.ObserveMessages(
            messageEmitted: emitted.Add,
            runtimeLimitReached: (name, detail, limit) => limits.Add(new TestRuntimeLimitEvent(name, detail, limit)));
        var host = GameEventScriptHost.CreateBuilder()
            .WithRandomSeed(0L)
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxRandomScopeDepth = 1 })
            .WithRuntimeObserver(observer)
            .WithRegistry(RandomScopeExtensionRegistry.Instance)
            .Build();
        host.Load(Compile("on Start { let value be :randomTest.overpush; emit Done(value: value) }"));
        long? followingValue = null;
        host.Subscribe("Rejected", [], (_, _) => Assert.Fail("A faulted extension must not enqueue messages."));
        host.Subscribe("Done", ["value"], (_, _) => Assert.Fail("The VM must halt before emitting the extension result."));
        host.Subscribe("Check", [], (_, context) => followingValue = context.Random.NextInclusiveInteger(long.MinValue, long.MaxValue));
        host.Receive(Create("Start"));
        host.Receive(Create("Check"));

        var limited = host.RunToCompletion();
        var completed = host.RunToCompletion();

        var expected = GameEventScriptRandomGenerator.FromSeed(0L).NextInclusiveInteger(long.MinValue, long.MaxValue);
        Assert.AreEqual(GameEventScriptExecutionState.RuntimeLimitReached, limited.State);
        Assert.AreEqual(GameEventScriptExecutionState.Completed, completed.State);
        Assert.AreEqual(expected, followingValue);
        Assert.HasCount(1, limits);
        Assert.AreEqual(nameof(GameEventScriptRuntimeLimits.MaxRandomScopeDepth), limits[0].Name);
        Assert.IsTrue(emitted.Any(message => message.Name == "Rejected"));
        Assert.IsFalse(emitted.Any(message => message.Name == "Done"));
    }

    [TestMethod]
    public void ExtensionCannotLeakARandomScopeAcrossItsBoundary()
    {
        var diagnostics = new List<GameEventScriptDiagnostic>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRandomSeed(0L)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(runtimeError: diagnostics.Add))
            .WithRegistry(RandomScopeExtensionRegistry.Instance)
            .Build();
        host.Load(Compile("on Start { let value be :randomTest.leak; emit Done(value: value) }"));
        long? followingValue = null;
        host.Subscribe("Done", ["value"], (_, _) => Assert.Fail("The VM must halt when an extension leaks a random scope."));
        host.Subscribe("Check", [], (_, context) => followingValue = context.Random.NextInclusiveInteger(long.MinValue, long.MaxValue));
        host.Receive(Create("Start"));
        host.Receive(Create("Check"));

        var result = host.RunToCompletion();

        var expected = GameEventScriptRandomGenerator.FromSeed(0L).NextInclusiveInteger(long.MinValue, long.MaxValue);
        Assert.AreEqual(GameEventScriptExecutionState.RuntimeError, result.State);
        Assert.AreEqual(expected, followingValue);
        Assert.HasCount(1, diagnostics);
        Assert.AreEqual(GameEventScriptDiagnosticCodes.RuntimeRandomScopeImbalance, diagnostics[0].Code);
    }

    [TestMethod]
    public void RandomHandlerScopeCanResumeAcrossSerializedThreadHandoffs()
    {
        var values = new List<long>();
        var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(0L).Build();
        host.Load(Compile("on Start { random with 7 { emit Done(value: random from 1 to 100) } }"));
        host.Subscribe("Done", ["value"], (message, _) => values.Add(message.Arguments.GetAsInteger("value")));
        host.Subscribe("Check", [], (_, context) => values.Add(context.Random.NextInclusiveInteger(long.MinValue, long.MaxValue)));
        host.Receive(Create("Start"));

        while (!host.IsIdle)
        {
            var result = ExecuteOneFrameOnNewThread(host);
            Assert.AreNotEqual(GameEventScriptExecutionState.RuntimeError, result.State);
            Assert.AreNotEqual(GameEventScriptExecutionState.RuntimeLimitReached, result.State);
        }
        host.Receive(Create("Check"));
        ExecuteOneFrameOnNewThread(host, int.MaxValue);

        Assert.HasCount(2, values);
        Assert.AreEqual(GameEventScriptRandomGenerator.FromSeed(7L).NextInclusiveInteger(1, 100), values[0]);
        Assert.AreEqual(GameEventScriptRandomGenerator.FromSeed(0L).NextInclusiveInteger(long.MinValue, long.MaxValue), values[1]);
    }

    [TestMethod]
    public void CSharpAutoRunnerSerializesReceiveAndPumps()
    {
        var completed = new ManualResetEventSlim(false);
        var host = GameEventScriptHost.CreateBuilder().Build();
        using var runner = host.RunAutomatically();
        runner.Subscribe("Start", [], (_, _) => completed.Set());
        Assert.IsTrue(runner.Receive(Create("Start")));
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void CSharpAutoRunnerSerializesConcurrentReceives()
    {
        const int messageCount = 32;
        using var completed = new CountdownEvent(messageCount);
        var active = 0;
        var maximumActive = 0;
        var host = GameEventScriptHost.CreateBuilder().Build();
        using var runner = host.RunAutomatically();
        runner.Subscribe("Start", [], (_, _) =>
        {
            var nowActive = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximumActive, nowActive);
            Thread.SpinWait(10_000);
            Interlocked.Decrement(ref active);
            completed.Signal();
        });

        Parallel.For(0, messageCount, _ => Assert.IsTrue(runner.Receive(Create("Start"))));

        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, maximumActive);
    }

    [TestMethod]
    public void CSharpAutoRunnerSupportsDynamicSubscribeAndDetachWithSnapshots()
    {
        using var nativeCompleted = new CountdownEvent(2);
        using var scriptCompleted = new ManualResetEventSlim(false);
        var nativeCalls = 0;
        var observer = TestRuntimeObserver.ObserveMessages(
            messageEmitted: message =>
            {
                if (message.Name == "ScriptDone") scriptCompleted.Set();
            });
        var host = GameEventScriptHost.CreateBuilder().WithRuntimeObserver(observer).Build();
        using var runner = host.RunAutomatically();

        var first = runner.Subscribe("Native", [], (_, _) =>
        {
            Interlocked.Increment(ref nativeCalls);
            nativeCompleted.Signal();
        });
        Assert.IsTrue(runner.Receive(Create("Native")));
        Assert.IsTrue(runner.Unsubscribe(first));

        var second = runner.Subscribe("Native", [], (_, _) =>
        {
            Interlocked.Increment(ref nativeCalls);
            nativeCompleted.Signal();
        });
        Assert.IsTrue(runner.Receive(Create("Native")));
        Assert.IsTrue(runner.Unsubscribe(second));

        var instance = runner.Load(Compile("on ScriptStart { emit ScriptDone }"));
        Assert.IsTrue(runner.Receive(Create("ScriptStart")));
        Assert.IsTrue(runner.Detach(instance));

        Assert.IsTrue(nativeCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scriptCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(2, nativeCalls);
        Assert.IsFalse(runner.Receive(Create("Native")));
        Assert.IsFalse(runner.Receive(Create("ScriptStart")));
    }

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    public void WarmQueueDispatchFrameResultAndVmResumeDoNotAllocate()
    {
        const int iterations = 1_000;
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(Compile("""
                          on Tick {
                            let value be 1 + 2 + 3
                            random with 7 {
                              let outer be random from 1 to 6
                              random with (nothing as :Number) {
                                let inner be random from 1 to 6
                              }
                            }
                          }
                          """));
        var message = Create("Tick");

        for (var index = 0; index < 100; index++)
        {
            host.Receive(message);
            while (!host.IsIdle) host.ExecuteFrame(1);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index++)
        {
            host.Receive(message);
            while (!host.IsIdle) host.ExecuteFrame(1);
        }

        Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static GameEventScriptProgram Compile(string source)
        => GameEventScriptBuilder.Create().AddScript(source).Compile();

    private static void UpdateMaximum(ref int target, int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (candidate <= current) return;
        }
        while (Interlocked.CompareExchange(ref target, candidate, current) != current);
    }

    private static GameEventScriptExecutionResult ExecuteOneFrameOnNewThread(GameEventScriptHost host, int budget = 1)
    {
        var result = default(GameEventScriptExecutionResult);
        var thread = new Thread(() => result = host.ExecuteFrame(budget));
        thread.Start();
        thread.Join();
        return result;
    }

    private sealed class RandomScopeExtensionRegistry : IGameEventScriptExtensionRegistry
    {
        internal static RandomScopeExtensionRegistry Instance { get; } = new();

        private static readonly IGameEventScriptExtensionFunction Overpush = new RandomScopeOverpushExtension();
        private static readonly IGameEventScriptExtensionFunction Leak = new RandomScopeLeakExtension();

        public IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference)
        {
            if (reference.ExtensionName != "randomTest" || reference.ArgumentLabels.Count != 0) return null;
            return reference.FunctionName switch
            {
                "overpush" => Overpush,
                "leak" => Leak,
                _ => null
            };
        }
    }

    private sealed class RandomScopeOverpushExtension : IGameEventScriptExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
        {
            Assert.IsTrue(call.Random.Push(7L));
            Assert.IsFalse(call.Random.Push(8L));
            _ = call.Random.NextInclusiveInteger(1, 100);
            Assert.IsFalse(call.Context.Emit("Rejected"));
            call.SetInteger(99);
        }
    }

    private sealed class RandomScopeLeakExtension : IGameEventScriptExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
        {
            Assert.IsTrue(call.Random.Push(7L));
            _ = call.Random.NextInclusiveInteger(1, 100);
            call.SetInteger(99);
        }
    }

}
