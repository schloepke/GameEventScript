using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Linker;

[TestClass]
public class EventScriptLinkBuilderScenarios
{
    [TestMethod]
    public void LinkBuilderCanMergeMultipleModulesIntoOneLinkedModule()
    {
        const string sharedScript =
            """
            record :meter as {
                current: :decimal,
                maximum: :decimal
            }

            rule wounded(unit) means unit.hp < unit.maxHp
            select woundedUnits(units) means units[:filter unit where unit is wounded]
            """;

        const string runtimeScript =
            """
            on Start(unit, units) {
                let hp as :meter be [current: unit.hp, maximum: unit.maxHp];
                let anyWounded be wounded(unit);
                let candidates be woundedUnits(units);
                publish Done(arg1: anyWounded, arg2: :len candidates, arg3: hp.maximum);
            }
            """;

        var linkedModule = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(sharedScript))
            .AddModule(EventScriptManager.ParseModule(runtimeScript))
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

        var interpreter = EventScriptInterpretationCompiler.Compile(linkedModule);
        var args = interpreter.InvokePositional("Start", unit, units).EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.AreEqual(1, Convert.ToInt32(args[1].AsInteger()));
        Assert.AreEqual(5m, args[2].AsNumber());
    }

    [TestMethod]
    public void ValidLinkedModulesCanCompileAndInvokeWithoutInterpreterSideValidation()
    {
        const string sharedScript =
            """
            rule wounded(unit) means unit.hp < unit.maxHp
            select living(units) means units[:filter unit where unit.hp > 0]
            """;

        const string runtimeScript =
            """
            on Start(unit, units) {
                let byRule be wounded(unit)
                let bySelect be living(units)
                publish Done(result: byRule, total: :len bySelect)
            }
            """;

        var linkedModule = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(sharedScript))
            .AddModule(EventScriptManager.ParseModule(runtimeScript))
            .Link();

        var compiled = EventScriptInterpretationCompiler.Compile(linkedModule);
        var unit = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["hp"] = 2m,
            ["maxHp"] = 5m
        });
        var units = EventScriptValue.List([
            unit,
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["hp"] = 0m,
                ["maxHp"] = 4m
            })
        ]);

        var args = compiled.InvokePositional("Start", unit, units).EmittedEvents[0].Arguments;

        Assert.IsTrue(args["result"].AsBoolean());
        Assert.AreEqual(1, Convert.ToInt32(args["total"].AsInteger()));
    }

    [TestMethod]
    public void LinkBuilderFailsWhenCrossModuleDependenciesAreMissing()
    {
        const string runtimeScript =
            """
            on Start(unit, units) {
                let wounded be missingRule(unit);
                let choices be missingSelect(units);
                publish Done(arg1: wounded, arg2: choices);
            }
            """;

        var linker = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(runtimeScript));

        Assert.ThrowsExactly<EventScriptLinkageException>(() => linker.Link());
    }

    [TestMethod]
    public void LinkBuilderFailsWhenMergedDefinitionsConflict()
    {
        const string scriptA =
            """
            rule wounded(unit) means unit.hp < unit.maxHp
            """;

        const string scriptB =
            """
            select wounded(unit) means unit.hp < unit.maxHp
            """;

        var linker = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(scriptA))
            .AddModule(EventScriptManager.ParseModule(scriptB));

        Assert.ThrowsExactly<EventScriptLinkageException>(() => linker.Link());
    }

    [TestMethod]
    public void LinkBuilderIncludesModuleNameInDependencyErrors()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module CombatRules
                on Start(unit) {
                    let wounded be missingRule(unit);
                    publish Done(arg1: wounded);
                }
                """, "combat.es"));

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
            .AddModule(EventScriptManager.ParseModule(
                """
                module BrokenRules
                rule wounded(unit) means unit.hp < unit.maxHp
                rule wounded(target) means target.hp < target.maxHp

                on Start(unit) {
                    let byCall be missingRule(unit);
                    let byPredicate be unit is missingPredicate;
                    publish Done(arg1: byCall, arg2: byPredicate);
                }
                """, "broken.es"));

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

    [TestMethod]
    public void LinkBuilderFailsWhenALocalVariableIsDeclaredTwiceInTheSameHandlerScope()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module DuplicateVariables
                on Start {
                    let x be 10
                    let x be 20
                }
                """,
                "duplicate-variables.es"));

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());

        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.DuplicateVariable &&
            error.SymbolKind == EventScriptSymbolKind.Variable &&
            error.Symbol == "x"));
    }

    [TestMethod]
    public void LinkBuilderFailsWhenALocalVariableReusesAHandlerParameterName()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module DuplicateParameterVariable
                on Start(value) {
                    let value be 10
                }
                """,
                "duplicate-parameter-variable.es"));

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());

        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.DuplicateVariable &&
            error.Symbol == "value"));
    }

    [TestMethod]
    public void LinkBuilderFailsWhenABlockDeclaresTheSameVariableTwice()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module DuplicateBlockVariables
                on Start {
                    if true {
                        let x be 10
                        let x be 20
                    }
                }
                """,
                "duplicate-block-variables.es"));

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());

        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.DuplicateVariable &&
            error.Symbol == "x"));
    }

    [TestMethod]
    public void LinkBuilderFailsWhenARuleDeclaresTheSameParameterTwice()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module DuplicateRuleParameters
                rule wounded(unit, unit) means unit.hp < unit.maxHp
                """,
                "duplicate-rule-parameters.es"));

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());

        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.DuplicateDefinitionParameter &&
            error.SymbolKind == EventScriptSymbolKind.Rule &&
            error.Symbol == "wounded"));
    }

    [TestMethod]
    public void LinkBuilderFailsWhenASelectDeclaresTheSameParameterTwice()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module DuplicateSelectParameters
                select wounded(units, units) means units[:filter unit where unit.hp < unit.maxHp]
                """,
                "duplicate-select-parameters.es"));

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());

        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.DuplicateDefinitionParameter &&
            error.SymbolKind == EventScriptSymbolKind.Select &&
            error.Symbol == "wounded"));
    }

    [TestMethod]
    public void LinkBuilderAllowsShadowingInNestedBlockScopes()
    {
        const string script =
            """
            module NestedShadowing
            on Start {
                let x be 10
                if true {
                    let x be 20
                    publish Done(value: x)
                }
                publish Done(value: x)
            }
            """;

        var linkedModule = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(script, "nested-shadowing.es"))
            .Link();

        var compiled = EventScriptInterpretationCompiler.Compile(linkedModule);
        var result = compiled.Invoke("Start");

        Assert.HasCount(2, result.EmittedEvents);
        Assert.AreEqual(20m, result.EmittedEvents[0].Arguments["value"].AsNumber());
        Assert.AreEqual(10m, result.EmittedEvents[1].Arguments["value"].AsNumber());
    }

    [TestMethod]
    public void LinkBuilderFailsWhenHandlerBindingUsesDuplicateNamedArguments()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module DuplicateHandlerBindArgs
                on Start(unit, target, myHandler) {
                    publish myHandler(unit: unit, unit: target)
                }
                """,
                "duplicate-handler-bind-args.es"));

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());
        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.DuplicatePublishArgument &&
            error.Symbol == "handler bind"));
    }

    [TestMethod]
    public void LinkBuilderFailsWhenHandlerLiteralUsesDuplicateParameters()
    {
        var builder = new EventScriptLinkBuilder()
            .AddModule(EventScriptManager.ParseModule(
                """
                module DuplicateHandlerLiteralParams
                on Start(unit, target) {
                    let shoot as :handler be :handler Shoot(unit, unit)
                    publish Done
                }
                """,
                "duplicate-handler-literal-params.es"));

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() => builder.Link());
        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.DuplicateHandlerParameter &&
            error.Symbol == "Shoot"));
    }

    [TestMethod]
    public void LinkBuilderCollectsCaseViolationsForProgrammaticAstModules()
    {
        var module = new EventScriptModule(
            "InvalidCaseModule",
            "invalid-case.es",
            [],
            [
                new RuleDefinitionNode("Wounded", ["unit"], new BooleanLiteralExpressionNode(true))
            ],
            [
                new SelectDefinitionNode("Filter", ["Units"], new IdentifierExpressionNode("Units"))
            ],
            [
                new EventHandlerNode(
                    "start",
                    ["Target"],
                    [
                        new LetStatementNode("Value", null, new IntegerLiteralExpressionNode(1)),
                        new ExpressionStatementNode(new CallExpressionNode("Wounded", [new IdentifierExpressionNode("Target")]))
                    ])
            ]);

        var exception = Assert.ThrowsExactly<EventScriptLinkageException>(() =>
            new EventScriptLinkBuilder().AddModule(module).Link());

        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.InvalidIdentifierCase &&
            error.Symbol == "Wounded"));
        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.InvalidIdentifierCase &&
            error.Symbol == "Filter"));
        Assert.IsTrue(exception.Errors.Any(error =>
            error.Kind == EventScriptLinkageErrorKind.InvalidMessageCase &&
            error.Symbol == "start"));
    }
}
