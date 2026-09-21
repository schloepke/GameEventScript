// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;

namespace GameEventScript.Tests.Native.Runtime;

[TestClass]
public sealed class GameEventScriptStartupBootstrapTests
{
    [TestMethod]
    public void StartIsExplicitAndIdempotentAndDoesNotDrainQueuedWork()
    {
        var host = GameEventScriptHost.CreateBuilder().Build();
        var delivered = 0;
        host.Subscribe("Tick", [], (_, _) => delivered++);
        var instance = host.Load(GameEventScriptBuilder.Create().AddScript("on initialization { emit Tick() }").Compile());
        Assert.IsFalse(host.IsReady);
        Assert.IsNull(instance.StartResult);
        Assert.IsFalse(host.Receive(GameEventScriptMessage.Create("Tick")));
        Assert.ThrowsExactly<InvalidOperationException>(() => host.RunToCompletion());
        var start = host.Start();
        Assert.AreEqual(GameEventScriptStartState.Ready, start.State);
        Assert.IsGreaterThan(0, start.ExecutedOpcodes);
        Assert.AreEqual(1, start.ProcessedMessages);
        Assert.AreEqual(1, start.EmittedMessages);
        Assert.AreEqual(start.ExecutedOpcodes, instance.StartResult!.Value.ExecutedOpcodes);
        Assert.AreEqual(GameEventScriptStartState.Ready, instance.StartResult!.Value.State);
        Assert.AreEqual(0, delivered);
        Assert.AreEqual(1, host.PendingMessageCount);
        Assert.AreEqual(GameEventScriptStartState.Ready, host.Start().State);
        Assert.AreEqual(1, host.PendingMessageCount);
        host.RunToCompletion();
        Assert.AreEqual(1, delivered);
    }

    [TestMethod]
    public void ReadyRunnerCanBeConfiguredBeforeExplicitStart()
    {
        using var runner = GameEventScriptCSharpHostRunner.Create(GameEventScriptHost.CreateBuilder().Build());
        var delivered = 0;
        runner.Subscribe("Tick", [], (_, _) => delivered++);
        var instance = runner.Load(GameEventScriptBuilder.Create().AddScript("on initialization { emit Tick() }").Compile());
        Assert.IsFalse(runner.IsReady);
        Assert.IsNull(runner.GetStartResult(instance));
        Assert.IsFalse(runner.Receive(GameEventScriptMessage.Create("Tick")));
        Assert.AreEqual(0, delivered);
        Assert.AreEqual(GameEventScriptStartState.Ready, runner.Start().State);
        runner.RunToCompletion();
        Assert.AreEqual(1, delivered);
        Assert.AreEqual(GameEventScriptStartState.Ready, runner.GetStartResult(instance)!.Value.State);
    }
}
