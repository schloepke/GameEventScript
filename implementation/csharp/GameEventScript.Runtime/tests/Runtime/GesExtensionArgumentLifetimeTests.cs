// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptMessage;

namespace GameEventScript.Tests.Native.Runtime;

// An extension's argument view borrows the VM's registers for the call only; a retained view must fail instead of reading
// another handler's registers, and the result reaches its register only after the callback returns.
[TestClass]
public sealed class GesExtensionArgumentLifetimeTests
{
    [TestMethod]
    public void RetainedArgumentsFailAfterTheirCall()
    {
        var extension = new RetainingExtension();
        var program = new GesProgramBuilder().AddScript("on Start(a) { let value be :app.keep(a); emit Done(value: value) }\non Other(b) { let value be b + 1; emit Done(value: value) }").Compile();
        var host = GameEventScriptHost.CreateBuilder().WithRegistry(extension).Build();
        host.Load(program);
        host.Start();
        Assert.IsTrue(host.Receive(Create("Start", [new GesMessageArgument("a", GesValue.GesInteger(7))])));
        host.RunToCompletion();
        Assert.AreEqual(7L, extension.SeenDuringCall);
        Assert.IsTrue(host.Receive(Create("Other", [new GesMessageArgument("b", GesValue.GesInteger(40))])));
        host.RunToCompletion();
        Assert.ThrowsExactly<InvalidOperationException>(() => extension.Retained.ValueAt(0));
    }

    private sealed class RetainingExtension : IGesExtensionRegistry, IGesExtensionFunction
    {
        public GesValueArguments Retained { get; private set; }

        public long SeenDuringCall { get; private set; }

        public IGesExtensionFunction? Resolve(GesExtensionReference reference) => this;

        public void Invoke(GesExtensionCall call)
        {
            // A provisional result does not overwrite an argument before it is read.
            call.SetInteger(-1);
            SeenDuringCall = call.Arguments.ValueAt(0).AsInteger();
            Retained = call.Arguments;
            call.SetInteger(SeenDuringCall);
        }
    }
}
