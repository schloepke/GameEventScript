using StepH.Flow.EventScript;
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
    public void OptionalNoneExposesNothingAsItsValue()
    {
        var none = OptionalNone().AsOptional();
        Assert.AreEqual(EventScriptValueType.Nothing, none.Value.Type);
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
    public void AccessorsStayLenientAcrossUnrelatedKinds()
    {
        Assert.AreEqual("12", Decimal(12m).AsText());
        Assert.AreEqual("name", Tag("name").AsText());
        Assert.IsFalse(Text("abc").AsBoolean());
        Assert.AreEqual(1m, Boolean(true).AsNumber());
        Assert.AreEqual(12, Convert.ToInt32(Text("12.8").AsInteger()));
        Assert.HasCount(0, Integer(1).AsList());
        Assert.HasCount(0, Integer(1).AsDictionary());
        Assert.HasCount(0, Integer(1).AsSet());
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
    public void KeysAndValuesCreateIteratorViewsWithoutChangingTheSourceKind()
    {
        var dictionary = Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = Text("Mark"),
            ["age"] = Decimal(25m)
        });
        var list = List([1m, 2m, 3m]);

        var keys = Keys(dictionary);
        var values = Values(list);

        Assert.AreEqual(EventScriptValueType.Iterator, keys.Type);
        Assert.AreEqual(EventScriptValueType.Iterator, values.Type);
        CollectionAssert.AreEqual(new[] { "age", "name" }, keys.AsEnumerable().Select(x => x.AsText()).ToArray());
        CollectionAssert.AreEqual(new[] { 1m, 2m, 3m }, values.AsEnumerable().Select(x => x.AsNumber()).ToArray());
        Assert.AreEqual(EventScriptValueType.Dictionary, dictionary.Type);
        Assert.AreEqual(EventScriptValueType.List, list.Type);
    }

    [TestMethod]
    public void EntriesCreateDictionaryLikeKeyValueItems()
    {
        var dictionary = Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = Text("Mark"),
            ["age"] = Decimal(25m)
        });

        var entries = Entries(dictionary).AsEnumerable().ToArray();

        Assert.AreEqual(2, entries.Length);
        Assert.AreEqual("age", entries[0].AsDictionary()["key"].AsText());
        Assert.AreEqual(25m, entries[0].AsDictionary()["value"].AsNumber());
        Assert.AreEqual("name", entries[1].AsDictionary()["key"].AsText());
        Assert.AreEqual("Mark", entries[1].AsDictionary()["value"].AsText());
    }

    [TestMethod]
    public void TagsBehaveAsNamedValuesWithStableIdentity()
    {
        var first = Tag("name");
        var second = Tag("name");
        var third = Tag("age");

        Assert.AreEqual(EventScriptValueType.Tag, first.Type);
        Assert.AreEqual(":name", first.ToString());
        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, third);
    }

    [TestMethod]
    public void MessageAndHandlerAreFirstClassValueTypesWithReadOnlyMembers()
    {
        var signature = new EventScriptMessageSignature("Shoot", ["unit", "target"]);
        var message = new EventScriptMessage("Shoot", new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["unit"] = Text("u1"),
            ["target"] = Text("t1")
        });

        var messageValue = Message(message);
        var handlerValue = Handler(signature);

        Assert.AreEqual(EventScriptValueType.Message, messageValue.Type);
        Assert.AreEqual(EventScriptValueType.Handler, handlerValue.Type);
        Assert.IsFalse(messageValue.isDictionary());
        Assert.IsFalse(handlerValue.isDictionary());

        Assert.IsTrue(messageValue.TryGetDictionaryMember("name", out var messageName));
        Assert.IsTrue(messageValue.TryGetDictionaryMember("arguments", out var messageArguments));
        Assert.IsTrue(messageValue.TryGetDictionaryMember("signatureid", out var messageSignatureId));
        Assert.AreEqual("Shoot", messageName.AsText());
        Assert.HasCount(2, messageArguments.AsDictionary());
        Assert.AreEqual("Shoot(target,unit)", messageSignatureId.AsText());

        Assert.IsTrue(handlerValue.TryGetDictionaryMember("name", out var handlerName));
        Assert.IsTrue(handlerValue.TryGetDictionaryMember("parameters", out var handlerParameters));
        Assert.IsTrue(handlerValue.TryGetDictionaryMember("signatureid", out var handlerSignatureId));
        Assert.AreEqual("Shoot", handlerName.AsText());
        Assert.HasCount(2, handlerParameters.AsList());
        Assert.AreEqual("Shoot(target,unit)", handlerSignatureId.AsText());
    }

    [TestMethod]
    public void ClrDictionariesWithNonTextKeysDoNotThrow()
    {
        var value = EventScriptValue.FromClr(new Dictionary<int, string> { [1] = "a" });

        Assert.AreEqual(EventScriptValueType.Dictionary, value.Type);
        Assert.HasCount(0, value.AsDictionary());
    }
}
