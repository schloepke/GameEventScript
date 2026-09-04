// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.Native.CSharpBridge;

[TestClass]
public sealed class GameEventScriptExternalTypeTests
{
    [TestMethod]
    public void ExternalTypeConstructorIsPortableAndBoundByHost()
    {
        var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(AimValue));
        const string script =
            """
            on Start {
              let aim be :aim(range: 12m, bearing: 90°, steps: 4m, direction: :vector(1m, 2m, 3m))
              emit Done(
                isAim: aim is :aim,
                bearing: aim.bearing,
                range: aim.range,
                steps: aim.steps,
                directionZ: aim.direction.z,
                checksum: aim.checksum)
            }
            """;

        var bytecode = GameEventScriptBuilder.Create()
            .WithExternalTypeCatalog(registry)
            .AddScript(script)
            .Compile();

        // FIXME: Fix the test here
        //Assert.HasCount(1, bytecode.ExternalTypeConstructorReferences);
        //Assert.AreEqual("aim(bearing,direction,range,steps)", bytecode.ExternalTypeConstructorReferences[0].SignatureId);

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypeRegistry(registry)
            .Build();
        host.Load(bytecode);
        host.Subscribe("Done", ["isAim", "bearing", "range", "steps", "directionZ", "checksum"], (message, _) => received.Add(message));

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.HasCount(1, received);
        var arguments = received[0].Arguments;
        Assert.IsTrue(arguments.GetAsBoolean("isAim"));
        Assert.AreEqual(90d, arguments.GetAsNumber("bearing"));
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitDegree, arguments.UnitAt(arguments.IndexOf("bearing")));
        Assert.AreEqual(12d, arguments.GetAsNumber("range"));
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("range")));
        Assert.AreEqual(4, arguments.GetAsInteger("steps"));
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("steps")));
        Assert.AreEqual(3d, arguments.GetAsNumber("directionZ"));
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("directionZ")));
        Assert.AreEqual(106, arguments.GetAsInteger("checksum"));
    }

    [TestMethod]
    public void ExtensionCanReadExternalClrObjectWithoutDictionaryMaterialization()
    {
        var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(AimValue));
        const string script =
            """
            on Start {
              let aim be :aim(range: 12m, bearing: 90°, steps: 4m, direction: :vector(1m, 2m, 3m))
              emit Done(
                score: :aim.score aim,
                lead: :aim.lead heading: 90°,
                distance: :aim.distance value: 12,
                integerDistance: :aim.integerDistance value: 12m)
            }
            """;

        var bytecode = GameEventScriptBuilder.Create()
            .WithExternalTypeCatalog(registry)
            .AddScript(script)
            .Compile();

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypeRegistry(registry)
            .WithRegistry(GameEventScriptCSharpExtensions.CreateRegistry(typeof(AimExtensionFunctions)))
            .Build();
        host.Load(bytecode);
        host.Subscribe("Done", ["score", "lead", "distance", "integerDistance"], (message, _) => received.Add(message));

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.HasCount(1, received);
        var arguments = received[0].Arguments;
        Assert.AreEqual(106, arguments.GetAsInteger("score"));
        Assert.AreEqual(95d, arguments.GetAsNumber("lead"));
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitDegree, arguments.UnitAt(arguments.IndexOf("lead")));
        Assert.AreEqual(12d, arguments.GetAsNumber("distance"));
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("distance")));
        Assert.AreEqual(13, arguments.GetAsInteger("integerDistance"));
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, arguments.UnitAt(arguments.IndexOf("integerDistance")));
    }

    [TestMethod]
    public void AnnotatedExtensionsAllowValueParameters()
    {
        var registry = GameEventScriptCSharpExtensions.CreateRegistry(typeof(ValueExtensionFunctions));

        Assert.IsNotNull(registry.Resolve(new GameEventScriptExtensionReference("value", "value", [GameEventScriptMessageSignature.UnlabeledParameterName])));
    }

    [TestMethod]
    public void ExtensionRegistryBuilderCreatesImmutableOverlayRegistry()
    {
        var baseRegistry = GameEventScriptCSharpExtensions
            .CreateBuilder()
            .Add(typeof(BaseOverlayExtensionFunctions))
            .Build();
        var extendedRegistry = GameEventScriptCSharpExtensions
            .CreateBuilder(baseRegistry)
            .Add(typeof(LocalOverlayExtensionFunctions))
            .Build();
        var reference = new GameEventScriptExtensionReference("overlay", "value", []);

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

    [GesType("aim")]
    private sealed class AimValue
    {
        [GesConstruct]
        public AimValue(
            [GesParam("bearing", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree)] double bearing,
            [GesParam("range", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)] double range,
            [GesParam("steps", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)] int steps,
            [GesParam("direction", GameEventScriptBytecodeTypeKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)] GesValue direction)
        {
            Bearing = bearing;
            Range = range;
            Steps = steps;
            Direction = direction;
            Checksum = (int)(bearing + range + steps);
        }

        [GesField("bearing", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree)]
        public double Bearing { get; }

        [GesField("range", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public double Range { get; }

        [GesField("steps", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public int Steps { get; }

        [GesField("direction", GameEventScriptBytecodeTypeKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public GesValue Direction { get; }

        [GesField("checksum", GameEventScriptBytecodeTypeKind.Float)]
        public int Checksum { get; }
    }

    [GesExtension("aim")]
    private static class AimExtensionFunctions
    {
        [GesFunction("score", GameEventScriptBytecodeTypeKind.Float)]
        public static long Score([GesParam("_", "aim")] AimValue aim) => aim.Checksum;

        [GesFunction("lead", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree)]
        public static double Lead([GesParam("heading", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree)] double heading)
            => heading + 5d;

        [GesFunction("distance")]
        public static (double, GameEventScriptBytecodeInstructionUnit) Distance([GesParam("value", GameEventScriptBytecodeTypeKind.Float)] double value)
            => (value, GameEventScriptBytecodeInstructionUnit.UnitMeter);

        [GesFunction("integerDistance", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public static long IntegerDistance([GesParam("value", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)] long value)
            => value + 1;
    }

    [GesExtension("value")]
    private static class ValueExtensionFunctions
    {
        [GesFunction("value")]
        public static GesValue Value([GesParam("_", GameEventScriptBytecodeTypeKind.Float)] GesValue value)
            => value;
    }

    [GesExtension("overlay")]
    private static class BaseOverlayExtensionFunctions
    {
        [GesFunction("value", GameEventScriptBytecodeTypeKind.Float)]
        public static long Value() => 1;
    }

    [GesExtension("overlay")]
    private static class LocalOverlayExtensionFunctions
    {
        [GesFunction("value", GameEventScriptBytecodeTypeKind.Float)]
        public static long Value() => 2;
    }

}
