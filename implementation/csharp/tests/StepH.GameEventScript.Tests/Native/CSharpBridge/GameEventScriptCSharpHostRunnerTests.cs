// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;

namespace StepH_GameEventScript_Tests.Native.CSharpBridge;

[TestClass]
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
}
