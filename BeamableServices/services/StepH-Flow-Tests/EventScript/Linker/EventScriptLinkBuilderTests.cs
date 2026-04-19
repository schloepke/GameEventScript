using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;

namespace StepH_Flow_Tests.EventScript.Linker;

[TestClass]
public class EventScriptLinkBuilderScenarios
{
    [TestMethod]
    public void LinkBuilderCanMergeMultipleModulesIntoOneLinkedModule()
    {
        const string sharedScript = """
            record :meter as {
                current: :decimal,
                maximum: :decimal
            }

            rule wounded(unit) means unit.hp < unit.maxHp
            select woundedUnits(units) means units[:filter unit where unit is wounded]
            """;

        const string runtimeScript = """
            on Start(unit, units) {
                let hp as :meter be [current: unit.hp, maximum: unit.maxHp];
                let anyWounded be wounded(unit);
                let candidates be woundedUnits(units);
                publish Done(anyWounded, :len candidates, hp.maximum);
            }
            """;

        var linkedModule = new EventScriptLinkBuilder()
            .AddScript(sharedScript)
            .AddScript(runtimeScript)
            .Link();

        Assert.AreEqual(2, linkedModule.SourceCount);
        Assert.HasCount(1, linkedModule.TypeDefinitions);
        Assert.HasCount(1, linkedModule.RuleDefinitions);
        Assert.HasCount(1, linkedModule.SelectDefinitions);
        Assert.HasCount(1, linkedModule.Handlers);

        var unit = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["hp"] = 2m,
            ["maxHp"] = 5m
        });
        var units = EventScriptValue.List([
            unit,
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["hp"] = 5m,
                ["maxHp"] = 5m
            })
        ]);

        var interpreter = EventScriptInterpreter.Compile(linkedModule);
        var args = interpreter.Emit("Start", unit, units).EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.AreEqual(1, Convert.ToInt32(args[1].AsInteger()));
        Assert.AreEqual(5m, args[2].AsNumber());
    }

    [TestMethod]
    public void LinkBuilderFailsWhenCrossModuleDependenciesAreMissing()
    {
        const string runtimeScript = """
            on Start(unit, units) {
                let wounded be missingRule(unit);
                let choices be missingSelect(units);
                publish Done(wounded, choices);
            }
            """;

        var linker = new EventScriptLinkBuilder()
            .AddScript(runtimeScript);

        Assert.ThrowsExactly<EventScriptLinkageException>(() => linker.Link());
    }

    [TestMethod]
    public void LinkBuilderFailsWhenMergedDefinitionsConflict()
    {
        const string scriptA = """
            rule wounded(unit) means unit.hp < unit.maxHp
            """;

        const string scriptB = """
            select wounded(unit) means unit.hp < unit.maxHp
            """;

        var linker = new EventScriptLinkBuilder()
            .AddScript(scriptA)
            .AddScript(scriptB);

        Assert.ThrowsExactly<EventScriptLinkageException>(() => linker.Link());
    }

    [TestMethod]
    public void LinkBuilderIncludesModuleNameInDependencyErrors()
    {
        var builder = new EventScriptLinkBuilder()
            .AddScript("""
                #module CombatRules
                on Start(unit) {
                    let wounded be missingRule(unit);
                    publish Done(wounded);
                }
                """, "combat.es");

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());
        StringAssert.Contains(exception.Message, "Module 'CombatRules'");
        Assert.AreEqual("CombatRules", exception.Errors[0].ModuleName);
        Assert.AreEqual("combat.es", exception.Errors[0].SourceLocation.SourceName);
        Assert.AreEqual("missingRule", exception.Errors[0].Symbol);
        Assert.AreEqual(EventScriptLinkageErrorKind.MissingRuleOrSelect, exception.Errors[0].Kind);
    }

    [TestMethod]
    public void LinkBuilderCollectsMultipleErrorsIntoOneLinkageException()
    {
        var builder = new EventScriptLinkBuilder()
            .AddScript("""
                #module BrokenRules
                rule wounded(unit) means unit.hp < unit.maxHp
                rule wounded(target) means target.hp < target.maxHp

                on Start(unit) {
                    let byCall be missingRule(unit);
                    let byPredicate be unit is missingPredicate;
                    publish Done(byCall, byPredicate);
                }
                """, "broken.es");

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());
        Assert.IsTrue(exception.Errors.Count >= 3);
        StringAssert.Contains(exception.Message, "Module 'BrokenRules'");
        StringAssert.Contains(exception.Message, "Rule 'wounded' is defined more than once");
        StringAssert.Contains(exception.Message, "No rule or select named 'missingRule' exists");
        StringAssert.Contains(exception.Message, "Rule 'missingPredicate' must exist");
        Assert.IsTrue(exception.Errors.Any(error => error.Kind == EventScriptLinkageErrorKind.DuplicateRule && error.Symbol == "wounded"));
        Assert.IsTrue(exception.Errors.Any(error => error.Kind == EventScriptLinkageErrorKind.MissingRuleOrSelect && error.Symbol == "missingRule"));
        Assert.IsTrue(exception.Errors.Any(error => error.Kind == EventScriptLinkageErrorKind.InvalidRulePredicate && error.Symbol == "missingPredicate"));
    }
}
