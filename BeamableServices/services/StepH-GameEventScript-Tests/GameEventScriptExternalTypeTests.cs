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
              let aim be :aim(range: 12m, bearing: 90°, direction: :vector(1m, 2m, 3m))
              emit Done(
                isAim: aim is :aim,
                bearing: aim.bearing,
                range: aim.range,
                directionZ: aim.direction.z,
                checksum: aim.checksum)
            }
            """;

        var bytecode = GameEventScriptBuilder.Create()
            .WithExternalTypes(registry)
            .AddScript(script)
            .Compile();

        Assert.HasCount(1, bytecode.ExternalTypeConstructorReferences);
        Assert.AreEqual("aim(bearing,direction,range)", bytecode.ExternalTypeConstructorReferences[0].SignatureId);

        var received = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypes(registry)
            .Build()
            .Load(bytecode)
            .Subscribe("Done", ["isAim", "bearing", "range", "directionZ", "checksum"], (message, _) => received.Add(message));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));
        Assert.HasCount(1, received);
        Assert.IsTrue(received[0].Arguments["isAim"].AsBoolean());
        Assert.AreEqual(90m, received[0].Arguments["bearing"].AsNumber());
        Assert.IsTrue(received[0].Arguments["bearing"].IsDecimalUnit(GameEventScriptDecimalUnit.Degree));
        Assert.AreEqual(12m, received[0].Arguments["range"].AsNumber());
        Assert.IsTrue(received[0].Arguments["range"].IsDecimalUnit(GameEventScriptDecimalUnit.Meter));
        Assert.AreEqual(3m, received[0].Arguments["directionZ"].AsNumber());
        Assert.IsTrue(received[0].Arguments["directionZ"].IsDecimalUnit(GameEventScriptDecimalUnit.Meter));
        Assert.AreEqual(102, received[0].Arguments["checksum"].AsInteger());
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
                  let aim be :aim(bearing: 45°, range: 3m, direction: :vector(1m, 0m, 0m))
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
              let aim be :aim(range: 12m, bearing: 90°, direction: :vector(1m, 2m, 3m))
              emit Done(
                score: :aim.score aim,
                lead: :aim.lead heading: 90°,
                distance: :aim.distance value: 12)
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
            .Subscribe("Done", ["score", "lead", "distance"], (message, _) => received.Add(message));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));
        Assert.HasCount(1, received);
        Assert.AreEqual(102, received[0].Arguments["score"].AsInteger());
        Assert.AreEqual(95m, received[0].Arguments["lead"].AsNumber());
        Assert.IsTrue(received[0].Arguments["lead"].IsDecimalUnit(GameEventScriptDecimalUnit.Degree));
        Assert.AreEqual(12m, received[0].Arguments["distance"].AsNumber());
        Assert.IsTrue(received[0].Arguments["distance"].IsDecimalUnit(GameEventScriptDecimalUnit.Meter));
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
            [GesParam("bearing", GameEventScriptValueKind.Decimal, GameEventScriptDecimalUnit.Degree)] decimal bearing,
            [GesParam("range", GameEventScriptValueKind.Decimal, GameEventScriptDecimalUnit.Meter)] decimal range,
            [GesParam("direction", GameEventScriptValueKind.Vector, GameEventScriptDecimalUnit.Meter)] GameEventScriptVectorValue direction)
        {
            Bearing = bearing;
            Range = range;
            Direction = direction;
            Checksum = (int)(bearing + range);
        }

        [GesField("bearing", GameEventScriptValueKind.Decimal, GameEventScriptDecimalUnit.Degree)]
        public decimal Bearing { get; }

        [GesField("range", GameEventScriptValueKind.Decimal, GameEventScriptDecimalUnit.Meter)]
        public decimal Range { get; }

        [GesField("direction", GameEventScriptValueKind.Vector, GameEventScriptDecimalUnit.Meter)]
        public GameEventScriptVectorValue Direction { get; }

        [GesField("checksum", GameEventScriptValueKind.Integer)]
        public int Checksum { get; }
    }

    [GesExtension("aim")]
    private static class AimExtensionFunctions
    {
        [GesFunction("score", GameEventScriptValueKind.Integer)]
        public static long Score([GesParam("_", "aim")] AimValue aim) => aim.Checksum;

        [GesFunction("lead", GameEventScriptValueKind.Decimal, GameEventScriptDecimalUnit.Degree)]
        public static decimal Lead([GesParam("heading", GameEventScriptValueKind.Decimal, GameEventScriptDecimalUnit.Degree)] decimal heading)
            => heading + 5m;

        [GesFunction("distance")]
        public static (decimal, GameEventScriptDecimalUnit) Distance([GesParam("value", GameEventScriptValueKind.Decimal)] decimal value)
            => (value, GameEventScriptDecimalUnit.Meter);
    }

    [GesExtension("boxed")]
    private static class BoxedExtensionFunctions
    {
        [GesFunction("value")]
        public static GameEventScriptFastValue Value([GesParam("_", GameEventScriptValueKind.Decimal)] GameEventScriptValue value)
            => GameEventScriptFastValue.FromGameEventScriptValue(value);
    }
}
