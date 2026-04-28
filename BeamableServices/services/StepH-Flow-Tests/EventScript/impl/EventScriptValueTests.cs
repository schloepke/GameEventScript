using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.Types.EventScriptValueFactory;

namespace StepH_Flow_Tests.EventScript;

[TestClass]
public class EventScriptValueScenarios
{
    [TestMethod]
    public void BuiltCollectionsDoNotTrackLaterSourceMutations()
    {
        var sourceList = new List<EventScriptValue> { 1m, 2m };
        var sourceDictionary = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["a"] = 1m
        };
        var sourceSet = new HashSet<EventScriptValue> { 1m, 2m };

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
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<EventScriptValue>)listView)[0] = 9m);
        Assert.AreEqual(1m, listValue.AsList()[0].AsNumber());

        var dictionaryValue = Dictionary(new Dictionary<string, EventScriptValue> { ["a"] = 1m });
        var dictionaryView = dictionaryValue.AsDictionary();
        Assert.ThrowsExactly<NotSupportedException>(() => ((IDictionary<string, EventScriptValue>)dictionaryView)["b"] = 2m);
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
        Assert.AreSame(EventScriptValueFactory.List(null), EventScriptValueFactory.List(Array.Empty<EventScriptValue>()));
        Assert.AreSame(EventScriptValueFactory.Dictionary(null), EventScriptValueFactory.Dictionary(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)));
        Assert.AreSame(EventScriptValueFactory.Set(null), EventScriptValueFactory.Set(Array.Empty<EventScriptValue>()));
        Assert.AreSame(EventScriptValueFactory.Dice(null), EventScriptDiceValue.EventScriptDice(Array.Empty<int>()));
        Assert.AreSame(EventScriptValueFactory.Message(EventScriptMessage.EmptyMessage), EventScriptValueFactory.Message(new EventScriptMessage(string.Empty)));
        Assert.AreSame(EventScriptValueFactory.Handler(new EventScriptMessageSignature(string.Empty, [])), EventScriptValueFactory.Handler(new EventScriptMessageSignature(string.Empty, [])));
    }

    [TestMethod]
    public void StableComparisonOrdersValuesDeterministically()
    {
        var values = new List<EventScriptValue> { Boolean(true), Text("b"), Decimal(2m), Integer(1), OptionalNone(), Text("a") };

        values.Sort(EventScriptValue.StableComparer);

        string[] expected = ["1", "2", "a", "b", "True", "Optional.None"];
        CollectionAssert.AreEqual(
            expected,
            values.Select(v => v switch
            {
                { Kind: EventScriptValueKind.Text } => v.AsText(),
                { Kind: EventScriptValueKind.Decimal } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Boolean } => v.AsBoolean().ToString(),
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
    public void DegreeValuesNormalizeToCircleRange()
    {
        var wrapped = Degree(450m);
        var negative = Degree(-270m);
        var zero = Degree(360m);

        Assert.AreEqual(EventScriptValueKind.Degree, wrapped.Kind);
        Assert.AreEqual(90m, wrapped.AsNumber());
        Assert.AreEqual(wrapped, negative);
        Assert.AreEqual(wrapped.GetHashCode(), negative.GetHashCode());
        Assert.AreSame(EventScriptDegreeValue.Zero, zero);
        Assert.AreEqual("90°", wrapped.ToString());
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
    public void SetsStayCanonicallySorted()
    {
        var setValue = Set([Decimal(3m), Text("z"), Integer(1), Text("a"), Boolean(false), Decimal(2m)]);

        var ordered = setValue.AsSet().ToArray();

        string[] expected = ["1", "2", "3", "a", "z", "False"];
        CollectionAssert.AreEqual(
            expected,
            ordered.Select(v => v switch
            {
                { Kind: EventScriptValueKind.Text } => v.AsText(),
                { Kind: EventScriptValueKind.Decimal } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
    }

    [TestMethod]
    public void NegativeDiceKeepOrDropCountsProduceEmptyDice()
    {
        var dice = EventScriptDiceValue.EventScriptDice([6, 3, 1]);
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
        var value = EventScriptValueFactory.FromClr(new Dictionary<int, string> { [1] = "a" });

        Assert.AreEqual(EventScriptValueKind.Dictionary, value.Kind);
        Assert.HasCount(0, value.AsDictionary());
    }
}
