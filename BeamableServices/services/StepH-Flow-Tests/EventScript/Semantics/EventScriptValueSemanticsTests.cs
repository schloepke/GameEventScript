using System.Linq;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Semantics;

namespace StepH_Flow_Tests.EventScript.Semantics;

[TestClass]
public class EventScriptValueSemanticsScenarios
{
    [TestMethod]
    public void LookupUsesOneBasedIndexingForSequentialValues()
    {
        var items = EventScriptValue.List([10m, 20m, 30m]);

        var second = EventScriptValueSemantics.Lookup(items, 2m);
        var missing = EventScriptValueSemantics.Lookup(items, 4m);

        Assert.AreEqual(20m, second.AsNumber());
        Assert.AreEqual(EventScriptValueKind.Nothing, missing.Kind);
    }

    [TestMethod]
    public void LookupSupportsDictionaryKeysAndOptionalSelectors()
    {
        var dictionary = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = "Mark",
            ["age"] = 25m
        });

        var age = EventScriptValueSemantics.Lookup(dictionary, EventScriptValue.Tag("age"));
        var none = EventScriptValueSemantics.Lookup(dictionary, EventScriptValue.OptionalNone());

        Assert.AreEqual(25m, age.AsNumber());
        Assert.IsTrue(none.isOptional());
        Assert.IsFalse(none.AsOptional().HasValue);
    }

    [TestMethod]
    public void ContainsAndBoundaryChecksCanBeUsedOutsideTheInterpreter()
    {
        var text = EventScriptValue.Text("battle club");
        var list = EventScriptValue.List([1m, 2m, 3m]);
        var prefix = EventScriptValue.List([1m, 2m]);

        Assert.IsTrue(EventScriptValueSemantics.Contains(text, EventScriptValue.Text("club")));
        Assert.IsTrue(EventScriptValueSemantics.Contains(list, 2m));
        Assert.IsTrue(EventScriptValueSemantics.StartsWith(list, prefix));
        Assert.IsFalse(EventScriptValueSemantics.EndsWith(list, prefix));
    }

    [TestMethod]
    public void CollectionContainsAllAndAnyCanBeUsedOutsideTheInterpreter()
    {
        var list = EventScriptValue.List([1m, 2m, 3m, 4m]);
        var all = EventScriptValue.List([2m, 4m]);
        var any = EventScriptValue.List([7m, 4m]);

        Assert.IsTrue(EventScriptCollectionSemantics.ContainsAll(list, list.AsList(), all));
        Assert.IsTrue(EventScriptCollectionSemantics.ContainsAny(list, list.AsList(), any));
        Assert.IsFalse(EventScriptCollectionSemantics.ContainsAll(list, list.AsList(), EventScriptValue.List([2m, 9m])));
    }

    [TestMethod]
    public void CollectionSortingAndDistinctCanBeUsedOutsideTheInterpreter()
    {
        var list = EventScriptValue.List([3m, 1m, 2m, 2m]);

        var sorted = EventScriptCollectionSemantics.Sort(list, list.AsList(), "ascending");
        var distinct = EventScriptCollectionSemantics.Distinct(list, list.AsList());

        CollectionAssert.AreEqual(new[] { 1m, 2m, 2m, 3m }, sorted.AsList().Select(item => item.AsNumber()).ToArray());
        CollectionAssert.AreEqual(new[] { 3m, 1m, 2m }, distinct.AsList().Select(item => item.AsNumber()).ToArray());
    }

    [TestMethod]
    public void CollectionOrderByAndDistinctByCanBeUsedOutsideTheInterpreter()
    {
        var items = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Orc", ["hp"] = 4m, ["team"] = "red" }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Mage", ["hp"] = 7m, ["team"] = "blue" }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["hp"] = 5m, ["team"] = "red" })
        ]);

        var ordered = EventScriptCollectionSemantics.OrderBy(
            items,
            items.AsList(),
            "descending",
            item => item.AsDictionary()["hp"]);

        var distinctBy = EventScriptCollectionSemantics.DistinctBy(
            items,
            items.AsList(),
            item => item.AsDictionary()["team"]);

        CollectionAssert.AreEqual(new[] { "Mage", "Knight", "Orc" }, ordered.AsList().Select(item => item.AsDictionary()["name"].AsText()).ToArray());
        CollectionAssert.AreEqual(new[] { "Orc", "Mage" }, distinctBy.AsList().Select(item => item.AsDictionary()["name"].AsText()).ToArray());
    }

    [TestMethod]
    public void CollectionGroupByCanBeUsedOutsideTheInterpreter()
    {
        var items = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Orc", ["team"] = "red" }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Mage", ["team"] = "blue" }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["team"] = "red" })
        ]);

        var grouped = EventScriptCollectionSemantics.GroupBy(
            items.AsList(),
            item => item.AsDictionary()["team"]);

        Assert.AreEqual(2, grouped.AsDictionary()["red"].AsList().Count);
        Assert.AreEqual(1, grouped.AsDictionary()["blue"].AsList().Count);
    }

    [TestMethod]
    public void HasValueAndIsEmptyFollowContainerSemantics()
    {
        Assert.IsFalse(EventScriptValueSemantics.HasValue(EventScriptValue.List(Array.Empty<EventScriptValue>())));
        Assert.IsTrue(EventScriptValueSemantics.IsEmpty(EventScriptValue.List(Array.Empty<EventScriptValue>())));
        Assert.IsFalse(EventScriptValueSemantics.HasValue(EventScriptValue.NumberNaN()));
        Assert.IsFalse(EventScriptValueSemantics.IsEmpty(EventScriptValue.NumberNaN()));
    }
}
