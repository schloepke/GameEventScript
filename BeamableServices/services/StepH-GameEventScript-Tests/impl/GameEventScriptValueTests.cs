using StepH.GameEventScript.Api;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnits;
using GameEventScriptBytecodeInstructionUnits = StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnits;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public class GameEventScriptValueScenarios
{
    [TestMethod]
    public void BuiltCollectionsDoNotTrackLaterSourceMutations()
    {
        var sourceList = new List<GameEventScriptValue> { 1d, 2d };
        var sourceDictionary = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            ["a"] = 1d
        };
        var listValue = GesList(sourceList);
        var dictionaryValue = GesMap(sourceDictionary);

        sourceList.Add(3d);
        sourceDictionary["b"] = 2d;

        Assert.HasCount(2, listValue.AsList());
        Assert.HasCount(1, dictionaryValue.AsMap());
    }

    [TestMethod]
    public void CollectionViewsProtectTheStoredValue()
    {
        var listValue = GesList([1d, 2d]);
        var listView = listValue.AsList();
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<GameEventScriptValue>)listView)[0] = 9d);
        Assert.AreEqual(1d, listValue.AsList()[0].AsNumber());

        var dictionaryValue = GesMap(new Dictionary<string, GameEventScriptValue> { ["a"] = 1d });
        var dictionaryView = dictionaryValue.AsMap();
        Assert.ThrowsExactly<NotSupportedException>(() => ((IDictionary<string, GameEventScriptValue>)dictionaryView)["b"] = 2d);
        Assert.HasCount(1, dictionaryValue.AsMap());
    }

    [TestMethod]
    public void EmptyValuesUseTypedSingletons()
    {
        Assert.AreSame(GesText(string.Empty), GesText(string.Empty));
        Assert.AreSame(GesNothing(), GesMaybe(null));
        Assert.AreSame(GesList(null), GesList(Array.Empty<GameEventScriptValue>()));
        Assert.AreSame(GesMap(null), GesMap(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)));
        Assert.AreSame(GesDice((GameEventScriptDiceValue?)null), GameEventScriptDiceValue.Create(Array.Empty<int>()));
        Assert.AreSame(GesHandler(GameEventScriptMessageSignature.Create(string.Empty, [])), GesHandler(GameEventScriptMessageSignature.Create(string.Empty, [])));
    }

    [TestMethod]
    public void MessageValuesExposeTagsAsPartOfTheValue()
    {
        var message = GameEventScriptMessage.Create("Ping", new Dictionary<string, GameEventScriptValue> { ["amount"] = GesInteger(7) }, ["radio", "encrypted"]);
        var value = GesMessage(message);

        var map = value.AsMap();
        Assert.IsTrue(map.TryGetValue("tags", out var tags));
        CollectionAssert.AreEqual(
            new[] { "radio", "encrypted" },
            tags.AsList().Select(tag => tag.AsText()).ToArray());
    }

    [TestMethod]
    public void MessageEqualityIncludesTags()
    {
        var args = new Dictionary<string, GameEventScriptValue> { ["amount"] = GesInteger(7) };
        var untagged = GesMessage(GameEventScriptMessage.Create("Ping", args));
        var tagged = GesMessage(GameEventScriptMessage.Create("Ping", args, ["radio"]));

        Assert.AreNotEqual(untagged, tagged);
        Assert.AreEqual(
            ((GameEventScriptMessageValue)untagged).Value.SignatureId,
            ((GameEventScriptMessageValue)tagged).Value.SignatureId);
    }

    [TestMethod]
    public void MessagesRequireNames()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptMessage.Create(string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptMessage.Create("   "));
    }

    [TestMethod]
    public void ValuesStoreExplicitUnitSentinels()
    {
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitNothing, GesNothing().Unit);
        Assert.IsFalse(GesNothing().HasUnit);

        var unitlessNumber = GesFloat(1d, null);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitNone, unitlessNumber.Unit);
        Assert.IsFalse(unitlessNumber.HasUnit);

        var unitlessVector = GesVector(1d, 2d, 3d, GameEventScriptBytecodeInstructionUnit.UnitNothing);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitNone, unitlessVector.Unit);
        Assert.IsFalse(unitlessVector.HasUnit);

        var meteredNumber = GesInteger(3, GameEventScriptBytecodeInstructionUnit.UnitMeter);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, meteredNumber.Unit);
        Assert.IsTrue(meteredNumber.HasUnit);
    }

    [TestMethod]
    public void StableComparisonOrdersValuesDeterministically()
    {
        var values = new List<GameEventScriptValue> { GesBoolean(true), GesText("b"), GesFloat(2d), GesInteger(1), GesNothing(), GesText("a") };

        values.Sort(GameEventScriptValue.StableComparer);

        string[] expected = ["Nothing", "1", "2", "a", "b", "True"];
        CollectionAssert.AreEqual(
            expected,
            values.Select(v => v switch
            {
                { Kind: GameEventScriptValueKind.Text } => v.AsText(),
                { Kind: GameEventScriptValueKind.Number } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: GameEventScriptValueKind.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
    }

    [TestMethod]
    public void EqualPercentagesHaveEqualHashCodes()
    {
        var left = GesPercentage(0.25d);
        var right = GesPercentage(0.25d);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
    }

    [TestMethod]
    public void NumericUnitsPreserveValueUnitAndFormatting()
    {
        var over = GesDegree(450d);
        var negative = GesDegree(-270d);
        var fullTurn = GesDegree(360d);
        var distance = GesMeter(100d);
        var duration = GesSeconds(15d);

        Assert.AreEqual(GameEventScriptValueKind.Number, over.Kind);
        Assert.AreEqual(450d, over.AsNumber());
        Assert.IsTrue(over.IsNumber());
        Assert.IsTrue(over.IsNumericUnit(GameEventScriptBytecodeInstructionUnit.UnitDegree));
        Assert.AreEqual(-270d, negative.AsNumber());
        Assert.AreEqual(360d, fullTurn.AsNumber());
        Assert.AreNotEqual(over, negative);
        Assert.AreNotEqual(over, GesFloat(450d));
        Assert.AreEqual(90d, GameEventScriptValue.WrapDegrees(over.AsNumber()));
        Assert.AreEqual(90d, GameEventScriptValue.WrapDegrees(negative.AsNumber()));
        Assert.AreEqual(0d, GameEventScriptValue.WrapDegrees(fullTurn.AsNumber()));
        Assert.AreEqual("450°", over.ToString());
        Assert.AreEqual("100m", distance.ToString());
        Assert.AreEqual("15s", duration.ToString());
    }

    [TestMethod]
    public void NumericUnitsParticipateInStableOrderingButNotNumericEquality()
    {
        var values = new List<GameEventScriptValue> { GesDegree(350d), GesDegree(10d), GesFloat(10d), GesMeter(10d) };

        values.Sort(GameEventScriptValue.StableComparer);

        Assert.AreNotEqual(GesDegree(10d), GesFloat(10d));
        Assert.AreNotEqual(GesDegree(10d), GesMeter(10d));
        CollectionAssert.AreEqual(new[] { "10", "10°", "350°", "10m" }, values.Select(value => value.ToString()).ToArray());
    }

    [TestMethod]
    public void VectorValuesExposeStableComponents()
    {
        var vector = GesVector(10.5d, -2d);
        var vectorEqual = GesVector(10.5d, -2d);
        var vectorWithZ = GesVector(10.5d, -2d, 3d);

        Assert.AreEqual(GameEventScriptValueKind.Vector, vector.Kind);
        Assert.AreEqual(vector, vectorEqual);
        Assert.AreEqual(vector.GetHashCode(), vectorEqual.GetHashCode());
        Assert.AreEqual(10.5d, vector.AsMap()["x"].AsNumber());
        Assert.AreEqual(-2d, vector.AsList()[1].AsNumber());
        Assert.AreEqual(0d, vector.AsMap()["z"].AsNumber());
        Assert.AreEqual(3d, vectorWithZ.AsMap()["z"].AsNumber());
        Assert.AreSame(GameEventScriptVectorValue.Zero, GesVector(0d, 0d, 0d));
        Assert.AreEqual("vector[x: 10.5, y: -2, z: 0]", vector.ToString());

        var unitVector = GesVector(0d, 0d, 0d, GameEventScriptBytecodeInstructionUnit.UnitMeter);
        var sameComponentsDifferentUnit = GesVector(0d, 0d, 0d, GameEventScriptBytecodeInstructionUnit.UnitSecond);
        Assert.AreNotSame(GameEventScriptVectorValue.Zero, unitVector);
        Assert.AreNotEqual(GesVector(0d, 0d, 0d), unitVector);
        Assert.AreNotEqual(unitVector, sameComponentsDifferentUnit);
        Assert.AreNotEqual(unitVector.GetHashCode(), sameComponentsDifferentUnit.GetHashCode());
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, ((GameEventScriptVectorValue)unitVector).Unit);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, ((GameEventScriptNumberValue)unitVector.AsMap()["x"]).Unit);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, ((GameEventScriptNumberValue)unitVector.AsList()[1]).Unit);
        Assert.AreEqual("vector[x: 0m, y: 0m, z: 0m]", unitVector.ToString());
    }

    [TestMethod]
    public void PointValuesExposeStableComponents()
    {
        var point = GesPoint(10.5d, -2d);
        var pointEqual = GesPoint(10.5d, -2d);
        var pointWithZ = GesPoint(10.5d, -2d, 3d);

        Assert.AreEqual(GameEventScriptValueKind.Point, point.Kind);
        Assert.AreEqual(point, pointEqual);
        Assert.AreEqual(point.GetHashCode(), pointEqual.GetHashCode());
        Assert.AreEqual(10.5d, point.AsMap()["x"].AsNumber());
        Assert.AreEqual(-2d, point.AsList()[1].AsNumber());
        Assert.AreEqual(0d, point.AsMap()["z"].AsNumber());
        Assert.AreEqual(3d, pointWithZ.AsMap()["z"].AsNumber());
        Assert.AreSame(GameEventScriptPointValue.Zero, GesPoint(0d, 0d, 0d));
        Assert.AreEqual("point[x: 10.5, y: -2, z: 0]", point.ToString());

        var unitPoint = GesPoint(0d, 0d, 0d, GameEventScriptBytecodeInstructionUnit.UnitMeter);
        var sameComponentsDifferentUnit = GesPoint(0d, 0d, 0d, GameEventScriptBytecodeInstructionUnit.UnitSecond);
        Assert.AreNotSame(GameEventScriptPointValue.Zero, unitPoint);
        Assert.AreNotEqual(GesPoint(0d, 0d, 0d), unitPoint);
        Assert.AreNotEqual(unitPoint, sameComponentsDifferentUnit);
        Assert.AreNotEqual(unitPoint.GetHashCode(), sameComponentsDifferentUnit.GetHashCode());
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, ((GameEventScriptPointValue)unitPoint).Unit);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, ((GameEventScriptNumberValue)unitPoint.AsMap()["x"]).Unit);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, ((GameEventScriptNumberValue)unitPoint.AsList()[1]).Unit);
        Assert.AreEqual("point[x: 0m, y: 0m, z: 0m]", unitPoint.ToString());
    }

    [TestMethod]
    public void PercentagesCompareAsNumericRatios()
    {
        var percentage = GesPercentage(0.25d);
        var floatRatio = GesFloat(0.25d);

        Assert.IsTrue(percentage.IsNumber());
        Assert.AreEqual(floatRatio, percentage);
        Assert.AreEqual(floatRatio.GetHashCode(), percentage.GetHashCode());
    }

    [TestMethod]
    public void PercentageBinaryAluAppliesRelativeBases()
    {
        AssertFloat(105d, EvaluatePercentageBinary(GesFloat(100d), "+", GesPercentage(0.05d)));
        AssertFloat(95d, EvaluatePercentageBinary(GesInteger(100), "-", GesPercentage(0.05d)));
        AssertFloat(5d, EvaluatePercentageBinary(GesFloat(100d), "*", GesPercentage(0.05d)));
        AssertFloat(2000d, EvaluatePercentageBinary(GesInteger(100), "/", GesPercentage(0.05d)));
        AssertNaN(EvaluatePercentageBinary(GesPercentage(0.05d), "+", GesFloat(100d)));
        AssertNaN(EvaluatePercentageBinary(GesPercentage(0.05d), "-", GesMeter(100d)));
    }

    [TestMethod]
    public void PercentageBinaryAluPreservesPercentageWhenPercentageIsSubject()
    {
        AssertPercentage(0.15d + 0.15d, EvaluatePercentageBinary(GesPercentage(0.15d), "+", GesPercentage(0.15d)));
        AssertPercentage(0.15d - 0.05d, EvaluatePercentageBinary(GesPercentage(0.15d), "-", GesPercentage(0.05d)));
        AssertFloat(0.15d * 2d, EvaluatePercentageBinary(GesPercentage(0.15d), "*", GesInteger(2)));
        AssertPercentage(0.15d / 3d, EvaluatePercentageBinary(GesPercentage(0.15d), "/", GesInteger(3)));
        AssertFloat(0.30d, EvaluatePercentageBinary(GesInteger(2), "*", GesPercentage(0.15d)));
        AssertPercentage(0.15d * 0.15d, EvaluatePercentageBinary(GesPercentage(0.15d), "*", GesPercentage(0.15d)));
        AssertFloat(1d, EvaluatePercentageBinary(GesPercentage(0.15d), "/", GesPercentage(0.15d)));
        AssertInfinity(EvaluatePercentageBinary(GesPercentage(0.15d), "/", GesInteger(0)));
    }

    [TestMethod]
    public void PercentageBinaryAluPreservesUnitsForRelativeBases()
    {
        AssertNumericUnit(105d, GameEventScriptBytecodeInstructionUnit.UnitMeter, EvaluatePercentageBinary(GesMeter(100d), "+", GesPercentage(0.05d)));
        AssertNumericUnit(95d, GameEventScriptBytecodeInstructionUnit.UnitMeter, EvaluatePercentageBinary(GesMeter(100d), "-", GesPercentage(0.05d)));
        AssertNumericUnit(5d, GameEventScriptBytecodeInstructionUnit.UnitMeter, EvaluatePercentageBinary(GesMeter(100d), "*", GesPercentage(0.05d)));
        AssertNumericUnit(5d, GameEventScriptBytecodeInstructionUnit.UnitMeter, EvaluatePercentageBinary(GesPercentage(0.05d), "*", GesMeter(100d)));
        AssertNumericUnit(2000d, GameEventScriptBytecodeInstructionUnit.UnitMeter, EvaluatePercentageBinary(GesMeter(100d), "/", GesPercentage(0.05d)));
        AssertNaN(EvaluatePercentageBinary(GesPercentage(0.05d), "/", GesMeter(100d)));
    }

    [TestMethod]
    public void NumericAluDistinguishesModuloAndRemainder()
    {
        var seven = GesValueOperations.NumericValue.Finite(7d);
        var minusSeven = GesValueOperations.NumericValue.Finite(-7d);
        var three = GesValueOperations.NumericValue.Finite(3d);
        var minusThree = GesValueOperations.NumericValue.Finite(-3d);

        Assert.AreEqual(-3d, GesValueOperations.IntegerDivideNumeric(minusSeven, three).Value);
        Assert.AreEqual(-3d, GesValueOperations.IntegerDivideNumeric(seven, minusThree).Value);
        Assert.AreEqual(3d, GesValueOperations.IntegerDivideNumeric(GesValueOperations.NumericValue.Finite(7.5d), GesValueOperations.NumericValue.Finite(2d)).Value);
        Assert.AreEqual(2d, GesValueOperations.ModuloNumeric(minusSeven, three).Value);
        Assert.AreEqual(-2d, GesValueOperations.ModuloNumeric(seven, minusThree).Value);
        Assert.AreEqual(-1d, GesValueOperations.RemainderNumeric(minusSeven, three).Value);
        Assert.AreEqual(1d, GesValueOperations.RemainderNumeric(seven, minusThree).Value);
    }

    [TestMethod]
    public void NegativeDiceKeepOrDropCountsProduceEmptyDice()
    {
        var dice = GameEventScriptDiceValue.Create([6, 3, 1]);
        var kept = dice.KeepHighest(-1);
        var dropped = dice.DropLowest(-1);

        Assert.HasCount(0, kept.Rolls);
        Assert.HasCount(0, dropped.Rolls);
    }

    [TestMethod]
    public void RangesStopAtIntegerBoundsWithoutOverflowing()
    {
        var ascending = GesRange(long.MaxValue - 1, long.MaxValue).AsEnumerable().Select(value => value.AsInteger()).ToArray();
        var descending = GesRange(long.MinValue + 1, long.MinValue, -1).AsEnumerable().Select(value => value.AsInteger()).ToArray();

        CollectionAssert.AreEqual(new[] { long.MaxValue - 1, long.MaxValue }, ascending);
        CollectionAssert.AreEqual(new[] { long.MinValue + 1, long.MinValue }, descending);
    }

    [TestMethod]
    public void ClrDictionariesWithNonTextKeysDoNotThrow()
    {
        var value = new Dictionary<int, string> { [1] = "a" }.ToGameEventScriptValue();

        Assert.AreEqual(GameEventScriptValueKind.Map, value.Kind);
        Assert.HasCount(0, value.AsMap());
    }

    private static GameEventScriptValue EvaluatePercentageBinary(GameEventScriptValue left, string operation, GameEventScriptValue right)
    {
        Assert.IsTrue(GesValueOperations.TryEvaluatePercentageBinary(left, operation, right, out var value));
        return value;
    }

    private static void AssertFloat(double expected, GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Number, actual.Kind);
        Assert.AreEqual(expected, actual.AsNumber());
        Assert.IsFalse(actual.HasNumericUnit());
    }

    private static void AssertNumericUnit(double expected, GameEventScriptBytecodeInstructionUnit unit, GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Number, actual.Kind);
        Assert.AreEqual(expected, actual.AsNumber());
        Assert.IsTrue(actual.IsNumericUnit(unit));
    }

    private static void AssertPercentage(double expectedRatio, GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Percentage, actual.Kind);
        Assert.AreEqual(expectedRatio, actual.AsNumber());
    }

    private static void AssertNaN(GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Number, actual.Kind);
        Assert.IsTrue(actual.IsNaN());
    }

    private static void AssertInfinity(GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Number, actual.Kind);
        Assert.IsTrue(actual.IsInfinity());
        Assert.IsFalse(actual.IsNegativeInfinity());
    }
}
