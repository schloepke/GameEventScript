// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Api;

// Refs never enter the VM (specs/HostRuntime.md#handles). Markdown Conformance covers Receive and the answers of
// ResolveRef (runtime/handles.md); these tests keep what it cannot reach: native Emit and Publish, native values nested
// beyond any script depth, the default ResolveRef, a host without a resolver, identity and the preserved inner exception.
[TestClass]
public sealed class GesHandleRefTests
{
    private const string Schemas = "schema :DroneState as { altitude: :Number integer between 0 and 1000 }\n"
        + "handle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\n";

    private static readonly GesHandle Live = GesHandle.Parse("@Drone:7~#live");

    private static GesHandleType DroneType => GesSchemaSet.Parse([new GesSchemaSource("drones.gess", Schemas)]).HandleTypes[0];

    private static GameEventScriptMessage WithRef(string name)
        => GameEventScriptMessage.Create(name, [new GesMessageArgument("drone", GesValue.GesHandle(Live))]);

    private static GameEventScriptHost Host(IGesHandleResolver resolver)
        => GameEventScriptHost.CreateBuilder().WithHandleResolver(DroneType, resolver).Build().StartForTest();

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
    public void ReceiveRejectsARefAtAnyNestingDepth()
    {
        var host = GameEventScriptHost.CreateBuilder().Build().StartForTest();

        // Levels cycle through List, Map, Record and Message, so the walk crosses every container kind on both sides
        // of its recursion limit.
        GesValue Nested(GesValue inner)
        {
            for (var level = 0; level < 1000; level++)
            {
                inner = (level % 4) switch
                {
                    0 => GesValue.GesList([GesValue.GesInteger(level), inner]),
                    1 => GesValue.GesMap(("a", GesValue.GesInteger(level)), ("b", inner)),
                    2 => GesValue.GesRecord("Level", ("value", inner)),
                    _ => GesValue.GesMessage(GameEventScriptMessage.Create("Level", [new GesMessageArgument("value", inner)])),
                };
            }

            return inner;
        }

        GameEventScriptMessage Deep(GesValue inner) => GameEventScriptMessage.Create("Deep", [new GesMessageArgument("value", Nested(inner))]);
        GesValue KeyedBy(GesHandle key) => GesValue.GesMapOfKeys([GesValue.GesHandle(key)], [GesValue.GesInteger(1)]);
        var versioned = GesHandle.Parse("@Drone:7~3");

        Assert.AreEqual(GesWireException.UnresolvedRef, Assert.ThrowsExactly<GesWireException>(() => host.Receive(Deep(GesValue.GesHandle(Live)))).Code);
        Assert.AreEqual(GesWireException.UnresolvedRef, Assert.ThrowsExactly<GesWireException>(() => host.Receive(Deep(KeyedBy(Live)))).Code);
        host.Receive(Deep(GesValue.GesHandle(versioned)));
        host.Receive(Deep(KeyedBy(versioned)));
        host.Receive(Deep(GesValue.GesInteger(1)));
    }

    [TestMethod]
    public void ResolveRefAsksOnlyForRefsAndReturnsNullForAnUnknownRef()
    {
        var resolver = new RefResolver(GesValue.GesInteger(12));
        var host = Host(resolver);
        var plain = GesHandle.Parse("@Drone:7~3");

        Assert.AreEqual("@Drone:7~12", host.ResolveRef(Live)?.ToString());
        Assert.AreSame(plain, host.ResolveRef(plain));
        CollectionAssert.AreEqual(new[] { "@Drone:7~#live" }, resolver.Asked);
        Assert.IsNull(Host(new RefResolver(GesValue.GesNothing())).ResolveRef(Live));
    }

    [TestMethod]
    public void ResolveRefFailsWithoutAResolverOrRefSupport()
    {
        var withoutResolver = GameEventScriptHost.CreateBuilder().Build().StartForTest();
        var missing = Assert.ThrowsExactly<GesHandleException>(() => withoutResolver.ResolveRef(Live));
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, missing.Code);
        Assert.AreEqual("@Drone:7~#live", missing.Symbol);

        // A resolver of a type without refs need not implement ResolveRef; the default fails like a throwing resolver.
        var stateOnly = Assert.ThrowsExactly<GesHandleException>(() => Host(new StateOnlyResolver()).ResolveRef(Live));
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, stateOnly.Code);

        var failed = Assert.ThrowsExactly<GesHandleException>(() => Host(new RefResolver(default, fail: true)).ResolveRef(Live));
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, failed.Code);
        Assert.IsInstanceOfType<InvalidOperationException>(failed.InnerException);
    }

    private sealed class StateOnlyResolver : IGesHandleResolver
    {
        public void Resolve(GesHandleResolveCall call) => call.SetMissing();
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
