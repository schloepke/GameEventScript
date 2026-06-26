using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

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
            .WithExternalTypes(registry)
            .AddScript(script)
            .Compile();

        // FIXME: Fix the test here
        //Assert.HasCount(1, bytecode.ExternalTypeConstructorReferences);
        //Assert.AreEqual("aim(bearing,direction,range,steps)", bytecode.ExternalTypeConstructorReferences[0].SignatureId);

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypes(registry)
            .Build()
            .Load(GameEventScriptManager.CreateModule(bytecode))
            .Subscribe("Done", ["isAim", "bearing", "range", "steps", "directionZ", "checksum"], (message, _) => received.Add(message));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));
        Assert.HasCount(1, received);
        Assert.IsTrue(received[0].Arguments["isAim"].AsBoolean());
        Assert.AreEqual(90d, received[0].Arguments["bearing"].AsNumber());
        Assert.IsTrue(received[0].Arguments["bearing"].IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitDegree));
        Assert.AreEqual(12d, received[0].Arguments["range"].AsNumber());
        Assert.IsTrue(received[0].Arguments["range"].IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitMeter));
        Assert.AreEqual(4, received[0].Arguments["steps"].AsInteger());
        Assert.IsTrue(received[0].Arguments["steps"].IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitMeter));
        Assert.AreEqual(3d, received[0].Arguments["directionZ"].AsNumber());
        Assert.IsTrue(received[0].Arguments["directionZ"].IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitMeter));
        Assert.AreEqual(106, received[0].Arguments["checksum"].AsInteger());
    }

    [TestMethod]
    public void ExternalTypeConstructorRequiresMatchingHostRegistry()
    {
        var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(AimValue));
        var bytecode = GameEventScriptBuilder.Create()
            .WithExternalTypes(registry)
            .AddScript(
                """
                on Start {
                  let aim be :aim(bearing: 45°, range: 3m, steps: 1m, direction: :vector(1m, 0m, 0m))
                  emit Done(value: aim.bearing)
                }
                """)
            .Compile();

        Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() =>
            GameEventScriptHost.CreateBuilder()
                .Build()
                .Load(GameEventScriptManager.CreateModule(bytecode)));
    }

    [TestMethod]
    public void ExternalTypeConstructorShapeMustBeRegistered()
    {
        var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(AimValue));

        var exception = Assert.ThrowsExactly<GameEventScriptCompileException>(() =>
            GameEventScriptBuilder.Create()
                .WithExternalTypes(registry)
                .AddScript(
                    """
                    on Start {
                      let aim be :aim(bearing: 45°)
                      emit Done(value: aim)
                    }
                    """)
                .Compile());

        StringAssert.Contains(exception.Message, "aim(bearing)");
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
            .WithExternalTypes(registry)
            .AddScript(script)
            .Compile();

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypes(registry)
            .WithRegistry(GameEventScriptCSharpExtensions.CreateRegistry(typeof(AimExtensionFunctions)))
            .Build()
            .Load(GameEventScriptManager.CreateModule(bytecode))
            .Subscribe("Done", ["score", "lead", "distance", "integerDistance"], (message, _) => received.Add(message));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));
        Assert.HasCount(1, received);
        Assert.AreEqual(106, received[0].Arguments["score"].AsInteger());
        Assert.AreEqual(95d, received[0].Arguments["lead"].AsNumber());
        Assert.IsTrue(received[0].Arguments["lead"].IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitDegree));
        Assert.AreEqual(12d, received[0].Arguments["distance"].AsNumber());
        Assert.IsTrue(received[0].Arguments["distance"].IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitMeter));
        Assert.AreEqual(13, received[0].Arguments["integerDistance"].AsInteger());
        Assert.IsTrue(received[0].Arguments["integerDistance"].IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitMeter));
    }

    [TestMethod]
    public void AnnotatedExtensionsAllowValueParameters()
    {
        var registry = GameEventScriptCSharpExtensions.CreateRegistry(typeof(ValueExtensionFunctions));

        Assert.IsTrue(registry.TryResolve(new GameEventScriptExtensionReference("value", "value", [GameEventScriptMessageSignature.UnlabeledParameterName]), out _));
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

        Assert.IsTrue(baseRegistry.TryResolve(reference, out var baseFunction));
        Assert.AreEqual(1, baseFunction.Invoke(null!, ReadOnlySpan<GameEventScriptValue>.Empty).AsInteger());
        Assert.IsTrue(extendedRegistry.TryResolve(reference, out var extendedFunction));
        Assert.AreEqual(2, extendedFunction.Invoke(null!, ReadOnlySpan<GameEventScriptValue>.Empty).AsInteger());
    }

    [GesType("aim")]
    private sealed class AimValue
    {
        [GesConstruct]
        public AimValue(
            [GesParam("bearing", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree)] double bearing,
            [GesParam("range", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)] double range,
            [GesParam("steps", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)] int steps,
            [GesParam("direction", GameEventScriptBytecodeTypeKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)] GameEventScriptValue direction)
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
        public GameEventScriptValue Direction { get; }

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
        public static GameEventScriptValue Value([GesParam("_", GameEventScriptBytecodeTypeKind.Float)] GameEventScriptValue value)
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
