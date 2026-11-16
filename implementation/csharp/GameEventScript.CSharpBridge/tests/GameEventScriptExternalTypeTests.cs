// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptMessage;

namespace GameEventScript.Tests.Native.CSharpBridge;

[TestClass]
public sealed class GameEventScriptExternalTypeTests
{
    [TestMethod]
    public void ExternalTypeConstructorIsPortableAndBoundByHost()
    {
        var registry = GesBridgeExternalTypeRegistry.Create(typeof(AimValue));
        const string script =
            """
            on Start {
              let aim be :Aim(range: 12m, bearing: 90°, steps: 4m, direction: :Vector(1m, 2m, 3m))
              emit Done(
                isAim: aim is :Aim,
                bearing: aim.bearing,
                range: aim.range,
                steps: aim.steps,
                directionZ: aim.direction.z,
                checksum: aim.checksum)
            }
            """;

        var bytecode = new GesProgramBuilder()
            .WithExternalTypeCatalog(registry)
            .AddScript(script)
            .Compile();

        // FIXME: Fix the test here
        //Assert.HasCount(1, bytecode.ExternalTypeConstructorReferences);
        //Assert.AreEqual("aim(bearing,direction,range,steps)", bytecode.ExternalTypeConstructorReferences[0].SignatureId);

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypeRegistry(registry)
            .Build().StartForTest();
        host.Load(bytecode);
        host.Subscribe("Done", ["isAim", "bearing", "range", "steps", "directionZ", "checksum"], (message, _) => received.Add(message));

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.HasCount(1, received);
        var arguments = received[0].Arguments;
        Assert.IsTrue(arguments.GetAsBoolean("isAim"));
        Assert.AreEqual(90d, arguments.GetAsNumber("bearing"));
        Assert.AreEqual(GesQuantityUnit.UnitDegree, arguments.UnitAt(arguments.IndexOf("bearing")));
        Assert.AreEqual(12d, arguments.GetAsNumber("range"));
        Assert.AreEqual(GesQuantityUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("range")));
        Assert.AreEqual(4, arguments.GetAsInteger("steps"));
        Assert.AreEqual(GesQuantityUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("steps")));
        Assert.AreEqual(3d, arguments.GetAsNumber("directionZ"));
        Assert.AreEqual(GesQuantityUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("directionZ")));
        Assert.AreEqual(106, arguments.GetAsInteger("checksum"));
    }

    [TestMethod]
    public void ExtensionCanReadExternalClrObjectWithoutDictionaryMaterialization()
    {
        var registry = GesBridgeExternalTypeRegistry.Create(typeof(AimValue));
        const string script =
            """
            on Start {
              let aim be :Aim(range: 12m, bearing: 90°, steps: 4m, direction: :Vector(1m, 2m, 3m))
              emit Done(
                score: :aim.score aim,
                lead: :aim.lead heading: 90°,
                distance: :aim.distance value: 12,
                integerDistance: :aim.integerDistance value: 12m)
            }
            """;

        var bytecode = new GesProgramBuilder()
            .WithExternalTypeCatalog(registry)
            .AddScript(script)
            .Compile();

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypeRegistry(registry)
            .WithExtensionRegistry(GesBridgeExtensionRegistry.Create(typeof(AimExtensionFunctions)))
            .Build().StartForTest();
        host.Load(bytecode);
        host.Subscribe("Done", ["score", "lead", "distance", "integerDistance"], (message, _) => received.Add(message));

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.HasCount(1, received);
        var arguments = received[0].Arguments;
        Assert.AreEqual(106, arguments.GetAsInteger("score"));
        Assert.AreEqual(95d, arguments.GetAsNumber("lead"));
        Assert.AreEqual(GesQuantityUnit.UnitDegree, arguments.UnitAt(arguments.IndexOf("lead")));
        Assert.AreEqual(12d, arguments.GetAsNumber("distance"));
        Assert.AreEqual(GesQuantityUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("distance")));
        Assert.AreEqual(13, arguments.GetAsInteger("integerDistance"));
        Assert.AreEqual(GesQuantityUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("integerDistance")));
    }

    [TestMethod]
    public void AnnotatedExtensionsAllowValueParameters()
    {
        var registry = GesBridgeExtensionRegistry.Create(typeof(ValueExtensionFunctions));

        Assert.IsNotNull(registry.Resolve(new GesExtensionReference("value", "value", [GesMessageSignature.UnlabeledParameterName])));
    }

    [TestMethod]
    public void ExtensionRegistryBuilderCreatesImmutableOverlayRegistry()
    {
        var baseRegistry = GesBridgeExtensionRegistry
            .CreateBuilder()
            .Add(typeof(BaseOverlayExtensionFunctions))
            .Build();
        var extendedRegistry = GesBridgeExtensionRegistry
            .CreateBuilder(baseRegistry)
            .Add(typeof(LocalOverlayExtensionFunctions))
            .Build();
        var reference = new GesExtensionReference("overlay", "value", []);

        var baseFunction = baseRegistry.Resolve(reference);
        Assert.IsNotNull(baseFunction);
        var baseCall = new GesExtensionCall();
        baseFunction.Invoke(baseCall);
        Assert.AreEqual(1, baseCall.Result.AsInteger());
        var extendedFunction = extendedRegistry.Resolve(reference);
        Assert.IsNotNull(extendedFunction);
        var extendedCall = new GesExtensionCall();
        extendedFunction.Invoke(extendedCall);
        Assert.AreEqual(2, extendedCall.Result.AsInteger());
    }

    private static (GameEventScriptMessage? Done, GesExecutionResult Result) RunCrate(string arguments, string fields)
    {
        var registry = GesBridgeExternalTypeRegistry.Create(typeof(CrateValue));
        var program = new GesProgramBuilder()
            .WithExternalTypeCatalog(registry)
            .AddScript("on Start {\n  let crate be :Crate(" + arguments + ")\n  emit Done(" + fields + ")\n}")
            .Compile();
        var host = GameEventScriptHost.CreateBuilder().WithExternalTypeRegistry(registry).Build().StartForTest();
        host.Load(program);
        GameEventScriptMessage? done = null;
        host.Subscribe("Done", ["built", "count", "active", "weight"], (message, _) => done = message);
        Assert.IsTrue(host.Receive(Create("Start")));
        var result = host.RunToCompletion();
        return (done, result);
    }

    [TestMethod]
    public void ConstructorArgumentsConvertStrictly()
    {
        const string fields = "built: crate is :Crate, count: crate.count, active: crate.active, weight: crate.weight";
        var (done, result) = RunCrate("name: 'box', count: 3.0, active: true, weight: 7", fields);
        Assert.IsNotNull(done, result.Diagnostic?.Message);
        Assert.IsTrue(done.Arguments[0].AsBoolean());
        Assert.AreEqual(3L, done.Arguments[1].AsInteger(), "An integral Float converts to an int parameter.");
        Assert.IsTrue(done.Arguments[2].AsBoolean());
        Assert.AreEqual(7L, done.Arguments[3].AsInteger(), "A nullable parameter receives a present value.");

        // Nothing constructs nothing before the constructor is called.
        var (absent, _) = RunCrate("name: 'box', count: 3, active: true, weight: nothing", "built: crate is :Crate, count: 0, active: 0, weight: 0");
        Assert.IsFalse(absent!.Arguments[0].AsBoolean());

        // A fraction or an out-of-range number for an integer parameter is a fault of the call, as in Swift.
        foreach (var arguments in new[] { "name: 'box', count: 2.7, active: true, weight: 1", "name: 'box', count: 3, active: true, weight: 3000000000" })
        {
            var (failed, faulted) = RunCrate(arguments, fields);
            Assert.IsNull(failed, arguments);
            Assert.AreEqual("runtime.externalConstructorFailed", faulted.Diagnostic?.Code, arguments);
        }
    }

    [TestMethod]
    public void RegistrationRejectsMembersWithoutAConversion()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GesBridgeExternalTypeRegistry.Create(typeof(DecimalField)));
        Assert.ThrowsExactly<ArgumentException>(() => GesBridgeExternalTypeRegistry.Create(typeof(EnumParameter)));
        Assert.ThrowsExactly<ArgumentException>(() => GesBridgeExternalTypeRegistry.Create(typeof(Holder)), "The held type is not registered.");
        Assert.IsNotNull(GesBridgeExternalTypeRegistry.Create(typeof(Holder), typeof(CrateValue)).Resolve("Holder"));
    }

    [TestMethod]
    public void FactoriesMayReturnDerivedInstances()
    {
        var registry = GesBridgeExternalTypeRegistry.Create(typeof(ShapeValue));
        var program = new GesProgramBuilder().WithExternalTypeCatalog(registry).AddScript("on Start {\n  emit Done(side: :Shape(side: 2).side)\n}").Compile();
        var host = GameEventScriptHost.CreateBuilder().WithExternalTypeRegistry(registry).Build().StartForTest();
        host.Load(program);
        GameEventScriptMessage? done = null;
        host.Subscribe("Done", ["side"], (message, _) => done = message);
        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.AreEqual(2L, done!.Arguments[0].AsInteger());
    }

    [GesType("Crate")]
    private sealed class CrateValue
    {
        [GesConstruct]
        public CrateValue(
            [GesParam("name", GesBytecodeTypeKind.Text)] string name,
            [GesParam("count", GesBytecodeTypeKind.Float)] int count,
            [GesParam("active", GesBytecodeTypeKind.Boolean)] bool active,
            [GesParam("weight", GesBytecodeTypeKind.Float)] int? weight)
        {
            Name = name;
            Count = count;
            Active = active;
            Weight = weight;
        }

        [GesField("name", GesBytecodeTypeKind.Text)]
        public string Name { get; }

        [GesField("count", GesBytecodeTypeKind.Float)]
        public int Count { get; }

        [GesField("active", GesBytecodeTypeKind.Boolean)]
        public bool Active { get; }

        [GesField("weight", GesBytecodeTypeKind.Float)]
        public int? Weight { get; }
    }

    [GesType("DecimalField")]
    private sealed class DecimalField
    {
        [GesField("price", GesBytecodeTypeKind.Float)]
        public decimal Price { get; } = 1m;
    }

    [GesType("EnumParameter")]
    private sealed class EnumParameter
    {
        [GesConstruct]
        public EnumParameter([GesParam("mode", GesBytecodeTypeKind.Float)] DayOfWeek mode) => Mode = (int)mode;

        [GesField("mode", GesBytecodeTypeKind.Float)]
        public int Mode { get; }
    }

    [GesType("Holder")]
    private sealed class Holder
    {
        [GesField("crate", "Crate")]
        public CrateValue? Crate { get; }
    }

    [GesType("Shape")]
    private class ShapeValue
    {
        protected ShapeValue(long side) => Side = side;

        [GesField("side", GesBytecodeTypeKind.Float)]
        public long Side { get; }

        [GesConstruct]
        public static ShapeValue Square([GesParam("side", GesBytecodeTypeKind.Float)] long side) => new SquareValue(side);
    }

    private sealed class SquareValue(long side) : ShapeValue(side);

    [GesType("Aim")]
    private sealed class AimValue
    {
        [GesConstruct]
        public AimValue(
            [GesParam("bearing", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitDegree)] double bearing,
            [GesParam("range", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)] double range,
            [GesParam("steps", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)] int steps,
            [GesParam("direction", GesBytecodeTypeKind.Vector, GesQuantityUnit.UnitMeter)] GesValue direction)
        {
            Bearing = bearing;
            Range = range;
            Steps = steps;
            Direction = direction;
            Checksum = (int)(bearing + range + steps);
        }

        [GesField("bearing", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitDegree)]
        public double Bearing { get; }

        [GesField("range", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)]
        public double Range { get; }

        [GesField("steps", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)]
        public int Steps { get; }

        [GesField("direction", GesBytecodeTypeKind.Vector, GesQuantityUnit.UnitMeter)]
        public GesValue Direction { get; }

        [GesField("checksum", GesBytecodeTypeKind.Float)]
        public int Checksum { get; }
    }

    [GesExtension("aim")]
    private static class AimExtensionFunctions
    {
        [GesFunction("score", GesBytecodeTypeKind.Float)]
        public static long Score([GesParam("_", "Aim")] AimValue aim) => aim.Checksum;

        [GesFunction("lead", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitDegree)]
        public static double Lead([GesParam("heading", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitDegree)] double heading)
            => heading + 5d;

        [GesFunction("distance")]
        public static (double, GesQuantityUnit) Distance([GesParam("value", GesBytecodeTypeKind.Float)] double value)
            => (value, GesQuantityUnit.UnitMeter);

        [GesFunction("integerDistance", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)]
        public static long IntegerDistance([GesParam("value", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter)] long value)
            => value + 1;
    }

    [GesExtension("value")]
    private static class ValueExtensionFunctions
    {
        [GesFunction("value")]
        public static GesValue Value([GesParam("_", GesBytecodeTypeKind.Float)] GesValue value)
            => value;
    }

    [GesExtension("overlay")]
    private static class BaseOverlayExtensionFunctions
    {
        [GesFunction("value", GesBytecodeTypeKind.Float)]
        public static long Value() => 1;
    }

    [GesExtension("overlay")]
    private static class LocalOverlayExtensionFunctions
    {
        [GesFunction("value", GesBytecodeTypeKind.Float)]
        public static long Value() => 2;
    }

}
