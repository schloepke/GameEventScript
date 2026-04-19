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
    public void HasValueAndIsEmptyFollowContainerSemantics()
    {
        Assert.IsFalse(EventScriptValueSemantics.HasValue(EventScriptValue.List(Array.Empty<EventScriptValue>())));
        Assert.IsTrue(EventScriptValueSemantics.IsEmpty(EventScriptValue.List(Array.Empty<EventScriptValue>())));
        Assert.IsFalse(EventScriptValueSemantics.HasValue(EventScriptValue.NumberNaN()));
        Assert.IsFalse(EventScriptValueSemantics.IsEmpty(EventScriptValue.NumberNaN()));
    }
}
