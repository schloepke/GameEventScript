using StepH.GameEventScript;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Types.GameEventScriptValueFactory;

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

        var listValue = List(sourceList);
        var dictionaryValue = Dictionary(sourceDictionary);
        var setValue = Set(sourceSet);

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
        var listValue = List([1m, 2m]);
        var listView = listValue.AsList();
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<GameEventScriptValue>)listView)[0] = 9m);
        Assert.AreEqual(1m, listValue.AsList()[0].AsNumber());

        var dictionaryValue = Dictionary(new Dictionary<string, GameEventScriptValue> { ["a"] = 1m });
        var dictionaryView = dictionaryValue.AsDictionary();
        Assert.ThrowsExactly<NotSupportedException>(() => ((IDictionary<string, GameEventScriptValue>)dictionaryView)["b"] = 2m);
        Assert.HasCount(1, dictionaryValue.AsDictionary());

        var setValue = Set([1m, 2m]);
        var setCopy = setValue.AsSet();
        setCopy.Add(3m);
        Assert.HasCount(2, setValue.AsSet());
    }

    [TestMethod]
    public void EmptyValuesUseTypedSingletons()
    {
        Assert.AreSame(Text(string.Empty), Text(string.Empty));
        Assert.AreSame(OptionalNone(), OptionalNone());
        Assert.AreSame(GameEventScriptValueFactory.List(null), GameEventScriptValueFactory.List(Array.Empty<GameEventScriptValue>()));
        Assert.AreSame(GameEventScriptValueFactory.Dictionary(null), GameEventScriptValueFactory.Dictionary(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)));
        Assert.AreSame(GameEventScriptValueFactory.Set(null), GameEventScriptValueFactory.Set(Array.Empty<GameEventScriptValue>()));
        Assert.AreSame(GameEventScriptValueFactory.Dice(null), GameEventScriptDiceValue.GameEventScriptDice(Array.Empty<int>()));
        Assert.AreSame(GameEventScriptValueFactory.Message(GameEventScriptMessage.EmptyMessage), GameEventScriptValueFactory.Message(new GameEventScriptMessage(string.Empty)));
        Assert.AreSame(GameEventScriptValueFactory.Handler(new GameEventScriptMessageSignature(string.Empty, [])), GameEventScriptValueFactory.Handler(new GameEventScriptMessageSignature(string.Empty, [])));
    }

    [TestMethod]
    public void StableComparisonOrdersValuesDeterministically()
    {
        var values = new List<GameEventScriptValue> { Boolean(true), Text("b"), Decimal(2m), Integer(1), OptionalNone(), Text("a") };

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
        var left = Percentage(0.25m);
        var right = Percentage(0.25m);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
    }

    [TestMethod]
    public void DecimalUnitsPreserveValueUnitAndFormatting()
    {
        var over = Degree(450m);
        var negative = Degree(-270m);
        var fullTurn = Degree(360m);
        var distance = Meter(100m);
        var duration = Seconds(15m);

        Assert.AreEqual(GameEventScriptValueKind.Decimal, over.Kind);
        Assert.AreEqual(450m, over.AsNumber());
        Assert.IsTrue(over.IsNumber());
        Assert.IsTrue(over.IsDecimalUnit(GameEventScriptDecimalUnit.Degree));
        Assert.AreEqual(-270m, negative.AsNumber());
        Assert.AreEqual(360m, fullTurn.AsNumber());
        Assert.AreNotEqual(over, negative);
        Assert.AreNotEqual(over, Decimal(450m));
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
        var values = new List<GameEventScriptValue> { Degree(350m), Degree(10m), Decimal(10m), Meter(10m) };

        values.Sort(GameEventScriptValue.StableComparer);

        Assert.AreNotEqual(Degree(10m), Decimal(10m));
        Assert.AreNotEqual(Degree(10m), Meter(10m));
        CollectionAssert.AreEqual(new[] { "10", "10°", "350°", "10m" }, values.Select(value => value.ToString()).ToArray());
    }

    [TestMethod]
    public void VectorValuesExposeStableComponents()
    {
        var vector2 = Vector2(10.5m, -2m);
        var vector2Equal = Vector2(10.5m, -2m);
        var vector3 = Vector3(10.5m, -2m, 3m);

        Assert.AreEqual(GameEventScriptValueKind.Vector2, vector2.Kind);
        Assert.AreEqual(vector2, vector2Equal);
        Assert.AreEqual(vector2.GetHashCode(), vector2Equal.GetHashCode());
        Assert.AreEqual(10.5m, vector2.AsDictionary()["x"].AsNumber());
        Assert.AreEqual(-2m, vector2.AsList()[1].AsNumber());
        Assert.AreEqual(3m, vector3.AsDictionary()["z"].AsNumber());
        Assert.AreSame(GameEventScriptVector2Value.Zero, Vector2(0m, 0m));
        Assert.AreSame(GameEventScriptVector3Value.Zero, Vector3(0m, 0m, 0m));
        Assert.AreEqual("vector2[x: 10.5, y: -2]", vector2.ToString());

        var unitVector = Vector2(0m, 0m, GameEventScriptDecimalUnit.Meter);
        var sameComponentsDifferentUnit = Vector2(0m, 0m, GameEventScriptDecimalUnit.Second);
        Assert.AreNotSame(GameEventScriptVector2Value.Zero, unitVector);
        Assert.AreNotEqual(Vector2(0m, 0m), unitVector);
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
        var percentage = Percentage(0.25m);
        var decimalRatio = Decimal(0.25m);

        Assert.IsTrue(percentage.IsNumber());
        Assert.AreEqual(decimalRatio, percentage);
        Assert.AreEqual(decimalRatio.GetHashCode(), percentage.GetHashCode());
    }

    [TestMethod]
    public void PercentageBinaryAluAppliesRelativeBases()
    {
        AssertDecimal(105m, EvaluatePercentageBinary(Decimal(100m), "+", Percentage(0.05m)));
        AssertDecimal(95m, EvaluatePercentageBinary(Integer(100), "-", Percentage(0.05m)));
        AssertDecimal(5m, EvaluatePercentageBinary(Decimal(100m), "*", Percentage(0.05m)));
        AssertDecimal(2000m, EvaluatePercentageBinary(Integer(100), "/", Percentage(0.05m)));
        AssertNaN(EvaluatePercentageBinary(Percentage(0.05m), "+", Decimal(100m)));
        AssertNaN(EvaluatePercentageBinary(Percentage(0.05m), "-", Meter(100m)));
    }

    [TestMethod]
    public void PercentageBinaryAluPreservesPercentageWhenPercentageIsSubject()
    {
        AssertPercentage(0.30m, EvaluatePercentageBinary(Percentage(0.15m), "+", Percentage(0.15m)));
        AssertPercentage(0.10m, EvaluatePercentageBinary(Percentage(0.15m), "-", Percentage(0.05m)));
        AssertPercentage(0.30m, EvaluatePercentageBinary(Percentage(0.15m), "*", Integer(2)));
        AssertPercentage(0.05m, EvaluatePercentageBinary(Percentage(0.15m), "/", Integer(3)));
        AssertDecimal(0.30m, EvaluatePercentageBinary(Integer(2), "*", Percentage(0.15m)));
        AssertPercentage(0.0225m, EvaluatePercentageBinary(Percentage(0.15m), "*", Percentage(0.15m)));
        AssertDecimal(1m, EvaluatePercentageBinary(Percentage(0.15m), "/", Percentage(0.15m)));
        AssertInfinity(EvaluatePercentageBinary(Percentage(0.15m), "/", Integer(0)));
    }

    [TestMethod]
    public void PercentageBinaryAluPreservesUnitsForRelativeBases()
    {
        AssertDecimalUnit(105m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(Meter(100m), "+", Percentage(0.05m)));
        AssertDecimalUnit(95m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(Meter(100m), "-", Percentage(0.05m)));
        AssertDecimalUnit(5m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(Meter(100m), "*", Percentage(0.05m)));
        AssertDecimalUnit(5m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(Percentage(0.05m), "*", Meter(100m)));
        AssertDecimalUnit(2000m, GameEventScriptDecimalUnit.Meter, EvaluatePercentageBinary(Meter(100m), "/", Percentage(0.05m)));
        AssertNaN(EvaluatePercentageBinary(Percentage(0.05m), "/", Meter(100m)));
    }

    [TestMethod]
    public void NumericAluDistinguishesModuloAndRemainder()
    {
        var seven = GameEventScriptValueAlu.NumericValue.Finite(7m);
        var minusSeven = GameEventScriptValueAlu.NumericValue.Finite(-7m);
        var three = GameEventScriptValueAlu.NumericValue.Finite(3m);
        var minusThree = GameEventScriptValueAlu.NumericValue.Finite(-3m);

        Assert.AreEqual(-3m, GameEventScriptValueAlu.IntegerDivideNumeric(minusSeven, three).Value);
        Assert.AreEqual(-3m, GameEventScriptValueAlu.IntegerDivideNumeric(seven, minusThree).Value);
        Assert.AreEqual(3m, GameEventScriptValueAlu.IntegerDivideNumeric(GameEventScriptValueAlu.NumericValue.Finite(7.5m), GameEventScriptValueAlu.NumericValue.Finite(2m)).Value);
        Assert.AreEqual(2m, GameEventScriptValueAlu.ModuloNumeric(minusSeven, three).Value);
        Assert.AreEqual(-2m, GameEventScriptValueAlu.ModuloNumeric(seven, minusThree).Value);
        Assert.AreEqual(-1m, GameEventScriptValueAlu.RemainderNumeric(minusSeven, three).Value);
        Assert.AreEqual(1m, GameEventScriptValueAlu.RemainderNumeric(seven, minusThree).Value);
    }

    [TestMethod]
    public void SetsStayCanonicallySorted()
    {
        var setValue = Set([Decimal(3m), Text("z"), Integer(1), Text("a"), Boolean(false), Decimal(2m)]);

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
        var dice = GameEventScriptDiceValue.GameEventScriptDice([6, 3, 1]);
        var kept = dice.KeepHighest(-1);
        var dropped = dice.DropLowest(-1);

        Assert.HasCount(0, kept.Rolls);
        Assert.HasCount(0, dropped.Rolls);
    }

    [TestMethod]
    public void RangesStopAtIntegerBoundsWithoutOverflowing()
    {
        var ascending = Range(long.MaxValue - 1, long.MaxValue, 1).AsEnumerable().Select(value => value.AsInteger()).ToArray();
        var descending = Range(long.MinValue + 1, long.MinValue, -1).AsEnumerable().Select(value => value.AsInteger()).ToArray();

        CollectionAssert.AreEqual(new[] { long.MaxValue - 1, long.MaxValue }, ascending);
        CollectionAssert.AreEqual(new[] { long.MinValue + 1, long.MinValue }, descending);
    }

    [TestMethod]
    public void ClrDictionariesWithNonTextKeysDoNotThrow()
    {
        var value = GameEventScriptValueFactory.FromClr(new Dictionary<int, string> { [1] = "a" });

        Assert.AreEqual(GameEventScriptValueKind.Dictionary, value.Kind);
        Assert.HasCount(0, value.AsDictionary());
    }

    private static GameEventScriptValue EvaluatePercentageBinary(GameEventScriptValue left, string operation, GameEventScriptValue right)
    {
        Assert.IsTrue(GameEventScriptValueAlu.TryEvaluatePercentageBinary(left, operation, right, out var value));
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
