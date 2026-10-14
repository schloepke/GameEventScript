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
            .WithRegistry(GesBridgeExtensionRegistry.Create(typeof(AimExtensionFunctions)))
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
