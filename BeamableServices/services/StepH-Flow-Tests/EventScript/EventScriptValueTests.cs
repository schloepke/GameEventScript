using StepH.Flow.EventScript;
using static StepH.Flow.EventScript.EventScriptValue;

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
        var values = new List<EventScriptValue> { Boolean(true), Text("b"), Number(2m), Integer(1), OptionalNone(), Text("a") };

        values.Sort(StableComparer);

        string[] expected = ["1", "2", "a", "b", "True", "Optional.None"];
        CollectionAssert.AreEqual(
            expected,
            values.Select(v => v switch
            {
                { Kind: EventScriptValueKind.Text } => v.AsText(),
                { Kind: EventScriptValueKind.Number } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
    }

    [TestMethod]
    public void SetsStayCanonicallySorted()
    {
        var setValue = Set([Number(3m), Text("z"), Integer(1), Text("a"), Boolean(false), Number(2m)]);

        var ordered = setValue.AsSet().ToArray();

        string[] expected = ["1", "2", "3", "a", "z", "False"];
        CollectionAssert.AreEqual(
            expected,
            ordered.Select(v => v switch
            {
                { Kind: EventScriptValueKind.Text } => v.AsText(),
                { Kind: EventScriptValueKind.Number } => v.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Integer } => v.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                { Kind: EventScriptValueKind.Boolean } => v.AsBoolean().ToString(),
                _ => v.ToString()
            }).ToArray());
    }

    [TestMethod]
    public void OptionalNoneExposesNothingAsItsValue()
    {
        var none = OptionalNone().AsOptional();
        Assert.AreEqual(EventScriptValueKind.Nothing, none.Value.Kind);
    }

    [TestMethod]
    public void NegativeDiceKeepOrDropCountsProduceEmptyDice()
    {
        var dice = EventScriptDice.Create([6, 3, 1]);
        var kept = dice.KeepHighest(-1);
        var dropped = dice.DropLowest(-1);

        Assert.HasCount(0, kept.Rolls);
        Assert.HasCount(0, dropped.Rolls);
    }

    [TestMethod]
    public void AccessorsStayLenientAcrossUnrelatedKinds()
    {
        Assert.AreEqual("12", Number(12m).AsText());
        Assert.AreEqual("name", Tag("name").AsText());
        Assert.IsFalse(Text("abc").AsBoolean());
        Assert.AreEqual(1m, Boolean(true).AsNumber());
        Assert.AreEqual(12, Convert.ToInt32(Text("12.8").AsInteger()));
        Assert.HasCount(0, Integer(1).AsList());
        Assert.HasCount(0, Integer(1).AsDictionary());
        Assert.HasCount(0, Integer(1).AsSet());
    }

    [TestMethod]
    public void KeysAndValuesCreateIteratorViewsWithoutChangingTheSourceKind()
    {
        var dictionary = Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = Text("Mark"),
            ["age"] = Number(25m)
        });
        var list = List([1m, 2m, 3m]);

        var keys = Keys(dictionary);
        var values = Values(list);

        Assert.AreEqual(EventScriptValueKind.Iterator, keys.Kind);
        Assert.AreEqual(EventScriptValueKind.Iterator, values.Kind);
        CollectionAssert.AreEqual(new[] { "age", "name" }, keys.AsEnumerable().Select(x => x.AsText()).ToArray());
        CollectionAssert.AreEqual(new[] { 1m, 2m, 3m }, values.AsEnumerable().Select(x => x.AsNumber()).ToArray());
        Assert.AreEqual(EventScriptValueKind.Dictionary, dictionary.Kind);
        Assert.AreEqual(EventScriptValueKind.List, list.Kind);
    }

    [TestMethod]
    public void TagsBehaveAsNamedValuesWithStableIdentity()
    {
        var first = Tag("name");
        var second = Tag("name");
        var third = Tag("age");

        Assert.AreEqual(EventScriptValueKind.Tag, first.Kind);
        Assert.AreEqual(":name", first.ToString());
        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, third);
    }

    [TestMethod]
    public void ClrDictionariesWithNonTextKeysDoNotThrow()
    {
        var value = EventScriptValue.FromClr(new Dictionary<int, string> { [1] = "a" });

        Assert.AreEqual(EventScriptValueKind.Dictionary, value.Kind);
        Assert.HasCount(0, value.AsDictionary());
    }
}
