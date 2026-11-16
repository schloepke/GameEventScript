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

    [GesExtension("strict")]
    private static class StrictExtensions
    {
        [GesFunction("whole")]
        public static long Whole([GesParam("_", GesBytecodeTypeKind.Float)] long value) => value;

        [GesFunction("real")]
        public static double Real([GesParam("_", GesBytecodeTypeKind.Float)] double value) => value;

        [GesFunction("truth")]
        public static bool Truth([GesParam("_", GesBytecodeTypeKind.Boolean)] bool value) => value;

        [GesFunction("maybe")]
        public static string Maybe([GesParam("_", GesBytecodeTypeKind.Float)] long? value) => value is { } present ? "value " + present : "none";
    }

    // A GesRandomGenerator parameter receives the call's generator in any position and is no script argument.
    [GesExtension("arena")]
    private static class ArenaExtensions
    {
        [GesFunction("first")]
        public static string First(GesRandomGenerator random, [GesParam("count", GesBytecodeTypeKind.Float)] int count) => Draw(random, count, 0);

        [GesFunction("middle")]
        public static string Middle([GesParam("count", GesBytecodeTypeKind.Float)] int count, GesRandomGenerator random, [GesParam("_", GesBytecodeTypeKind.Float)] double offset)
            => Draw(random, count, offset);

        [GesFunction("spawnSpots")]
        public static string SpawnSpots(
            [GesParam("count", GesBytecodeTypeKind.Float)] int count,
            [GesParam("margin", GesBytecodeTypeKind.Float)] double margin,
            [GesParam("spacing", GesBytecodeTypeKind.Float)] double spacing,
            GesRandomGenerator random)
            => Draw(random, count, margin + spacing);

        [GesFunction("only")]
        public static long Only(GesRandomGenerator random) => random.NextInclusiveInteger(1, 1000);

        [GesFunction("withCall")]
        public static bool WithCall(GesExtensionCall call, [GesParam("_", GesBytecodeTypeKind.Float)] int a, [GesParam("_", GesBytecodeTypeKind.Float)] int b, [GesParam("_", GesBytecodeTypeKind.Float)] int c, GesRandomGenerator random, [GesParam("_", GesBytecodeTypeKind.Float)] int d)
            => ReferenceEquals(call.Random, random) && a + b + c + d == 10;

        public static string Draw(GesRandomGenerator random, int count, double offset)
            => string.Join(",", Enumerable.Range(0, count).Select(_ => (random.NextInclusiveInteger(1, 1000) + offset).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [GesExtension("twice")]
    private static class TwoGeneratorsExtensions
    {
        [GesFunction("draw")]
        public static long Draw(GesRandomGenerator first, GesRandomGenerator second) => first.NextInclusiveInteger(1, 6) + second.NextInclusiveInteger(1, 6);
    }

    [GesExtension("described")]
    private static class DescribedGeneratorExtensions
    {
        [GesFunction("draw")]
        public static long Draw([GesParam("random", "Value")] GesRandomGenerator random) => random.NextInclusiveInteger(1, 6);
    }

    [GesExtension("randomPredicate")]
    private static class RandomPredicateExtensions
    {
        [GesPredicate("lucky")]
        public static bool Lucky([GesParam("_", GesBytecodeTypeKind.Float)] double value, GesRandomGenerator random) => random.NextFloat(0, 1) < value;
    }

    [GesExtension("wide")]
    private static class WideExtensions
    {
        [GesFunction("four")]
        public static long Four(
            [GesParam("a", GesBytecodeTypeKind.Float)] long a,
            [GesParam("b", GesBytecodeTypeKind.Float)] long b,
            GesRandomGenerator random,
            [GesParam("c", GesBytecodeTypeKind.Float)] long c,
            [GesParam("d", GesBytecodeTypeKind.Float)] long d) => 1000 * a + 100 * b + 10 * c + d + random.NextInclusiveInteger(0, 0);
    }

    private static (GameEventScriptMessage? Seen, GesExecutionResult Result) Run(string body, IGesExtensionRegistry registry, long? seed = null)
    {
        var program = new GesProgramBuilder().AddScript("on Start() {\n" + body + "\n}").Compile();
        var builder = GameEventScriptHost.CreateBuilder().WithExtensionRegistry(registry);
        if (seed is { } value) builder.WithRandomSeed(value);
        var host = builder.Build().StartForTest();
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
    public void ArgumentsConvertOnlyExactly()
    {
        var registry = GesBridgeExtensionRegistry.Create(typeof(StrictExtensions));
        var (rejected, _) = Run("emit Seen(a: :strict.whole(2.7), b: :strict.whole(true), c: :strict.real(50%), d: :strict.truth(5))", registry);
        for (var index = 0; index < 4; index++) Assert.IsTrue(rejected!.Arguments[index].IsNothing, "A fraction, a Boolean, a Percentage and a number do not convert.");
        var (accepted, _) = Run("emit Seen(a: :strict.whole(3.0), b: :strict.real(2), c: :strict.maybe(nothing), d: :strict.maybe(4))", registry);
        Assert.AreEqual(3L, accepted!.Arguments[0].AsInteger());
        Assert.AreEqual(2d, accepted.Arguments[1].AsNumber());
        Assert.AreEqual("none", accepted.Arguments[2].AsText(), "Nothing reaches a nullable parameter as null.");
        Assert.AreEqual("value 4", accepted.Arguments[3].AsText());
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

    [TestMethod]
    public void RandomGeneratorParametersDrawFromTheHostGenerator()
    {
        var registry = GesBridgeExtensionRegistry.Create(typeof(ArenaExtensions));
        var (seen, result) = Run(
            "emit Seen(a: :arena.first(count: 2), b: :arena.middle(count: 3, 0.5), c: :arena.spawnSpots(count: 2, margin: 1, spacing: 2), d: :arena.only())",
            registry,
            seed: 42);
        Assert.AreEqual(GesExecutionState.Completed, result.State);
        var expected = GesRandomGenerator.FromSeed(42L);
        Assert.AreEqual(ArenaExtensions.Draw(expected, 2, 0), seen!.Arguments[0].AsText(), "The first parameter receives the host generator.");
        Assert.AreEqual(ArenaExtensions.Draw(expected, 3, 0.5), seen.Arguments[1].AsText(), "A middle parameter receives the host generator.");
        Assert.AreEqual(ArenaExtensions.Draw(expected, 2, 3), seen.Arguments[2].AsText(), "The last parameter receives the host generator.");
        Assert.AreEqual(expected.NextInclusiveInteger(1, 1000), seen.Arguments[3].AsInteger(), "A generator alone leaves no script parameter.");
        var (again, _) = Run("emit Seen(a: :arena.first(count: 2), b: :arena.middle(count: 3, 0.5), c: :arena.spawnSpots(count: 2, margin: 1, spacing: 2), d: :arena.only())", registry, seed: 42);
        for (var index = 0; index < 4; index++) Assert.AreEqual(seen.Arguments[index], again!.Arguments[index], "The same seed draws the same numbers.");
    }

    [TestMethod]
    public void RandomGeneratorParametersFollowRandomScopes()
    {
        var registry = GesBridgeExtensionRegistry.Create(typeof(ArenaExtensions));
        var (seen, result) = Run(
            "emit Seen(a: :arena.first(count: 2), b: random with 7 (:arena.first(count: 2)), c: :arena.first(count: 2), d: nothing)",
            registry,
            seed: 42);
        Assert.AreEqual(GesExecutionState.Completed, result.State);
        var outer = GesRandomGenerator.FromSeed(42L);
        Assert.AreEqual(ArenaExtensions.Draw(outer, 2, 0), seen!.Arguments[0].AsText());
        Assert.AreEqual(ArenaExtensions.Draw(GesRandomGenerator.FromSeed(7L), 2, 0), seen.Arguments[1].AsText(), "A seeded scope supplies its stream.");
        Assert.AreEqual(ArenaExtensions.Draw(outer, 2, 0), seen.Arguments[2].AsText(), "The outer stream continues after the scope.");
    }

    [TestMethod]
    public void RandomGeneratorParametersAreNoScriptArguments()
    {
        var registry = GesBridgeExtensionRegistry.Create(typeof(ArenaExtensions), typeof(WideExtensions));
        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("arena", "spawnSpots", ["count", "margin", "spacing"])));
        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("arena", "first", ["count"])));
        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("arena", "middle", ["count", GesMessageSignature.UnlabeledParameterName])));
        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("arena", "only", [])));
        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("wide", "four", ["a", "b", "c", "d"])), "The four-parameter limit counts only script parameters.");
        Assert.IsNull(registry.Resolve(new GesExtensionReference("arena", "spawnSpots", ["count", "margin", "spacing", "random"])));
        var (seen, result) = Run(
            "emit Seen(a: :wide.four(a: 1, b: 2, c: 3, d: 4), b: :arena.withCall(1, 2, 3, 4), c: :arena.spawnSpots(count: 1, margin: 1m, spacing: 2), d: :arena.first(count: 'x'))",
            GesBridgeExtensionRegistry.Create(typeof(ArenaExtensions), typeof(WideExtensions)),
            seed: 1);
        Assert.AreEqual(GesExecutionState.Completed, result.State);
        Assert.AreEqual(1234L, seen!.Arguments[0].AsInteger(), "Script arguments around the generator keep their order.");
        Assert.IsTrue(seen.Arguments[1].AsBoolean(), "A GesExtensionCall and a generator parameter can be combined.");
        Assert.IsTrue(seen.Arguments[2].IsNothing, "An argument after the generator converts strictly.");
        Assert.IsTrue(seen.Arguments[3].IsNothing);
    }

    [TestMethod]
    public void InvalidRandomGeneratorParametersFailRegistration()
    {
        var two = Assert.ThrowsExactly<ArgumentException>(() => GesBridgeExtensionRegistry.Create(typeof(TwoGeneratorsExtensions)));
        StringAssert.Contains(two.Message, "declares more than one GesRandomGenerator parameter");
        var described = Assert.ThrowsExactly<ArgumentException>(() => GesBridgeExtensionRegistry.Create(typeof(DescribedGeneratorExtensions)));
        StringAssert.Contains(described.Message, "receives the call's random generator and must not declare GesParamAttribute");
        var predicate = Assert.ThrowsExactly<ArgumentException>(() => GesBridgeExtensionRegistry.Create(typeof(RandomPredicateExtensions)));
        StringAssert.Contains(predicate.Message, "requires one parameter and a bool or bool? result");
    }
}
