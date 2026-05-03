using StepH.GameEventScript.Api;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;
using static StepH.GameEventScript.Types.GameEventScriptDecimalUnits;
using GameEventScriptDecimalUnits = StepH.GameEventScript.Types.GameEventScriptDecimalUnits;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public class GameEventScriptValueScenarios
{
    [TestMethod]
    public void BuiltCollectionsDoNotTrackLaterSourceMutations()
    {
        var sourceList = new List<GameEventScriptValue> { 1m, 2m };
        var sourceDictionary = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            ["a"] = 1m
        };
        var sourceSet = new HashSet<GameEventScriptValue> { 1m, 2m };

        var listValue = GesList(sourceList);
        var dictionaryValue = GesDictionary(sourceDictionary);
        var setValue = GesSet(sourceSet);

        sourceList.Add(3m);
        sourceDictionary["b"] = 2m;
        sourceSet.Add(3m);

        Assert.HasCount(2, listValue.AsList());
        Assert.HasCount(1, dictionaryValue.AsDictionary());
        Assert.HasCount(2, setValue.AsSet());
    }

    [TestMethod]
    public void CollectionViewsProtectTheStoredValue()
    {
        var listValue = GesList([1m, 2m]);
        var listView = listValue.AsList();
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<GameEventScriptValue>)listView)[0] = 9m);
        Assert.AreEqual(1m, listValue.AsList()[0].AsNumber());

        var dictionaryValue = GesDictionary(new Dictionary<string, GameEventScriptValue> { ["a"] = 1m });
        var dictionaryView = dictionaryValue.AsDictionary();
        Assert.ThrowsExactly<NotSupportedException>(() => ((IDictionary<string, GameEventScriptValue>)dictionaryView)["b"] = 2m);
        Assert.HasCount(1, dictionaryValue.AsDictionary());

        var setValue = GesSet([1m, 2m]);
        var setCopy = setValue.AsSet();
        setCopy.Add(3m);
        Assert.HasCount(2, setValue.AsSet());
    }

    [TestMethod]
    public void EmptyValuesUseTypedSingletons()
    {
        Assert.AreSame(GesText(string.Empty), GesText(string.Empty));
        Assert.AreSame(GesOptionalNone(), GesOptionalNone());
        Assert.AreSame(GesList(null), GesList(Array.Empty<GameEventScriptValue>()));
        Assert.AreSame(GesDictionary(null), GesDictionary(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)));
        Assert.AreSame(GesSet(null), GesSet(Array.Empty<GameEventScriptValue>()));
        Assert.AreSame(GesDice((GameEventScriptDiceValue?)null), GameEventScriptDiceValue.Create(Array.Empty<int>()));
        Assert.AreSame(GesMessage(GameEventScriptMessage.Empty), GesMessage(GameEventScriptMessage.Empty));
        Assert.AreSame(GesHandler(GameEventScriptMessageSignature.Create(string.Empty, [])), GesHandler(GameEventScriptMessageSignature.Create(string.Empty, [])));
    }

    [TestMethod]
    public void StableComparisonOrdersValuesDeterministically()
    {
        var values = new List<GameEventScriptValue> { GesBoolean(true), GesText("b"), GesDecimal(2m), GesInteger(1), GesOptionalNone(), GesText("a") };

        values.Sort(GameEventScriptValue.StableComparer);

        string[] expected = ["1", "2", "a", "b", "True", "Optional.None"];
        CollectionAssert.AreEqual(
            expected,
            values.Select(v => v switch
            {
                { Kind: GameEventScriptValueKind.Text } => v.AsText(),
                { Kind: GameEventScriptValueKind.Decimal } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: GameEventScriptValueKind.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: GameEventScriptValueKind.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
    }

    [TestMethod]
    public void EqualPercentagesHaveEqualHashCodes()
    {
        var left = GesPercentage(0.25m);
        var right = GesPercentage(0.25m);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
    }

    [TestMethod]
    public void DecimalUnitsPreserveValueUnitAndFormatting()
    {
        var over = GesDegree(450m);
        var negative = GesDegree(-270m);
        var fullTurn = GesDegree(360m);
        var distance = GesMeter(100m);
        var duration = GesSeconds(15m);

        Assert.AreEqual(GameEventScriptValueKind.Decimal, over.Kind);
        Assert.AreEqual(450m, over.AsNumber());
        Assert.IsTrue(over.IsNumber());
        Assert.IsTrue(over.IsDecimalUnit(GameEventScriptDecimalUnit.Degree));
        Assert.AreEqual(-270m, negative.AsNumber());
        Assert.AreEqual(360m, fullTurn.AsNumber());
        Assert.AreNotEqual(over, negative);
        Assert.AreNotEqual(over, GesDecimal(450m));
        Assert.AreEqual(90m, GameEventScriptValue.WrapDegrees(over.AsNumber()));
        Assert.AreEqual(90m, GameEventScriptValue.WrapDegrees(negative.AsNumber()));
        Assert.AreEqual(0m, GameEventScriptValue.WrapDegrees(fullTurn.AsNumber()));
        Assert.AreEqual("450°", over.ToString());
        Assert.AreEqual("100m", distance.ToString());
        Assert.AreEqual("15s", duration.ToString());
    }

    [TestMethod]
    public void DecimalUnitsParticipateInStableOrderingButNotNumericEquality()
    {
        var values = new List<GameEventScriptValue> { GesDegree(350m), GesDegree(10m), GesDecimal(10m), GesMeter(10m) };

        values.Sort(GameEventScriptValue.StableComparer);

        Assert.AreNotEqual(GesDegree(10m), GesDecimal(10m));
        Assert.AreNotEqual(GesDegree(10m), GesMeter(10m));
        CollectionAssert.AreEqual(new[] { "10", "10°", "350°", "10m" }, values.Select(value => value.ToString()).ToArray());
    }

    [TestMethod]
    public void VectorValuesExposeStableComponents()
    {
        var vector2 = GesVector2(10.5m, -2m);
        var vector2Equal = GesVector2(10.5m, -2m);
        var vector3 = GesVector3(10.5m, -2m, 3m);

        Assert.AreEqual(GameEventScriptValueKind.Vector2, vector2.Kind);
        Assert.AreEqual(vector2, vector2Equal);
        Assert.AreEqual(vector2.GetHashCode(), vector2Equal.GetHashCode());
        Assert.AreEqual(10.5m, vector2.AsDictionary()["x"].AsNumber());
        Assert.AreEqual(-2m, vector2.AsList()[1].AsNumber());
        Assert.AreEqual(3m, vector3.AsDictionary()["z"].AsNumber());
        Assert.AreSame(GameEventScriptVector2Value.Zero, GesVector2(0m, 0m));
        Assert.AreSame(GameEventScriptVector3Value.Zero, GesVector3(0m, 0m, 0m));
        Assert.AreEqual("vector2[x: 10.5, y: -2]", vector2.ToString());

        var unitVector = GesVector2(0m, 0m, GameEventScriptDecimalUnit.Meter);
        var sameComponentsDifferentUnit = GesVector2(0m, 0m, GameEventScriptDecimalUnit.Second);
        Assert.AreNotSame(GameEventScriptVector2Value.Zero, unitVector);
        Assert.AreNotEqual(GesVector2(0m, 0m), unitVector);
        Assert.AreNotEqual(unitVector, sameComponentsDifferentUnit);
        Assert.AreNotEqual(unitVector.GetHashCode(), sameComponentsDifferentUnit.GetHashCode());
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, ((GameEventScriptVector2Value)unitVector).Unit);
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, ((GameEventScriptDecimalValue)unitVector.AsDictionary()["x"]).Unit);
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, ((GameEventScriptDecimalValue)unitVector.AsList()[1]).Unit);
        Assert.AreEqual("vector2[x: 0m, y: 0m]", unitVector.ToString());
    }

    [TestMethod]
    public void PercentagesCompareAsNumericRatios()
    {
        var percentage = GesPercentage(0.25m);
        var decimalRatio = GesDecimal(0.25m);

        Assert.IsTrue(percentage.IsNumber());
        Assert.AreEqual(decimalRatio, percentage);
        Assert.AreEqual(decimalRatio.GetHashCode(), percentage.GetHashCode());
    }

    [TestMethod]
    public void PercentageBinaryAluAppliesRelativeBases()
    {
        AssertDecimal(105m, EvaluatePercentageBinary(GesDecimal(100m), "+", GesPercentage(0.05m)));
        AssertDecimal(95m, EvaluatePercentageBinary(GesInteger(100), "-", GesPercentage(0.05m)));
        AssertDecimal(5m, EvaluatePercentageBinary(GesDecimal(100m), "*", GesPercentage(0.05m)));
        AssertDecimal(2000m, EvaluatePercentageBinary(GesInteger(100), "/", GesPercentage(0.05m)));
        AssertNaN(EvaluatePercentageBinary(GesPercentage(0.05m), "+", GesDecimal(100m)));
        AssertNaN(EvaluatePercentageBinary(GesPercentage(0.05m), "-", GesMeter(100m)));
    }

    [TestMethod]
    public void PercentageBinaryAluPreservesPercentageWhenPercentageIsSubject()
    {
        AssertPercentage(0.30m, EvaluatePercentageBinary(GesPercentage(0.15m), "+", GesPercentage(0.15m)));
        AssertPercentage(0.10m, EvaluatePercentageBinary(GesPercentage(0.15m), "-", GesPercentage(0.05m)));
        AssertPercentage(0.30m, EvaluatePercentageBinary(GesPercentage(0.15m), "*", GesInteger(2)));
        AssertPercentage(0.05m, EvaluatePercentageBinary(GesPercentage(0.15m), "/", GesInteger(3)));
        AssertDecimal(0.30m, EvaluatePercentageBinary(GesInteger(2), "*", GesPercentage(0.15m)));
        AssertPercentage(0.0225m, EvaluatePercentageBinary(GesPercentage(0.15m), "*", GesPercentage(0.15m)));
        AssertDecimal(1m, EvaluatePercentageBinary(GesPercentage(0.15m), "/", GesPercentage(0.15m)));
        AssertInfinity(EvaluatePercentageBinary(GesPercentage(0.15m), "/", GesInteger(0)));
    }

    [TestMethod]
    public void PercentageBinaryAluPreservesUnitsForRelativeBases()
    {
        AssertDecimalUnit(105m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(GesMeter(100m), "+", GesPercentage(0.05m)));
        AssertDecimalUnit(95m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(GesMeter(100m), "-", GesPercentage(0.05m)));
        AssertDecimalUnit(5m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(GesMeter(100m), "*", GesPercentage(0.05m)));
        AssertDecimalUnit(5m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(GesPercentage(0.05m), "*", GesMeter(100m)));
        AssertDecimalUnit(2000m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(GesMeter(100m), "/", GesPercentage(0.05m)));
        AssertNaN(EvaluatePercentageBinary(GesPercentage(0.05m), "/", GesMeter(100m)));
    }

    [TestMethod]
    public void NumericAluDistinguishesModuloAndRemainder()
    {
        var seven = GesValueOperations.NumericValue.Finite(7m);
        var minusSeven = GesValueOperations.NumericValue.Finite(-7m);
        var three = GesValueOperations.NumericValue.Finite(3m);
        var minusThree = GesValueOperations.NumericValue.Finite(-3m);

        Assert.AreEqual(-3m, GesValueOperations.IntegerDivideNumeric(minusSeven, three).Value);
        Assert.AreEqual(-3m, GesValueOperations.IntegerDivideNumeric(seven, minusThree).Value);
        Assert.AreEqual(3m, GesValueOperations.IntegerDivideNumeric(GesValueOperations.NumericValue.Finite(7.5m), GesValueOperations.NumericValue.Finite(2m)).Value);
        Assert.AreEqual(2m, GesValueOperations.ModuloNumeric(minusSeven, three).Value);
        Assert.AreEqual(-2m, GesValueOperations.ModuloNumeric(seven, minusThree).Value);
        Assert.AreEqual(-1m, GesValueOperations.RemainderNumeric(minusSeven, three).Value);
        Assert.AreEqual(1m, GesValueOperations.RemainderNumeric(seven, minusThree).Value);
    }

    [TestMethod]
    public void SetsStayCanonicallySorted()
    {
        var setValue = GesSet([GesDecimal(3m), GesText("z"), GesInteger(1), GesText("a"), GesBoolean(false), GesDecimal(2m)]);

        var ordered = setValue.AsSet().ToArray();

        string[] expected = ["1", "2", "3", "a", "z", "False"];
        CollectionAssert.AreEqual(
            expected,
            ordered.Select(v => v switch
            {
                { Kind: GameEventScriptValueKind.Text } => v.AsText(),
                { Kind: GameEventScriptValueKind.Decimal } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: GameEventScriptValueKind.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: GameEventScriptValueKind.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
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
        var value = new Dictionary<int, string> { [1] = "a" }.ToGseType();

        Assert.AreEqual(GameEventScriptValueKind.Dictionary, value.Kind);
        Assert.HasCount(0, value.AsDictionary());
    }

    private static GameEventScriptValue EvaluatePercentageBinary(GameEventScriptValue left, string operation, GameEventScriptValue right)
    {
        Assert.IsTrue(GesValueOperations.TryEvaluatePercentageBinary(left, operation, right, out var value));
        return value;
    }

    private static void AssertDecimal(decimal expected, GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Decimal, actual.Kind);
        Assert.AreEqual(expected, actual.AsNumber());
        Assert.IsFalse(actual.HasDecimalUnit());
    }

    private static void AssertDecimalUnit(decimal expected, GameEventScriptDecimalUnit unit, GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Decimal, actual.Kind);
        Assert.AreEqual(expected, actual.AsNumber());
        Assert.IsTrue(actual.IsDecimalUnit(unit));
    }

    private static void AssertPercentage(decimal expectedRatio, GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Percentage, actual.Kind);
        Assert.AreEqual(expectedRatio, actual.AsNumber());
    }

    private static void AssertNaN(GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Decimal, actual.Kind);
        Assert.IsTrue(actual.IsNaN());
    }

    private static void AssertInfinity(GameEventScriptValue actual)
    {
        Assert.AreEqual(GameEventScriptValueKind.Decimal, actual.Kind);
        Assert.IsTrue(actual.IsInfinity());
        Assert.IsFalse(actual.IsNegativeInfinity());
    }
}
