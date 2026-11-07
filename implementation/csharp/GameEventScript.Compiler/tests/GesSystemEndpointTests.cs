// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Compiler;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Compiler;

// Handler values have no Conformance transport encoding, so what they can create is pinned natively.
[TestClass]
public sealed class GesSystemEndpointTests
{
    [TestMethod]
    public void SystemEndpointSignaturesCreateNoMessage()
    {
        Assert.IsNull(GesMessageSignature.Create("initialization", []).CreateMessage([]));
        var signature = GesMessageSignature.Create("undeliverable", ["message"]);
        Assert.IsNull(signature.CreateMessage([GesValue.GesNothing()]));
        Assert.ThrowsExactly<ArgumentException>(() => signature.WithArguments(GesValue.GesNothing()));
        Assert.ThrowsExactly<GesMessageFormatException>(() => GesValue.GesHandler(signature).ToJson());
    }

    [TestMethod]
    public void BindingAHandlerOfASystemEndpointCreatesNoMessage()
    {
        var program = new GesProgramBuilder().AddScript("""
            on Start(handler) {
              let bound be asm { BindHandler bound, handler(message: 42) }
              emit Seen(missing: bound is nothing)
            }
            """).Compile();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(program);
        var seen = new SeenHandler();
        host.Subscribe(GesMessageSignature.Create("Seen", ["missing"]), seen);
        Assert.AreEqual(GesStartState.Ready, host.Start().State);
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Start", [new("handler", GesValue.GesHandler(GesMessageSignature.Create("undeliverable", ["message"])))])));
        Assert.AreEqual(GesExecutionState.Completed, host.RunToCompletion().State);
        Assert.IsTrue(seen.Missing);
    }

    private sealed class SeenHandler : IGesNativeMessageHandler
    {
        public bool Missing { get; private set; }

        public void Handle(GameEventScriptMessage message, GesContext context) => Missing = message.Arguments[0].AsBoolean();
    }
}
