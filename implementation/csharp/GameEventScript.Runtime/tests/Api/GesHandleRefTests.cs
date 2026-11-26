// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Api;

// Refs never enter the VM (specs/HostRuntime.md#handles). Conformance Markdown cannot construct a handle with a ref, so
// the boundary checks and GameEventScriptHost.ResolveRef are covered natively.
[TestClass]
public sealed class GesHandleRefTests
{
    private const string Schemas = "schema :DroneState as { altitude: :Number integer between 0 and 1000 }\n"
        + "handle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\n";

    private static GesHandleType DroneType => GesSchemaSet.Parse([new GesSchemaSource("drones.gess", Schemas)]).HandleTypes[0];

    private static GameEventScriptMessage WithRef(string name)
        => GameEventScriptMessage.Create(name, [new GesMessageArgument("drone", GesValue.GesHandle(GesHandle.Parse("@Drone:7~#live")))]);

    [TestMethod]
    public void ReceiveRejectsAHandleWithARef()
    {
        var host = GameEventScriptHost.CreateBuilder().Build().StartForTest();

        var exception = Assert.ThrowsExactly<GesWireException>(() => host.Receive(WithRef("Seen")));

        Assert.AreEqual(GesWireException.UnresolvedRef, exception.Code);
        Assert.AreEqual("Seen.drone", exception.Path);
        var delivered = 0;
        host.Subscribe(GesMessageSignature.Create("Seen", ["drone"]), (_, _) => delivered++);
        Assert.ThrowsExactly<GesWireException>(() => host.Receive(WithRef("Seen")));
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Seen", [new GesMessageArgument("drone", GesValue.GesHandle(GesHandle.Parse("@Drone:7~3")))])));
        Assert.AreEqual(GesExecutionState.Completed, host.RunToCompletion().State);
        Assert.AreEqual(1, delivered, "Only the message without a ref is enqueued.");
    }

    [TestMethod]
    public void ContextEmitAndPublishRejectAHandleWithARef()
    {
        var host = GameEventScriptHost.CreateBuilder().Build().StartForTest();
        var codes = new List<string>();
        host.Subscribe(GesMessageSignature.Create("Go", []), (_, context) =>
        {
            codes.Add(Assert.ThrowsExactly<GesWireException>(() => context.Emit(WithRef("Out"))).Code);
            codes.Add(Assert.ThrowsExactly<GesWireException>(() => context.Publish(WithRef("Out"))).Code);
        });

        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Go", [])));
        Assert.AreEqual(GesExecutionState.Completed, host.RunToCompletion().State);

        CollectionAssert.AreEqual(new[] { GesWireException.UnresolvedRef, GesWireException.UnresolvedRef }, codes);
    }

    [TestMethod]
    public void ResolveRefAsksTheResolverOfTheType()
    {
        var resolver = new RefResolver(GesValue.GesInteger(12));
        var host = GameEventScriptHost.CreateBuilder().WithHandleResolver(DroneType, resolver).Build().StartForTest();

        var resolved = host.ResolveRef(GesHandle.Parse("@Drone:7~#live"));

        Assert.AreEqual("@Drone:7~12", resolved.ToString());
        Assert.IsTrue(resolved.Ref.IsNothing);
        CollectionAssert.AreEqual(new[] { "@Drone:7~#live" }, resolver.Asked);
    }

    [TestMethod]
    public void ResolveRefReturnsAHandleWithoutARefUnchanged()
    {
        var resolver = new RefResolver(GesValue.GesInteger(12));
        var host = GameEventScriptHost.CreateBuilder().WithHandleResolver(DroneType, resolver).Build().StartForTest();
        var handle = GesHandle.Parse("@Drone:7~3");

        Assert.AreSame(handle, host.ResolveRef(handle));
        Assert.IsEmpty(resolver.Asked);
    }

    [TestMethod]
    public void ResolveRefFailsWithoutAResolverOrAValidVersion()
    {
        var withoutResolver = GameEventScriptHost.CreateBuilder().Build().StartForTest();
        var missing = Assert.ThrowsExactly<GesHandleException>(() => withoutResolver.ResolveRef(GesHandle.Parse("@Drone:7~#live")));
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, missing.Code);
        Assert.AreEqual("@Drone:7~#live", missing.Symbol);

        var invalid = GameEventScriptHost.CreateBuilder().WithHandleResolver(DroneType, new RefResolver(GesValue.GesText("12"))).Build().StartForTest();
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, Assert.ThrowsExactly<GesHandleException>(() => invalid.ResolveRef(GesHandle.Parse("@Drone:7~#live"))).Code);

        var failing = GameEventScriptHost.CreateBuilder().WithHandleResolver(DroneType, new RefResolver(default, fail: true)).Build().StartForTest();
        var failed = Assert.ThrowsExactly<GesHandleException>(() => failing.ResolveRef(GesHandle.Parse("@Drone:7~#live")));
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, failed.Code);
        Assert.IsInstanceOfType<InvalidOperationException>(failed.InnerException);
    }

    private sealed class RefResolver(GesValue version, bool fail = false) : IGesHandleResolver
    {
        public List<string> Asked { get; } = [];

        public void Resolve(GesHandleResolveCall call) => call.SetMissing();

        public GesValue ResolveRef(GesHandle handle)
        {
            Asked.Add(handle.ToString());
            return fail ? throw new InvalidOperationException("The ref is unknown.") : version;
        }
    }
}
