// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptMessage;

namespace GameEventScript.Tests.Native.CSharpBridge;

/// <summary>Attributed extensions follow the same rules as the Swift macros: the same declarations give the same signatures,
/// conversions, units and failure results.</summary>
[TestClass]
public sealed class GesBridgeExtensionAttributeTests
{
    [GesExtension("navigation")]
    private static class NavigationExtensions
    {
        [GesFunction("destination", GesQuantityUnit.UnitMeter)]
        public static GesPoint? Destination(
            [GesParam("from", GesBytecodeTypeKind.Point, GesQuantityUnit.UnitMeter)] GesPoint from,
            [GesParam("bearing", GesBytecodeTypeKind.Float)] double bearing,
            [GesParam("distance", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)] double distance)
        {
            // East for 90°, otherwise north: enough to show the conversions.
            var point = from + (bearing == 90 ? new GesVector(distance, 0) : new GesVector(0, distance));
            return point.IsFinite ? point : null;
        }

        [GesFunction("gap")]
        public static double Gap([GesParam("_", GesBytecodeTypeKind.Point, GesQuantityUnit.UnitMeter)] GesPoint a, [GesParam("_", GesBytecodeTypeKind.Point, GesQuantityUnit.UnitMeter)] GesPoint b)
            => a.DistanceTo(b);

        [GesPredicate("isFar")]
        public static bool? IsFar([GesParam("value", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)] double value) => double.IsFinite(value) ? value > 100 : null;

        [GesFunction("kind")]
        public static string Kind([GesParam("_", "Value")] GesValue value) => value.ValueKind.ToString();

        [GesFunction("fail")]
        public static double Fail() => throw new InvalidOperationException("deliberate");
    }

    [GesExtension("game")]
    private sealed class GameExtensions
    {
        public int Damage { get; private set; }

        [GesFunction("damage")]
        public int AddDamage([GesParam("_", GesBytecodeTypeKind.Float)] int amount) => Damage += amount;

        [GesFunction("twice")]
        public static int Twice([GesParam("_", GesBytecodeTypeKind.Float)] int value) => value * 2;
    }

    private static (GameEventScriptMessage? Seen, GesExecutionResult Result) Run(string body, IGesExtensionRegistry registry)
    {
        var program = new GesProgramBuilder().AddScript("on Start() {\n" + body + "\n}").Compile();
        var host = GameEventScriptHost.CreateBuilder().WithRegistry(registry).Build().StartForTest();
        host.Load(program);
        GameEventScriptMessage? seen = null;
        host.Subscribe("Seen", ["a", "b", "c", "d"], (message, _) => seen = message);
        Assert.IsTrue(host.Receive(Create("Start")));
        var result = host.RunToCompletion();
        return (seen, result);
    }

    [TestMethod]
    public void SignaturesConversionsAndUnits()
    {
        var registry = GesBridgeExtensionRegistry.Create(typeof(NavigationExtensions));
        var (seen, result) = Run(
            "emit Seen(a: :navigation.destination(from: :Point(1m, 2m), bearing: 90, distance: 10m), b: :navigation.gap(:Point(0m, 0m), :Point(3m, 4m)), c: :navigation.destination(from: :Point(1, 2), bearing: 90, distance: 10m), d: 150m is :navigation.isFar)",
            registry);
        Assert.AreEqual(GesExecutionState.Completed, result.State);
        var arguments = seen!.Arguments;
        Assert.AreEqual(new GesPoint(11, 2), GesPoint.FromValue(arguments[0], GesQuantityUnit.UnitMeter), "The declared result unit is attached.");
        Assert.AreEqual(5d, arguments[1].AsNumber());
        Assert.IsTrue(arguments[2].IsNothing, "A point of another unit converts to nothing.");
        Assert.IsTrue(arguments[3].AsBoolean());
        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("navigation", "isFar", [GesMessageSignature.UnlabeledParameterName])), "A predicate takes one unlabeled argument.");
        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("navigation", "destination", ["from", "bearing", "distance"])));
    }

    [TestMethod]
    public void RawValuesAndFaults()
    {
        var registry = GesBridgeExtensionRegistry.Create(typeof(NavigationExtensions));
        var (seen, _) = Run("emit Seen(a: :navigation.kind(#x), b: :navigation.gap(1, 2), c: 'x' is :navigation.isFar, d: nothing)", registry);
        Assert.AreEqual("Tag", seen!.Arguments[0].AsText());
        Assert.IsTrue(seen.Arguments[1].IsNothing);
        Assert.IsTrue(seen.Arguments[2].IsNothing);
        var (failed, result) = Run("emit Seen(a: :navigation.fail(), b: 1, c: 2, d: 3)", registry);
        Assert.IsNull(failed);
        Assert.AreEqual("runtime.extensionCallFailed", result.Diagnostic?.Code, "An exception stays an extension fault.");
    }

    [TestMethod]
    public void InstanceExtensionsRetainTheirInstance()
    {
        var game = new GameExtensions();
        var registry = GesBridgeExtensionRegistry.CreateBuilder().AddInstance(game).Build();
        var (seen, _) = Run("emit Seen(a: :game.damage(3), b: :game.damage(4), c: :game.twice(5), d: :game.damage(2m))", registry);
        Assert.AreEqual(3L, seen!.Arguments[0].AsInteger());
        Assert.AreEqual(7L, seen.Arguments[1].AsInteger());
        Assert.AreEqual(10L, seen.Arguments[2].AsInteger());
        Assert.IsTrue(seen.Arguments[3].IsNothing, "A number of another unit does not convert.");
        Assert.AreEqual(7, game.Damage);
        var statics = GesBridgeExtensionRegistry.Create(typeof(GameExtensions));
        Assert.IsNotNull(statics.Resolve(new GesExtensionReference("game", "twice", [GesMessageSignature.UnlabeledParameterName])));
        Assert.IsNull(statics.Resolve(new GesExtensionReference("game", "damage", [GesMessageSignature.UnlabeledParameterName])), "Instance methods need an instance.");
    }
}
