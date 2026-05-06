using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptExternalTypeTests
{
    [TestMethod]
    public void ExternalTypeConstructorIsPortableAndBoundByHost()
    {
        var registry = GameEventScriptExternalTypeRegistry.Create(typeof(AimValue));
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

        Assert.HasCount(1, bytecode.ExternalTypeConstructorReferences);
        Assert.AreEqual("aim(bearing,direction,range,steps)", bytecode.ExternalTypeConstructorReferences[0].SignatureId);

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypes(registry)
            .Build()
            .Load(bytecode)
            .Subscribe("Done", ["isAim", "bearing", "range", "steps", "directionZ", "checksum"], (message, _) => received.Add(message));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));
        Assert.HasCount(1, received);
        Assert.IsTrue(received[0].Arguments["isAim"].AsBoolean());
        Assert.AreEqual(90d, received[0].Arguments["bearing"].AsNumber());
        Assert.IsTrue(received[0].Arguments["bearing"].IsNumericUnit(GameEventScriptNumericUnit.Degree));
        Assert.AreEqual(12d, received[0].Arguments["range"].AsNumber());
        Assert.IsTrue(received[0].Arguments["range"].IsNumericUnit(GameEventScriptNumericUnit.Meter));
        Assert.AreEqual(4, received[0].Arguments["steps"].AsInteger());
        Assert.IsTrue(received[0].Arguments["steps"].IsNumericUnit(GameEventScriptNumericUnit.Meter));
        Assert.AreEqual(3d, received[0].Arguments["directionZ"].AsNumber());
        Assert.IsTrue(received[0].Arguments["directionZ"].IsNumericUnit(GameEventScriptNumericUnit.Meter));
        Assert.AreEqual(106, received[0].Arguments["checksum"].AsInteger());
    }

    [TestMethod]
    public void ExternalTypeConstructorRequiresMatchingHostRegistry()
    {
        var registry = GameEventScriptExternalTypeRegistry.Create(typeof(AimValue));
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
                .Load(bytecode));
    }

    [TestMethod]
    public void ExternalTypeConstructorShapeMustBeRegistered()
    {
        var registry = GameEventScriptExternalTypeRegistry.Create(typeof(AimValue));

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
        var registry = GameEventScriptExternalTypeRegistry.Create(typeof(AimValue));
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
            .WithRegistry(GameEventScriptExtensionRegistry.Create(typeof(AimExtensionFunctions)))
            .Build()
            .Load(bytecode)
            .Subscribe("Done", ["score", "lead", "distance", "integerDistance"], (message, _) => received.Add(message));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));
        Assert.HasCount(1, received);
        Assert.AreEqual(106, received[0].Arguments["score"].AsInteger());
        Assert.AreEqual(95d, received[0].Arguments["lead"].AsNumber());
        Assert.IsTrue(received[0].Arguments["lead"].IsNumericUnit(GameEventScriptNumericUnit.Degree));
        Assert.AreEqual(12d, received[0].Arguments["distance"].AsNumber());
        Assert.IsTrue(received[0].Arguments["distance"].IsNumericUnit(GameEventScriptNumericUnit.Meter));
        Assert.AreEqual(13, received[0].Arguments["integerDistance"].AsInteger());
        Assert.IsTrue(received[0].Arguments["integerDistance"].IsNumericUnit(GameEventScriptNumericUnit.Meter));
    }

    [TestMethod]
    public void AnnotatedExtensionsRejectBoxedValueParameters()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            GameEventScriptExtensionRegistry.Create(typeof(BoxedExtensionFunctions)));

        StringAssert.Contains(exception.Message, "cannot use boxed GameEventScriptValue");
    }

    [GesType("aim")]
    private sealed class AimValue
    {
        [GesConstruct]
        public AimValue(
            [GesParam("bearing", GameEventScriptValueKind.Float, GameEventScriptNumericUnit.Degree)] double bearing,
            [GesParam("range", GameEventScriptValueKind.Float, GameEventScriptNumericUnit.Meter)] double range,
            [GesParam("steps", GameEventScriptValueKind.Integer, GameEventScriptNumericUnit.Meter)] int steps,
            [GesParam("direction", GameEventScriptValueKind.Vector, GameEventScriptNumericUnit.Meter)] GameEventScriptVectorValue direction)
        {
            Bearing = bearing;
            Range = range;
            Steps = steps;
            Direction = direction;
            Checksum = (int)(bearing + range + steps);
        }

        [GesField("bearing", GameEventScriptValueKind.Float, GameEventScriptNumericUnit.Degree)]
        public double Bearing { get; }

        [GesField("range", GameEventScriptValueKind.Float, GameEventScriptNumericUnit.Meter)]
        public double Range { get; }

        [GesField("steps", GameEventScriptValueKind.Integer, GameEventScriptNumericUnit.Meter)]
        public int Steps { get; }

        [GesField("direction", GameEventScriptValueKind.Vector, GameEventScriptNumericUnit.Meter)]
        public GameEventScriptVectorValue Direction { get; }

        [GesField("checksum", GameEventScriptValueKind.Integer)]
        public int Checksum { get; }
    }

    [GesExtension("aim")]
    private static class AimExtensionFunctions
    {
        [GesFunction("score", GameEventScriptValueKind.Integer)]
        public static long Score([GesParam("_", "aim")] AimValue aim) => aim.Checksum;

        [GesFunction("lead", GameEventScriptValueKind.Float, GameEventScriptNumericUnit.Degree)]
        public static double Lead([GesParam("heading", GameEventScriptValueKind.Float, GameEventScriptNumericUnit.Degree)] double heading)
            => heading + 5d;

        [GesFunction("distance")]
        public static (double, GameEventScriptNumericUnit) Distance([GesParam("value", GameEventScriptValueKind.Float)] double value)
            => (value, GameEventScriptNumericUnit.Meter);

        [GesFunction("integerDistance", GameEventScriptValueKind.Integer, GameEventScriptNumericUnit.Meter)]
        public static long IntegerDistance([GesParam("value", GameEventScriptValueKind.Integer, GameEventScriptNumericUnit.Meter)] long value)
            => value + 1;
    }

    [GesExtension("boxed")]
    private static class BoxedExtensionFunctions
    {
        [GesFunction("value")]
        public static GameEventScriptFastValue Value([GesParam("_", GameEventScriptValueKind.Float)] GameEventScriptValue value)
            => GameEventScriptFastValue.FromGameEventScriptValue(value);
    }
}
