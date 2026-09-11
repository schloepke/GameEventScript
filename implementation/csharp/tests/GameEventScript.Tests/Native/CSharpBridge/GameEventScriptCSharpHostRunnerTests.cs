// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;

namespace GameEventScript.Tests.Native.CSharpBridge;

[TestClass]
[DoNotParallelize]
public sealed class GameEventScriptCSharpHostRunnerTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AutomaticRunnerPumpsWorkQueuedBeforeItWasCreated(bool queuedInitialization)
    {
        var delivered = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Ready", [], (message, _) => delivered.Add(message));
        if (queuedInitialization) host.Load(GameEventScriptBuilder.Create().AddScript("on initialization { emit Ready() }").Compile());
        else Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Ready")));
        Assert.IsFalse(host.IsIdle);

        using var barrier = new ManualResetEventSlim(false);
        using var runner = host.RunAutomatically();
        // The shared dispatcher has one FIFO worker. This barrier follows any initial
        // pump scheduled by Create; it does not send another message to the target host.
        GameEventScriptCSharpDispatcher.Shared.Enqueue(barrier.Set);
        Assert.IsTrue(barrier.Wait(TimeSpan.FromSeconds(5)), "The shared dispatcher did not reach the test barrier.");
        runner.Dispose();

        Assert.HasCount(1, delivered, "Taking ownership must schedule existing work without a subsequent Receive or Load.");
        Assert.AreEqual("Ready", delivered[0].Name);
        Assert.IsTrue(host.IsIdle);
    }

    [TestMethod]
    public void AutomaticRunnerResumesAnAlreadyPausedScriptHandler()
    {
        var delivered = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Ready", [], (message, _) => delivered.Add(message));
        host.Load(GameEventScriptBuilder.Create().AddScript("on Start { emit Ready() }").Compile());
        Assert.AreEqual(GameEventScriptExecutionState.Completed, host.RunToCompletion().State);
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Start")));
        Assert.AreEqual(GameEventScriptExecutionState.Paused, host.ExecuteFrame(1).State);
        Assert.IsFalse(host.IsIdle);

        using var barrier = new ManualResetEventSlim(false);
        using var runner = GameEventScriptCSharpHostRunner.Create(host);
        GameEventScriptCSharpDispatcher.Shared.Enqueue(barrier.Set);
        Assert.IsTrue(barrier.Wait(TimeSpan.FromSeconds(5)), "The shared dispatcher did not resume the paused host.");
        runner.Dispose();

        Assert.HasCount(1, delivered);
        Assert.AreEqual("Ready", delivered[0].Name);
        Assert.IsTrue(host.IsIdle);
    }

    [TestMethod]
    public void DisposingBeforeTheInitialPumpPreservesPendingHostWork()
    {
        var delivered = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Ready", [], (message, _) => delivered.Add(message));
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Ready")));

        using var workerEntered = new ManualResetEventSlim(false);
        using var releaseWorker = new ManualResetEventSlim(false);
        using var barrier = new ManualResetEventSlim(false);
        // Hold the single worker so Dispose deterministically precedes the initial pump.
        GameEventScriptCSharpDispatcher.Shared.Enqueue(() =>
        {
            workerEntered.Set();
            releaseWorker.Wait();
        });
        try
        {
            Assert.IsTrue(workerEntered.Wait(TimeSpan.FromSeconds(5)), "The shared dispatcher did not reach the worker gate.");
            using var runner = host.RunAutomatically();
            runner.Dispose();
            GameEventScriptCSharpDispatcher.Shared.Enqueue(barrier.Set);
            releaseWorker.Set();
            Assert.IsTrue(barrier.Wait(TimeSpan.FromSeconds(5)), "The shared dispatcher did not finish the suppressed pump.");

            Assert.HasCount(0, delivered);
            Assert.IsFalse(host.IsIdle);
            Assert.AreEqual(GameEventScriptExecutionState.Completed, host.RunToCompletion().State);
            Assert.HasCount(1, delivered);
            Assert.IsTrue(host.IsIdle);
        }
        finally
        {
            releaseWorker.Set();
        }
    }
}
