using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.Types.EventScriptValue;

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
    public void StableComparisonOrdersValuesDeterministically()
    {
        var values = new List<EventScriptValue> { Boolean(true), Text("b"), Decimal(2m), Integer(1), OptionalNone(), Text("a") };

        values.Sort(StableComparer);

        string[] expected = ["1", "2", "a", "b", "True", "Optional.None"];
        CollectionAssert.AreEqual(
            expected,
            values.Select(v => v switch
            {
                { Type: EventScriptValueType.Text } => v.AsText(),
                { Type: EventScriptValueType.Decimal } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Type: EventScriptValueType.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Type: EventScriptValueType.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
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
                { Type: EventScriptValueType.Text } => v.AsText(),
                { Type: EventScriptValueType.Decimal } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Type: EventScriptValueType.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Type: EventScriptValueType.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
    }

    [TestMethod]
    public void NegativeDiceKeepOrDropCountsProduceEmptyDice()
    {
        var dice = EventScriptDiceValue.Create([6, 3, 1]);
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
        var value = EventScriptValue.FromClr(new Dictionary<int, string> { [1] = "a" });

        Assert.AreEqual(EventScriptValueType.Dictionary, value.Type);
        Assert.HasCount(0, value.AsDictionary());
    }
}
