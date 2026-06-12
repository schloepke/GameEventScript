using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.Compiler;

[TestClass]
public sealed class GesBinaryCompilerTests
{
    [TestMethod]
    public void CompileBuildsRunnableBinaryForSimpleHandler()
    {
        const string script =
            """
            module BinaryCompilerSmoke

            on Start(value) {
                let doubled be value * 2
                emit Done(result: doubled, text: 'ok')
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(6))));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), emitted[0].Arguments["result"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("ok"), emitted[0].Arguments["text"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForFunctionCallAndBranch()
    {
        const string script =
            """
            module BinaryCompilerFunction

            function double(value) means value * 2

            on Start(value) {
                let result be double(value: value)
                if result > 10 {
                    emit Done(result: result, status: :high)
                } else {
                    emit Done(result: result, status: :low)
                }
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(7))));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(14), emitted[0].Arguments["result"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("high"), emitted[0].Arguments["status"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForRecordConstructor()
    {
        const string script =
            """
            module BinaryCompilerRecord

            record :gauge as {
                current: :number clamped between 0 and maximum,
                maximum: :number clamped between 0 and :infinity,
                percentage: :percentage computed by
                    0% when maximum <= 0,
                    otherwise (current / maximum) as :percentage
            }

            on Start {
                let hp be :gauge(current: 125, maximum: 100)
                emit Done(current: hp.current, maximum: hp.maximum, percentage: hp.percentage, isGauge: hp is :gauge)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(100), emitted[0].Arguments["current"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(100), emitted[0].Arguments["maximum"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesPercentage(0.01), emitted[0].Arguments["percentage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["isGauge"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForExternalTypeConstructor()
    {
        var registry = GameEventScriptExternalTypeRegistry.Create(typeof(TestAimValue));
        const string script =
            """
            module BinaryCompilerExternalType

            on Start {
                let aim be :aim(range: 12m, bearing: 90°, steps: 4m, direction: :vector(1m, 2m, 3m))
                emit Done(isAim: aim is :aim, bearing: aim.bearing, range: aim.range, steps: aim.steps, directionZ: aim.direction.z, checksum: aim.checksum)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .WithExternalTypes(registry)
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithExternalTypes(registry)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["isAim"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(90, GameEventScriptBytecodeInstructionUnit.UnitDegree), emitted[0].Arguments["bearing"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12, GameEventScriptBytecodeInstructionUnit.UnitMeter), emitted[0].Arguments["range"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(4, GameEventScriptBytecodeInstructionUnit.UnitMeter), emitted[0].Arguments["steps"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3, GameEventScriptBytecodeInstructionUnit.UnitMeter), emitted[0].Arguments["directionZ"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(106), emitted[0].Arguments["checksum"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForStandardAndExternalCalls()
    {
        var externalTypes = GameEventScriptExternalTypeRegistry.Create(typeof(TestAimValue));
        var extensions = GameEventScriptExtensionRegistry.Create(typeof(TestExtensionFunctions));
        const string script =
            """
            module BinaryCompilerExtensionCalls

            on Start {
                let aim be :aim(range: 12m, bearing: 90°, steps: 4m, direction: :vector(1m, 2m, 3m))
                let floored be :integer.floor 10.75
                let score be :test.score aim
                let lead be :test.lead heading: 90°
                let normalizedPredicate be 10 is :test.isPositive
                emit Done(floored: floored, score: score, lead: lead, normalizedPredicate: normalizedPredicate)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .WithExternalTypes(externalTypes)
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithExternalTypes(externalTypes)
            .WithRegistry(extensions)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), emitted[0].Arguments["floored"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(106), emitted[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(95d, GameEventScriptBytecodeInstructionUnit.UnitDegree), emitted[0].Arguments["lead"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["normalizedPredicate"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForHandlerLiteralBinding()
    {
        const string script =
            """
            module BinaryCompilerHandlerBinding

            on Start(unit, target, hp) {
                let shoot be Shoot(unit, target)
                let msg be shoot(unit: unit, target: target)
                let invalid be shoot(unit: unit, hp: hp)
                emit msg
                emit invalid
                emit Done
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create(
            "Start",
            ("unit", GameEventScriptValueFactory.GesText("u_1")),
            ("target", GameEventScriptValueFactory.GesText("t_1")),
            ("hp", GameEventScriptValueFactory.GesInteger(10))));

        Assert.IsTrue(handled);
        Assert.HasCount(2, emitted);
        Assert.AreEqual("Shoot", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("u_1"), emitted[0].Arguments["unit"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("t_1"), emitted[0].Arguments["target"]);
        Assert.AreEqual("Done", emitted[1].Name);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForMessageNameHandlerAndTags()
    {
        const string script =
            """
            module BinaryCompilerMessageNameHandler

            on Start {
                emit Ping(amount: 7, kind: :fire) with :radio, :urgent
            }

            on Ping as message {
                emit Done(name: message.name, signature: message.signature, amount: message.arguments.amount, kind: message.arguments.kind, tagCount: :len message.tags, firstTag: message.tags[1], secondTag: message.tags[2], isMessage: message is :message)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(2, emitted);
        Assert.AreEqual("Ping", emitted[0].Name);
        Assert.AreEqual("Done", emitted[1].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Ping"), emitted[1].Arguments["name"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Ping(amount,kind)"), emitted[1].Arguments["signature"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), emitted[1].Arguments["amount"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("fire"), emitted[1].Arguments["kind"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[1].Arguments["tagCount"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("radio"), emitted[1].Arguments["firstTag"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("urgent"), emitted[1].Arguments["secondTag"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[1].Arguments["isMessage"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForPublishAndMessageValueTags()
    {
        const string script =
            """
            module BinaryCompilerPublish

            on Start {
                let dynamicTags be [:radio, :relay]
                let relay be Relay(amount: 5)
                emit relay with dynamicTags
                publish Remote(amount: 6) with :network
                publish relay with :copy
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithPublishHook(message =>
            {
                published.Add(message);
                return true;
            })
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(messageEmitted: emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Relay", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(5), emitted[0].Arguments["amount"]);
        CollectionAssert.AreEqual(new[] { "radio", "relay" }, emitted[0].Tags.ToArray());

        Assert.HasCount(2, published);
        Assert.AreEqual("Remote", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[0].Arguments["amount"]);
        CollectionAssert.AreEqual(new[] { "network" }, published[0].Tags.ToArray());
        Assert.AreEqual("Relay", published[1].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(5), published[1].Arguments["amount"]);
        CollectionAssert.AreEqual(new[] { "copy" }, published[1].Tags.ToArray());
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForForLoops()
    {
        const string script =
            """
            module BinaryCompilerFor

            on Start {
                for item in [1, 2] {
                    emit Item(kind: 'list', value: item)
                }

                for item from 3 to 5 step 2 {
                    emit Item(kind: 'range', value: item)
                }

                emit Done
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(5, emitted);
        Assert.AreEqual("Item", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("list"), emitted[0].Arguments["kind"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), emitted[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[1].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("range"), emitted[2].Arguments["kind"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), emitted[2].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(5), emitted[3].Arguments["value"]);
        Assert.AreEqual("Done", emitted[4].Name);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForGeneratedLists()
    {
        const string script =
            """
            module BinaryCompilerGeneratedList

            on Start {
                let values be :list[:select item from 1 to 5 where item mod 2 = 1 => item * 10]
                emit Done(count: :len values, first: values[1], second: values[2], third: values[3])
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), emitted[0].Arguments["count"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), emitted[0].Arguments["first"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(30), emitted[0].Arguments["second"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(50), emitted[0].Arguments["third"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForGuardedChoices()
    {
        const string script =
            """
            module BinaryCompilerGuardedChoice

            on Start(value) {
                let result be :large when value > 10, :medium when value > 5 otherwise :small
                emit Done(result: result)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        Assert.IsTrue(host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(12)))));
        Assert.IsTrue(host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(7)))));
        Assert.IsTrue(host.PublishToCompletion(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(3)))));

        Assert.HasCount(3, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("large"), emitted[0].Arguments["result"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("medium"), emitted[1].Arguments["result"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("small"), emitted[2].Arguments["result"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForDirectDrawAndChooseSelectors()
    {
        const string script =
            """
            module BinaryCompilerDirectChoose

            on Start {
                let values be [1, 2, 3]
                let oneValue be values[:draw 1]
                let drawnValues be values[:draw 2]
                let chosenOne be values[:choose 1]
                let chosenValues be values[:choose 2]
                let randomOne be [42][:choose 1 at random]
                let randomValues be [7, 8][:choose 2 at random]
                emit Done(oneValue: oneValue, drawnLen: :len drawnValues, drawnSecond: drawnValues[2], chosenOne: chosenOne, chosenLen: :len chosenValues, randomOne: randomOne, randomLen: :len randomValues)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), emitted[0].Arguments["oneValue"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["drawnLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["drawnSecond"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), emitted[0].Arguments["chosenOne"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["chosenLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(42), emitted[0].Arguments["randomOne"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["randomLen"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForDirectPatternSelectors()
    {
        const string script =
            """
            module BinaryCompilerDirectPatterns

            on Start {
                let dice as :dice be [6, 6, 5, 5, 5]
                let straight as :dice be [6, 5, 4, 3, 2]
                let list be ['a', 'b', 'a']
                let dicePair be dice[:take pair]
                let diceFull be dice[:take full house]
                let straightTaken be straight[:take straight]
                let listPair be list[:take pair]
                let tags be [:fire, :ice, :fire]
                let dicePairOfSix be dice[:take pair of 6]
                let listPairOfA be list[:take pair of 'a']
                let tagPairOfFire be tags[:take pair of :fire]
                emit Done(hasDicePair: dice[:has pair], hasListPair: list[:has pair], hasPairOfSix: dice[:has pair of 6], hasPairOfFour: dice[:has pair of 4], pairLen: :len dicePair, fullLen: :len diceFull, straightLen: :len straightTaken, listPairLen: :len listPair, pairOfSixLen: :len dicePairOfSix, pairOfAFirst: listPairOfA[1], tagPairSecond: tagPairOfFire[2])
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["hasDicePair"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["hasListPair"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["hasPairOfSix"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(false), emitted[0].Arguments["hasPairOfFour"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["pairLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(5), emitted[0].Arguments["fullLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(5), emitted[0].Arguments["straightLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["listPairLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["pairOfSixLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("a"), emitted[0].Arguments["pairOfAFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("fire"), emitted[0].Arguments["tagPairSecond"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForDirectSortGroupDistinctSelectors()
    {
        const string script =
            """
            module BinaryCompilerDirectSortGroupDistinct

            on Start {
                let units be [[name: 'Knight', faction: :melee, hp: 10], [name: 'Rook', faction: :melee, hp: 8], [name: 'Archer', faction: :ranged, hp: 6]]
                let firstFaction be units[1].faction
                let thirdFaction be units[3].faction
                let distinctFactions be units[:distinct by unit => unit.faction]
                let groups be units[:group by unit => unit.faction]
                let orderedAscending be units[:order by unit => unit.hp ascending]
                let orderedDescending be units[:order by unit => unit.hp descending]
                emit Done(firstFaction: firstFaction, thirdFaction: thirdFaction, distinctLen: :len distinctFactions, distinctFirst: distinctFactions[1].name, distinctSecond: distinctFactions[2].name, meleeLen: :len groups[:melee], rangedFirst: groups[:ranged][1].name, ascFirst: orderedAscending[1].name, descFirst: orderedDescending[1].name)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("melee"), emitted[0].Arguments["firstFaction"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("ranged"), emitted[0].Arguments["thirdFaction"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["distinctLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Knight"), emitted[0].Arguments["distinctFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Archer"), emitted[0].Arguments["distinctSecond"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["meleeLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Archer"), emitted[0].Arguments["rangedFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Archer"), emitted[0].Arguments["ascFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Knight"), emitted[0].Arguments["descFirst"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForFilterAndSelectSelectors()
    {
        const string script =
            """
            module BinaryCompilerFilterSelect

            on Start {
                let values be [1, 2, 3, 4]
                let threshold be 2
                let base be 100
                let filtered be values[:filter value where value > 2]
                let selected be values[:select value => value * 10]
                let capturedFilter be values[:filter value where value > threshold]
                let capturedSelect be values[:select value => value + base]
                let chained be values[:filter value where value > 1][:select value => value + 5]
                emit Done(filteredLen: :len filtered, filteredFirst: filtered[1], selectedSecond: selected[2], capturedFilterFirst: capturedFilter[1], capturedSelectThird: capturedSelect[3], chainedLen: :len chained, chainedFirst: chained[1], chainedThird: chained[3])
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["filteredLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), emitted[0].Arguments["filteredFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(20), emitted[0].Arguments["selectedSecond"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), emitted[0].Arguments["capturedFilterFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(103), emitted[0].Arguments["capturedSelectThird"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), emitted[0].Arguments["chainedLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), emitted[0].Arguments["chainedFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(9), emitted[0].Arguments["chainedThird"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForStreamTerminalSelectors()
    {
        const string script =
            """
            module BinaryCompilerStreamTerminals

            on Start {
                let values be [1, 2, 3, 4]
                let threshold be 2
                let offset be 10
                let units be [[name: 'Knight', hp: 10], [name: 'Mage', hp: 6], [name: 'Guard', hp: 8]]
                let counted be values[:count value where value > threshold]
                let summed be values[:sum value => value + offset]
                let averaged be values[:average value => value]
                let chainedSum be values[:filter value where value > 1][:sum value => value]
                let weakest be units[:min unit => unit.hp]
                let strongest be units[:max unit => unit.hp + offset]
                emit Done(counted: counted, summed: summed, averaged: averaged, chainedSum: chainedSum, weakest: weakest.name, strongest: strongest.name)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["counted"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(50), emitted[0].Arguments["summed"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesFloat(2.5), emitted[0].Arguments["averaged"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(9), emitted[0].Arguments["chainedSum"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Mage"), emitted[0].Arguments["weakest"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Knight"), emitted[0].Arguments["strongest"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForPredicateAndFilteredEdgeSelectors()
    {
        const string script =
            """
            module BinaryCompilerPredicateEdges

            on Start {
                let values be [1, 2, 3, 4]
                let threshold be 2
                let units be [[name: 'Knight', alive: true, role: :fighter], [name: 'Mage', alive: false, role: :boss], [name: 'Guard', alive: true, role: :fighter]]
                let hasHigh be values[:any value where value > threshold]
                let allPositive be values[:all value where value > 0]
                let firstAlive be units[:first unit where unit.alive]
                let lastAlive be units[:last unit where unit.alive]
                let boss be units[:single unit where unit.role = :boss]
                emit Done(hasHigh: hasHigh, allPositive: allPositive, firstAlive: firstAlive.name, lastAlive: lastAlive.name, boss: boss.name)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["hasHigh"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["allPositive"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Knight"), emitted[0].Arguments["firstAlive"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Guard"), emitted[0].Arguments["lastAlive"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Mage"), emitted[0].Arguments["boss"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForObjectMatchSelector()
    {
        const string script =
            """
            module BinaryCompilerObjectMatch

            on Start {
                let targetRole be :boss
                let units be [[name: 'Knight', role: :fighter, stats: [hp: 10]], [name: 'Mage', role: :boss, stats: [hp: 6]], [name: 'Guard', role: :fighter, stats: [hp: 8]]]
                let hasBoss be units[:has [role: targetRole]]
                let hasNestedHp be units[:has [stats: [hp: 8]]]
                let hasMissing be units[:has [role: :healer]]
                emit Done(hasBoss: hasBoss, hasNestedHp: hasNestedHp, hasMissing: hasMissing)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["hasBoss"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), emitted[0].Arguments["hasNestedHp"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(false), emitted[0].Arguments["hasMissing"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForMapSelector()
    {
        const string script =
            """
            module BinaryCompilerMapSelector

            on Start {
                let units be [[id: :rook, hp: 10, team: :blue], [id: :mage, hp: 6, team: :red], [id: :guard, hp: 8, team: :blue]]
                let byId be units[:map unit by unit.id]
                let hpById be units[:map unit by unit.id => unit.hp]
                let blueById be units[:filter unit where unit.team = :blue][:map unit by unit.id => unit.hp]
                emit Done(byIdLen: :len byId, rookTeam: byId[:rook].team, mageHp: byId[:mage].hp, hpRook: hpById[:rook], hpMage: hpById[:mage], blueLen: :len blueById, blueGuard: blueById[:guard])
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), emitted[0].Arguments["byIdLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesTag("blue"), emitted[0].Arguments["rookTeam"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), emitted[0].Arguments["mageHp"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), emitted[0].Arguments["hpRook"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), emitted[0].Arguments["hpMage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), emitted[0].Arguments["blueLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(8), emitted[0].Arguments["blueGuard"]);
    }

    [TestMethod]
    public void CompileBuildsRunnableBinaryForFilteredAndWeightedChooseSelectors()
    {
        const string script =
            """
            module BinaryCompilerChooseSelectors

            on Start {
                let scale be 2
                let units be [[name: 'Low', weight: 0, team: :red], [name: 'High', weight: 3, team: :blue], [name: 'Zero', weight: 0, team: :blue]]
                let chosenBlue be units[:choose 1 unit where unit.team = :blue]
                let chosenRandomBlue be units[:choose 1 at random unit where unit.weight > 0]
                let weightedOne be units[:choose 1 weighted by unit => unit.weight * scale]
                let weightedMany be units[:choose 2 weighted by unit => unit.weight * scale]
                let weightedFiltered be units[:choose 2 unit where unit.team = :blue weighted by unit => unit.weight * scale]
                emit Done(chosenBlue: chosenBlue.name, chosenRandomBlue: chosenRandomBlue.name, weightedOne: weightedOne.name, weightedManyLen: :len weightedMany, weightedManyFirst: weightedMany[1].name, weightedFilteredLen: :len weightedFiltered, weightedFilteredFirst: weightedFiltered[1].name)
            }
            """;

        var module = GameEventScriptBuilder.Create()
            .AddScript(script)
            .BuildModule();
        var binary = GesBinaryCompiler.Compile(module);
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(emitted.Add))
            .Build()
            .Load(GameEventScriptManager.CreateModule(binary));

        var handled = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(handled);
        Assert.HasCount(1, emitted);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("High"), emitted[0].Arguments["chosenBlue"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("High"), emitted[0].Arguments["chosenRandomBlue"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("High"), emitted[0].Arguments["weightedOne"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), emitted[0].Arguments["weightedManyLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("High"), emitted[0].Arguments["weightedManyFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), emitted[0].Arguments["weightedFilteredLen"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("High"), emitted[0].Arguments["weightedFilteredFirst"]);
    }

    [GesType("aim")]
    private sealed class TestAimValue
    {
        [GesConstruct]
        public TestAimValue(
            [GesParam("bearing", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitDegree)] double bearing,
            [GesParam("range", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitMeter)] double range,
            [GesParam("steps", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitMeter)] int steps,
            [GesParam("direction", GameEventScriptValueKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)] GameEventScriptVectorValue direction)
        {
            Bearing = bearing;
            Range = range;
            Steps = steps;
            Direction = direction;
            Checksum = (int)(bearing + range + steps);
        }

        [GesField("bearing", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitDegree)]
        public double Bearing { get; }

        [GesField("range", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public double Range { get; }

        [GesField("steps", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public int Steps { get; }

        [GesField("direction", GameEventScriptValueKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public GameEventScriptVectorValue Direction { get; }

        [GesField("checksum", GameEventScriptValueKind.Number)]
        public int Checksum { get; }
    }

    [GesExtension("test")]
    private static class TestExtensionFunctions
    {
        [GesFunction("score", GameEventScriptValueKind.Number)]
        public static long Score([GesParam("_", "aim")] TestAimValue aim) => aim.Checksum;

        [GesFunction("lead", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitDegree)]
        public static double Lead([GesParam("heading", GameEventScriptValueKind.Number, GameEventScriptBytecodeInstructionUnit.UnitDegree)] double heading)
            => heading + 5d;

        [GesFunction("isPositive", GameEventScriptValueKind.Boolean)]
        public static bool IsPositive([GesParam("_", GameEventScriptValueKind.Number)] long value) => value > 0;
    }
}
