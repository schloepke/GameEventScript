using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Interpreter;

[TestClass]
public class EventScriptRuntimeScenarios
{
    [TestMethod]
    public void ThresholdChecksCanPublishPassedWithTheCollectionSize()
    {
        const string script = """
            on Start(values, threshold) {
                let passed be values[:any value where value > threshold];
                if passed {
                    publish Passed(arg1: :len values);
                } else {
                    publish Failed;
                }
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Start", EventScriptValue.List([1m, 5m, 9m]), 7m);

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Passed", result.EmittedEvents[0].Message);
        Assert.AreEqual(3, Convert.ToInt32(result.EmittedEvents[0].Arguments[0].AsInteger()));
    }

    [TestMethod]
    public void RandomAndDiceOutcomesCanBeMadeDeterministic()
    {
        const string script = """
            on Roll {
                let a be :random 1 to 6;
                let b be :dice 4d6[:take highest 2];
                publish Result(arg1: a, arg2: b);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(4, 1, 6, 3, 5);
        var interpreter = EventScriptManager.Compile(script);

        var result = interpreter.InvokePositional(random, "Roll");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(4, Convert.ToInt32(args[0].AsInteger()));
        Assert.AreEqual(11, Convert.ToInt32(args[1].AsInteger()));
    }

    [TestMethod]
    public void KeysAndValuesExposeIteratorWrappersForImperativeIteration()
    {
        const string script = """
            on Start(player, values, maybeTarget) {
                let dictKeys be :keys player;
                let valueIterator be :values values;
                let maybeValues be :values maybeTarget;
                let entries be :entries player;
                let keyCount be :len dictKeys;
                let valueCount be :len valueIterator;
                let maybeCount be :len maybeValues;
                let entryCount be :len entries;
                for key in dictKeys {
                    publish SeenKey(arg1: key, arg2: player[key]);
                }

                for item in valueIterator {
                    publish SeenValue(arg1: item);
                }

                for target in maybeValues {
                    publish SeenMaybe(arg1: target);
                }

                for entry in entries {
                    publish SeenEntry(arg1: entry.key, arg2: entry[:value]);
                }

                publish Done(arg1: keyCount, arg2: valueCount, arg3: maybeCount, arg4: entryCount, arg5: dictKeys, arg6: valueIterator, arg7: entries);
            }
            """;

        var player = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = "Mark",
            ["age"] = 25m
        });
        var values = EventScriptValue.List([10m, 20m, 30m]);
        var maybeTarget = EventScriptValue.OptionalSome(EventScriptValue.Text("boss"));

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Start", player, values, maybeTarget);

        Assert.AreEqual("SeenKey", result.EmittedEvents[0].Message);
        Assert.AreEqual(EventScriptValueType.Tag, result.EmittedEvents[0].Arguments[0].Type);
        Assert.AreEqual("age", result.EmittedEvents[0].Arguments[0].AsText());
        Assert.AreEqual(25m, result.EmittedEvents[0].Arguments[1].AsNumber());

        Assert.AreEqual("SeenKey", result.EmittedEvents[1].Message);
        Assert.AreEqual("name", result.EmittedEvents[1].Arguments[0].AsText());
        Assert.AreEqual("Mark", result.EmittedEvents[1].Arguments[1].AsText());

        Assert.AreEqual("SeenValue", result.EmittedEvents[2].Message);
        Assert.AreEqual(10m, result.EmittedEvents[2].Arguments[0].AsNumber());
        Assert.AreEqual("SeenValue", result.EmittedEvents[4].Message);
        Assert.AreEqual(30m, result.EmittedEvents[4].Arguments[0].AsNumber());

        Assert.AreEqual("SeenMaybe", result.EmittedEvents[5].Message);
        Assert.AreEqual("boss", result.EmittedEvents[5].Arguments[0].AsText());

        Assert.AreEqual("SeenEntry", result.EmittedEvents[6].Message);
        Assert.AreEqual("age", result.EmittedEvents[6].Arguments[0].AsText());
        Assert.AreEqual(25m, result.EmittedEvents[6].Arguments[1].AsNumber());
        Assert.AreEqual("SeenEntry", result.EmittedEvents[7].Message);
        Assert.AreEqual("name", result.EmittedEvents[7].Arguments[0].AsText());
        Assert.AreEqual("Mark", result.EmittedEvents[7].Arguments[1].AsText());

        var done = result.EmittedEvents[8].Arguments;
        Assert.AreEqual(2L, done[0].AsInteger());
        Assert.AreEqual(3L, done[1].AsInteger());
        Assert.AreEqual(1L, done[2].AsInteger());
        Assert.AreEqual(2L, done[3].AsInteger());
        Assert.AreEqual(EventScriptValueType.Iterator, done[4].Type);
        Assert.AreEqual(EventScriptValueType.Iterator, done[5].Type);
        Assert.AreEqual(EventScriptValueType.Iterator, done[6].Type);
    }

    [TestMethod]
    public void ChanceChecksAndWeightedChoicesCanBeDeterministic()
    {
        const string script = """
            on Start(units) {
                let alwaysHit be :chance 100%;
                let neverHit be :chance 0%;
                let quarterHit be :chance 25%;
                let weightedTarget be units[:choose 1 weighted by unit -> unit.weight];
                let weightedPair be units[:choose 2 weighted by unit -> unit.weight];
                publish Done(arg1: alwaysHit, arg2: neverHit, arg3: quarterHit, arg4: weightedTarget.name, arg5: weightedPair[1].name, arg6: weightedPair[2].name);
            }
            """;

        var units = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Goblin", ["weight"] = 1m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["weight"] = 3m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Dragon", ["weight"] = 6m })
        ]);

        var random = EventScriptRandomGenerator.FromSequence(0.249999m, 9m, 0m, 8.99999m);
        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional(random, "Start", units).EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsFalse(args[1].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
        Assert.AreEqual("Dragon", args[3].AsText());
        Assert.AreEqual("Goblin", args[4].AsText());
        Assert.AreEqual("Dragon", args[5].AsText());
    }

    [TestMethod]
    public void DictionarySelectorsCanBuildLookupObjectsWithLastWinsSemantics()
    {
        const string script = """
            on Start(items) {
                let byId be items[:dictionary item by item.id];
                let namesById be items[:dictionary item by item.id -> item.name];
                publish Done(arg1: byId[:orc].hp, arg2: byId[:mage].hp, arg3: namesById[:orc], arg4: namesById[:mage], arg5: :len byId);
            }
            """;

        var items = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["id"] = EventScriptValue.Tag("orc"), ["name"] = "Orc Grunt", ["hp"] = 4m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["id"] = EventScriptValue.Tag("mage"), ["name"] = "Mage", ["hp"] = 7m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["id"] = EventScriptValue.Tag("orc"), ["name"] = "Orc Chief", ["hp"] = 9m })
        ]);

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional("Start", items).EmittedEvents[0].Arguments;

        Assert.AreEqual(9m, args[0].AsNumber());
        Assert.AreEqual(7m, args[1].AsNumber());
        Assert.AreEqual("Orc Chief", args[2].AsText());
        Assert.AreEqual("Mage", args[3].AsText());
        Assert.AreEqual(2L, args[4].AsInteger());
    }

    [TestMethod]
    public void CollectionsCanBeProjectedFilteredAndSummed()
    {
        const string script = """
            on Compute(items) {
                let names be items[:select item -> item.name];
                let positive be items[:filter item where item.points > 0];
                let total be items[:sum item -> item.points];
                let positiveCount be items[:count item where item.points > 0];
                let averagePoints be items[:average item -> item.points];
                let highestItem be items[:highest item -> item.points];
                let lowestItem be items[:lowest item -> item.points];
                publish Done(arg1: :len names, arg2: :len positive, arg3: total, arg4: positiveCount, arg5: averagePoints, arg6: highestItem.name, arg7: lowestItem.name);
            }
            """;

        var items = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "a", ["points"] = 3m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "b", ["points"] = -1m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "c", ["points"] = 4m })
        ]);

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Compute", items);

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(3, Convert.ToInt32(result.EmittedEvents[0].Arguments[0].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(result.EmittedEvents[0].Arguments[1].AsInteger()));
        Assert.AreEqual(6m, result.EmittedEvents[0].Arguments[2].AsNumber());
        Assert.AreEqual(2, Convert.ToInt32(result.EmittedEvents[0].Arguments[3].AsInteger()));
        Assert.AreEqual(2m, result.EmittedEvents[0].Arguments[4].AsNumber());
        Assert.AreEqual("c", result.EmittedEvents[0].Arguments[5].AsText());
        Assert.AreEqual("b", result.EmittedEvents[0].Arguments[6].AsText());
    }

    [TestMethod]
    public void CollectionsCanChooseDrawAndShuffleWithoutMutation()
    {
        const string script = """
            on Start(enemies) {
                let cards be [1, 2, 3, 4, 5][:shuffle];
                let hand be cards[:draw 3];
                let restCards be cards[:drop first 3];
                let target be enemies[:choose 1 enemy where enemy.alive];
                let randomTargets be enemies[:choose 2 at random enemy where enemy.alive];
                publish Done(arg1: hand[1], arg2: hand[2], arg3: hand[3], arg4: restCards[1], arg5: restCards[2], arg6: target.name, arg7: randomTargets[1].name, arg8: randomTargets[2].name);
            }
            """;

        var enemies = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Goblin", ["alive"] = false }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Orc", ["alive"] = true }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Mage", ["alive"] = true }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["alive"] = true })
        ]);

        var random = EventScriptRandomGenerator.FromSequence(1, 0, 1, 1, 0, 1);
        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional(random, "Start", enemies).EmittedEvents[0].Arguments;

        Assert.AreEqual(4m, args[0].AsNumber());
        Assert.AreEqual(3m, args[1].AsNumber());
        Assert.AreEqual(5m, args[2].AsNumber());
        Assert.AreEqual(1m, args[3].AsNumber());
        Assert.AreEqual(2m, args[4].AsNumber());
        Assert.AreEqual("Orc", args[5].AsText());
        Assert.AreEqual("Orc", args[6].AsText());
        Assert.AreEqual("Knight", args[7].AsText());
    }

    [TestMethod]
    public void ValueOperatorsCanClampAbsAndCompareExtremes()
    {
        const string script = """
            on Start {
                let distance be :abs (0 - 12.5);
                let boundedHigh be :clamp 120 between 0 and 99;
                let boundedLow be :clamp (0 - 3) between 0 and 99;
                let highest be :max of 4 and 9 and 2;
                let lowest be :min of 4 and 9 and 2;
                publish Done(arg1: distance, arg2: boundedHigh, arg3: boundedLow, arg4: highest, arg5: lowest);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.Invoke("Start").EmittedEvents[0].Arguments;

        Assert.AreEqual(12.5m, args[0].AsNumber());
        Assert.AreEqual(99m, args[1].AsNumber());
        Assert.AreEqual(0m, args[2].AsNumber());
        Assert.AreEqual(9m, args[3].AsNumber());
        Assert.AreEqual(2m, args[4].AsNumber());
    }

    [TestMethod]
    public void PercentageValuesAndCustomMeterTypesCanBeUsedInScripts()
    {
        const string script = """
            record :meter as {
                current: :decimal clamped between 0 and maximum,
                maximum: :decimal clamped between 0 and :infinity,
                percentage: :percentage computed by
                    0% when maximum <= 0,
                    otherwise (current / maximum) as :percentage
            }

            on Start {
                let ratio as :percentage be 0.75;
                let percent as :percentage be 75;
                let hp as :meter be [current: 125, maximum: 100];
                let stalled as :meter be [current: 10, maximum: 0];
                publish Done(arg1: ratio, arg2: ratio as :decimal, arg3: ratio as :integer, arg4: percent, arg5: hp[:current], arg6: hp[:maximum], arg7: hp[:percentage], arg8: hp is :meter, arg9: stalled[:percentage]);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.Invoke("Start").EmittedEvents[0].Arguments;

        Assert.AreEqual("75%", args[0].AsText());
        Assert.AreEqual(0.75m, args[1].AsNumber());
        Assert.AreEqual(75L, args[2].AsInteger());
        Assert.AreEqual("75%", args[3].AsText());
        Assert.AreEqual(100m, args[4].AsNumber());
        Assert.AreEqual(100m, args[5].AsNumber());
        Assert.AreEqual("100%", args[6].AsText());
        Assert.IsTrue(args[7].AsBoolean());
        Assert.AreEqual("0%", args[8].AsText());
    }

    [TestMethod]
    public void CollectionsCanReturnProjectedMinimumAndMaximumItems()
    {
        const string script = """
            on Start(units) {
                let weakest be units[:min unit -> unit.hp];
                let fastest be units[:max unit -> unit.speed];
                publish Done(arg1: weakest.name, arg2: fastest.name);
            }
            """;

        var units = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["hp"] = 10m, ["speed"] = 3m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Mage", ["hp"] = 4m, ["speed"] = 5m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Orc", ["hp"] = 8m, ["speed"] = 2m })
        ]);

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional("Start", units).EmittedEvents[0].Arguments;

        Assert.AreEqual("Mage", args[0].AsText());
        Assert.AreEqual("Mage", args[1].AsText());
    }

    [TestMethod]
    public void CollectionsCanCombineIntersectExceptAndZip()
    {
        const string script = """
            on Start {
                let appended be [1, 2, 3] + 4;
                let prepended be 0 + [1, 2, 3];
                let mergedList be [1, 2, 3] + [4, 5];
                let commonList be [1, 2, 2, 3] :intersect [2, 2, 4];
                let leftOnly be [1, 2, 2, 3] :except [2];
                let mergedSet be :set[1, 2] :combine :set[2, 3];
                let commonSet be :set[1, 2, 3] :intersect :set[2, 4];
                let dictA be [name: 'Mark', age: 32];
                let dictB be [city: 'Somewhere', age: 33];
                let mergedDict be dictA :combine dictB;
                let mergedDictByMerge be dictA :merge dictB;
                let mergedDictByPlus be dictA + dictB;
                let zipped be ['a', 'b', 'c'] :zip [1, 2];
                publish Done(arg1: appended[4], arg2: prepended[1], arg3: mergedList[5], arg4: commonList[1], arg5: commonList[2], arg6: :len leftOnly, arg7: leftOnly[2], arg8: 3 in mergedSet, arg9: 2 in commonSet, arg10: mergedDict.age, arg11: mergedDict.city, arg12: mergedDictByMerge.age, arg13: mergedDictByPlus.age, arg14: zipped[1].left, arg15: zipped[1].right, arg16: zipped[2].left, arg17: zipped[2].right, arg18: :len zipped);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.Invoke("Start").EmittedEvents[0].Arguments;

        Assert.AreEqual(4m, args[0].AsNumber());
        Assert.AreEqual(0m, args[1].AsNumber());
        Assert.AreEqual(5m, args[2].AsNumber());
        Assert.AreEqual(2m, args[3].AsNumber());
        Assert.AreEqual(2m, args[4].AsNumber());
        Assert.AreEqual(3L, args[5].AsInteger());
        Assert.AreEqual(2m, args[6].AsNumber());
        Assert.IsTrue(args[7].AsBoolean());
        Assert.IsTrue(args[8].AsBoolean());
        Assert.AreEqual(33m, args[9].AsNumber());
        Assert.AreEqual("Somewhere", args[10].AsText());
        Assert.AreEqual(33m, args[11].AsNumber());
        Assert.AreEqual(33m, args[12].AsNumber());
        Assert.AreEqual("a", args[13].AsText());
        Assert.AreEqual(1m, args[14].AsNumber());
        Assert.AreEqual("b", args[15].AsText());
        Assert.AreEqual(2m, args[16].AsNumber());
        Assert.AreEqual(2L, args[17].AsInteger());
    }

    [TestMethod]
    public void ListsCanBeSortedInEitherDirection()
    {
        const string script = """
            on Sort(values, units) {
                let ascendingValues be values[:sort ascending];
                let descendingValues be values[:sort descending];
                let byPriority be units[:order by item -> item.priority descending];
                publish Done(arg1: ascendingValues[1], arg2: ascendingValues[2], arg3: ascendingValues[3], arg4: descendingValues[1], arg5: descendingValues[2], arg6: descendingValues[3], arg7: byPriority[1].name, arg8: byPriority[2].name, arg9: byPriority[3].name);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Sort",
            EventScriptValue.List([3m, 1m, 2m]),
            EventScriptValue.List([
                EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "a", ["priority"] = 2m }),
                EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "b", ["priority"] = 3m }),
                EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "c", ["priority"] = 1m })
            ]));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(1m, args[0].AsNumber());
        Assert.AreEqual(2m, args[1].AsNumber());
        Assert.AreEqual(3m, args[2].AsNumber());
        Assert.AreEqual(3m, args[3].AsNumber());
        Assert.AreEqual(2m, args[4].AsNumber());
        Assert.AreEqual(1m, args[5].AsNumber());
        Assert.AreEqual("b", args[6].AsText());
        Assert.AreEqual("a", args[7].AsText());
        Assert.AreEqual("c", args[8].AsText());
    }

    [TestMethod]
    public void CollectionsCanReadEdgesDistinctValuesAndGroups()
    {
        const string script = """
            on Start(values, units) {
                let firstValue be values[:first];
                let lastValue be values[:last];
                let firstAlive be units[:first unit where unit.alive];
                let lastAlive be units[:last unit where unit.alive];
                let singleBoss be units[:single unit where unit.role = 'boss'];
                let missingBoss be units[:single unit where unit.role = :missing];
                let distinctValues be values[:distinct];
                let distinctFactions be units[:distinct by unit -> unit.faction];
                let groups be units[:group by unit -> unit.faction];
                publish Done(arg1: firstValue, arg2: lastValue, arg3: firstAlive.name, arg4: lastAlive.name, arg5: singleBoss.name, arg6: missingBoss, arg7: :len distinctValues, arg8: distinctFactions[1].name, arg9: distinctFactions[2].name, arg10: groups[:melee][1].name, arg11: groups[:melee][2].name, arg12: groups[:ranged][1].name);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.List([3m, 3m, 1m, 2m, 2m]),
            EventScriptValue.List([
                EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["faction"] = "melee", ["alive"] = true, ["role"] = "tank" }),
                EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Orc", ["faction"] = "melee", ["alive"] = false, ["role"] = "bruiser" }),
                EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Archer", ["faction"] = "ranged", ["alive"] = true, ["role"] = "boss" }),
                EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Hunter", ["faction"] = "ranged", ["alive"] = true, ["role"] = "scout" })
            ]));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(3m, args[0].AsNumber());
        Assert.AreEqual(2m, args[1].AsNumber());
        Assert.AreEqual("Knight", args[2].AsText());
        Assert.AreEqual("Hunter", args[3].AsText());
        Assert.AreEqual("Archer", args[4].AsText());
        Assert.IsTrue(args[5].isNothing());
        Assert.AreEqual(3L, args[6].AsInteger());
        Assert.AreEqual("Knight", args[7].AsText());
        Assert.AreEqual("Archer", args[8].AsText());
        Assert.AreEqual("Knight", args[9].AsText());
        Assert.AreEqual("Orc", args[10].AsText());
        Assert.AreEqual("Archer", args[11].AsText());
    }

    [TestMethod]
    public void SortingCanOrderListsDiceAndSetsDeterministically()
    {
        const string script = """
            on SortAll(values, tags, rolled) {
                let sortedValues be values[:sort ascending];
                let sortedTags be tags[:sort descending];
                let sortedRoll be rolled[:sort ascending];
                publish Done(arg1: sortedValues[1], arg2: sortedValues[2], arg3: sortedValues[3], arg4: sortedTags[1], arg5: sortedTags[2], arg6: sortedTags[3], arg7: sortedRoll[1], arg8: sortedRoll[2], arg9: sortedRoll[3], arg10: sortedRoll[4]);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "SortAll",
            EventScriptValue.List([3m, 1m, 2m]),
            EventScriptValue.Set([EventScriptValue.Text("beta"), EventScriptValue.Text("alpha"), EventScriptValue.Text("gamma")]),
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 6, 4, 2, 1 })));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(1m, args[0].AsNumber());
        Assert.AreEqual(2m, args[1].AsNumber());
        Assert.AreEqual(3m, args[2].AsNumber());
        Assert.AreEqual("gamma", args[3].AsText());
        Assert.AreEqual("beta", args[4].AsText());
        Assert.AreEqual("alpha", args[5].AsText());
        Assert.AreEqual(1L, args[6].AsInteger());
        Assert.AreEqual(2L, args[7].AsInteger());
        Assert.AreEqual(4L, args[8].AsInteger());
        Assert.AreEqual(6L, args[9].AsInteger());
    }

    [TestMethod]
    public void ShuffleAndDrawAreRestrictedToOrderedCollections()
    {
        const string script = """
            on Start {
                let tags be :set['alpha', 'beta', 'gamma'];
                let shuffledTags be tags[:shuffle];
                let drawnTag be tags[:draw 1];
                let diceRoll be :dice 3d6;
                let shuffledDice be diceRoll[:shuffle];
                let drawnDice be diceRoll[:draw 2];
                publish Done(arg1: shuffledTags, arg2: drawnTag, arg3: shuffledDice[1], arg4: :len drawnDice);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(1, 2, 3, 1);
        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional(random, "Start").EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueType.Nothing, args[0].Type);
        Assert.AreEqual(EventScriptValueType.Nothing, args[1].Type);
        Assert.IsTrue(args[2].Type is EventScriptValueType.Integer or EventScriptValueType.Decimal);
        Assert.AreEqual(2, Convert.ToInt32(args[3].AsInteger()));
    }

    [TestMethod]
    public void ReverseContainsAndObjectMatchingWorkAcrossSupportedContainers()
    {
        const string script = """
            on Start(units) {
                let reversed be [1, 2, 3][:reverse];
                let reversedDice be :dice 3d6[:reverse];
                let reversedSet be :set[1, 2][:reverse];
                let hasTwo be [1, 2, 3][:contains 2];
                let hasAll be [1, 2, 3][:contains all [1, 3]];
                let hasAny be [1, 2, 3][:contains any [0, 3]];
                let textContains be 'battle'[:contains 'tt'];
                let textContainsAll be 'battle'[:contains all ['ba', 'tt']];
                let dictContains be [name: 'Ada', team: 'red'][:contains 'name'];
                let hasOrc be units[:has [faction: 'orc', alive: true]];
                let hasNestedOwner be units[:has [owner: [team: 'red']]];
                publish Done(arg1: reversed[1], arg2: reversed[2], arg3: reversed[3], arg4: reversedDice[1], arg5: :len reversedDice, arg6: reversedSet, arg7: hasTwo, arg8: hasAll, arg9: hasAny, arg10: textContains, arg11: textContainsAll, arg12: dictContains, arg13: hasOrc, arg14: hasNestedOwner);
            }
            """;

        var units = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["faction"] = "orc",
                ["alive"] = true,
                ["owner"] = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["team"] = "red" })
            }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["faction"] = "human",
                ["alive"] = true,
                ["owner"] = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["team"] = "blue" })
            })
        ]);

        var random = EventScriptRandomGenerator.FromSequence(2, 5, 6);
        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional(random, "Start", units).EmittedEvents[0].Arguments;

        Assert.AreEqual(3m, args[0].AsNumber());
        Assert.AreEqual(2m, args[1].AsNumber());
        Assert.AreEqual(1m, args[2].AsNumber());
        Assert.AreEqual(2L, args[3].AsInteger());
        Assert.AreEqual(3, Convert.ToInt32(args[4].AsInteger()));
        Assert.AreEqual(EventScriptValueType.Nothing, args[5].Type);
        Assert.IsTrue(args[6].AsBoolean());
        Assert.IsTrue(args[7].AsBoolean());
        Assert.IsTrue(args[8].AsBoolean());
        Assert.IsTrue(args[9].AsBoolean());
        Assert.IsTrue(args[10].AsBoolean());
        Assert.IsTrue(args[11].AsBoolean());
        Assert.IsTrue(args[12].AsBoolean());
        Assert.IsTrue(args[13].AsBoolean());
    }

    [TestMethod]
    public void InlineCollectionsCanBeDeclaredAndReadBack()
    {
        const string script = """
            on Start {
                let myList be [1, 2, 3];
                let myValues be [name: 'Hello', position: 1];
                let emptyValues be [:];
                publish Done(arg1: :len myList, arg2: myValues.name, arg3: myValues.position, arg4: :len emptyValues);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(3, Convert.ToInt32(result.EmittedEvents[0].Arguments[0].AsInteger()));
        Assert.AreEqual("Hello", result.EmittedEvents[0].Arguments[1].AsText());
        Assert.AreEqual(1m, result.EmittedEvents[0].Arguments[2].AsNumber());
        Assert.AreEqual(0, Convert.ToInt32(result.EmittedEvents[0].Arguments[3].AsInteger()));
    }

    [TestMethod]
    public void GeneratedCollectionsCanProjectRangesIntoListsAndSets()
    {
        const string script = """
            on Start {
                let squares be :list[:select item from 1 to 5 -> item * item];
                let descending be :list[:select item from 5 to 1 step (0 - 2) -> item];
                let filtered be :list[:select item from 1 to 6 where item % 2 = 0 -> item * item];
                let tags be :set[:select item from 1 to 4 where item >= 2 -> item % 2];
                let emptyByDirection be :list[:select item from 1 to 5 step (0 - 1) -> item];
                publish Done(arg1: squares[1], arg2: squares[2], arg3: squares[3], arg4: squares[4], arg5: squares[5], arg6: descending[1], arg7: descending[2], arg8: descending[3], arg9: filtered[1], arg10: filtered[2], arg11: filtered[3], arg12: :len tags, arg13: 0 in tags, arg14: 1 in tags, arg15: :len emptyByDirection);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.Invoke("Start").EmittedEvents[0].Arguments;

        Assert.AreEqual(1m, args[0].AsNumber());
        Assert.AreEqual(4m, args[1].AsNumber());
        Assert.AreEqual(9m, args[2].AsNumber());
        Assert.AreEqual(16m, args[3].AsNumber());
        Assert.AreEqual(25m, args[4].AsNumber());
        Assert.AreEqual(5L, args[5].AsInteger());
        Assert.AreEqual(3L, args[6].AsInteger());
        Assert.AreEqual(1L, args[7].AsInteger());
        Assert.AreEqual(4m, args[8].AsNumber());
        Assert.AreEqual(16m, args[9].AsNumber());
        Assert.AreEqual(36m, args[10].AsNumber());
        Assert.AreEqual(2, Convert.ToInt32(args[11].AsInteger()));
        Assert.IsTrue(args[12].AsBoolean());
        Assert.IsTrue(args[13].AsBoolean());
        Assert.AreEqual(0, Convert.ToInt32(args[14].AsInteger()));
    }

    [TestMethod]
    public void RangesCanBeDeclaredIteratedAndUsedAsGeneratedCollectionSources()
    {
        const string script = """
            on Start(values) {
                let fullRange as :range be from 1 to 3;
                let odds as :range be from 1 to 5 step 2;
                let descending be from 5 to 1 step (0 - 2);
                let zeroStep be from 1 to 5 step 0;
                let doubled be :list[:select item in values -> item * 2];
                let filtered be :set[:select item in values where item > 3 -> item % 2];

                for item in fullRange publish Full(value: item);
                for item from 1 to 5 step 2 publish Direct(value: item);
                for item in odds publish Indirect(value: item);
                for item in zeroStep publish Zero(value: item);

                publish Done(isRange: odds is :range, descendingFirst: descending[1], descendingSecond: descending[2], descendingThird: descending[3], zeroLen: :len zeroStep, doubledFirst: doubled[1], doubledSecond: doubled[2], filteredLen: :len filtered, hasZero: 0 in filtered, hasOne: 1 in filtered);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Start", EventScriptValue.List(new EventScriptValue[] { 2m, 4m, 5m }));

        Assert.AreEqual("Full", result.EmittedEvents[0].Message);
        Assert.AreEqual(1L, result.EmittedEvents[0].Arguments["value"].AsInteger());
        Assert.AreEqual(2L, result.EmittedEvents[1].Arguments["value"].AsInteger());
        Assert.AreEqual(3L, result.EmittedEvents[2].Arguments["value"].AsInteger());
        Assert.AreEqual("Direct", result.EmittedEvents[3].Message);
        Assert.AreEqual(1L, result.EmittedEvents[3].Arguments["value"].AsInteger());
        Assert.AreEqual(3L, result.EmittedEvents[4].Arguments["value"].AsInteger());
        Assert.AreEqual(5L, result.EmittedEvents[5].Arguments["value"].AsInteger());
        Assert.AreEqual("Indirect", result.EmittedEvents[6].Message);
        Assert.AreEqual(1L, result.EmittedEvents[6].Arguments["value"].AsInteger());
        Assert.AreEqual(3L, result.EmittedEvents[7].Arguments["value"].AsInteger());
        Assert.AreEqual(5L, result.EmittedEvents[8].Arguments["value"].AsInteger());
        Assert.AreEqual("Done", result.EmittedEvents[9].Message);

        var done = result.EmittedEvents[9].Arguments;
        Assert.IsTrue(done["isRange"].AsBoolean());
        Assert.AreEqual(5L, done["descendingFirst"].AsInteger());
        Assert.AreEqual(3L, done["descendingSecond"].AsInteger());
        Assert.AreEqual(1L, done["descendingThird"].AsInteger());
        Assert.AreEqual(0L, done["zeroLen"].AsInteger());
        Assert.AreEqual(4m, done["doubledFirst"].AsNumber());
        Assert.AreEqual(8m, done["doubledSecond"].AsNumber());
        Assert.AreEqual(2L, done["filteredLen"].AsInteger());
        Assert.IsTrue(done["hasZero"].AsBoolean());
        Assert.IsTrue(done["hasOne"].AsBoolean());
    }

    [TestMethod]
    public void RulesAndSelectsCanBeReusedAcrossExpressionsAndCollections()
    {
        const string script = """
            rule wounded(unit) means unit.hp < unit.maxHp
            select woundedUnits(units) means units[:filter unit where unit is wounded]

            on Start(unit, units) {
                let byCall be wounded(unit);
                let byPredicate be unit is wounded;
                let woundedList be woundedUnits(units);
                publish Done(arg1: byCall, arg2: byPredicate, arg3: :len woundedList, arg4: woundedList[1].name, arg5: woundedList[2].name);
            }
            """;

        var unit = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = "Ada",
            ["hp"] = 2m,
            ["maxHp"] = 5m
        });
        var units = EventScriptValue.List([
            unit,
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["name"] = "Bert",
                ["hp"] = 4m,
                ["maxHp"] = 4m
            }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["name"] = "Cara",
                ["hp"] = 1m,
                ["maxHp"] = 3m
            })
        ]);

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional("Start", unit, units).EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsTrue(args[1].AsBoolean());
        Assert.AreEqual(2, Convert.ToInt32(args[2].AsInteger()));
        Assert.AreEqual("Ada", args[3].AsText());
        Assert.AreEqual("Cara", args[4].AsText());
    }

    [TestMethod]
    public void MissingRulesOrSelectsFailCompilation()
    {
        const string missingRuleScript = """
            on Start(unit) {
                let x be missingRule(unit);
            }
            """;

        const string missingSelectScript = """
            on Start(units) {
                let x be missingSelect(units);
            }
            """;

        const string invalidPredicateScript = """
            select wounded(unit) means unit.hp < unit.maxHp

            on Start(unit) {
                let x be unit is wounded;
            }
            """;

        const string wrongRuleArityScript = """
            rule wounded(unit) means unit.hp < unit.maxHp

            on Start(unit) {
                let x be wounded(unit, unit);
            }
            """;

        const string wrongSelectArityScript = """
            select woundedUnits(units) means units[:filter unit where unit.hp < unit.maxHp]

            on Start(units) {
                let x be woundedUnits();
            }
            """;

        Assert.ThrowsExactly<EventScriptLinkageException>(() => EventScriptManager.Compile(missingRuleScript));
        Assert.ThrowsExactly<EventScriptLinkageException>(() => EventScriptManager.Compile(missingSelectScript));
        Assert.ThrowsExactly<EventScriptLinkageException>(() => EventScriptManager.Compile(invalidPredicateScript));
        Assert.ThrowsExactly<EventScriptLinkageException>(() => EventScriptManager.Compile(wrongRuleArityScript));
        Assert.ThrowsExactly<EventScriptLinkageException>(() => EventScriptManager.Compile(wrongSelectArityScript));
    }

    [TestMethod]
    public void RulesAndSelectsStayLenientWhenExpectedKeysAreMissing()
    {
        const string script = """
            rule wounded(unit) means unit.hp < unit.maxHp
            select woundedUnits(units) means units[:filter unit where unit is wounded]

            on Start(unit, units) {
                let byCall be wounded(unit);
                let byPredicate be unit is wounded;
                let woundedList be woundedUnits(units);
                publish Done(arg1: byCall, arg2: byPredicate, arg3: :len woundedList);
            }
            """;

        var incompleteUnit = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = "NoHp"
        });
        var units = EventScriptValue.List([
            incompleteUnit,
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
            {
                ["name"] = "Healthy",
                ["hp"] = 5m,
                ["maxHp"] = 5m
            })
        ]);

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional("Start", incompleteUnit, units).EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueType.Boolean, args[0].Type);
        Assert.AreEqual(EventScriptValueType.Boolean, args[1].Type);
        Assert.IsFalse(args[0].AsBoolean());
        Assert.IsFalse(args[1].AsBoolean());
        Assert.AreEqual(0, Convert.ToInt32(args[2].AsInteger()));
    }

    [TestMethod]
    public void RuleCallsAlwaysReturnBooleanWhileSelectKeepsOriginalType()
    {
        const string script = """
            rule numeric(value) means value * 2
            select numericSelect(value) means value * 2

            on Start {
                let byRuleTwo be numeric(2)
                let byRuleZero be numeric(0)
                let bySelect be numericSelect(2)
                let byPredicate be 2 is numeric
                publish Done(
                    byRuleTwo: byRuleTwo,
                    byRuleZero: byRuleZero,
                    bySelect: bySelect,
                    byPredicate: byPredicate
                )
            }
            """;

        var args = EventScriptManager.Compile(script)
            .Invoke("Start")
            .EmittedEvents[0]
            .Arguments;

        Assert.AreEqual(EventScriptValueType.Boolean, args["byRuleTwo"].Type);
        Assert.AreEqual(EventScriptValueType.Boolean, args["byRuleZero"].Type);
        Assert.AreEqual(EventScriptValueType.Decimal, args["bySelect"].Type);
        Assert.AreEqual(EventScriptValueType.Boolean, args["byPredicate"].Type);
        Assert.IsTrue(args["byRuleTwo"].AsBoolean());
        Assert.IsFalse(args["byRuleZero"].AsBoolean());
        Assert.AreEqual(4m, args["bySelect"].AsNumber());
        Assert.IsTrue(args["byPredicate"].AsBoolean());
    }

    [TestMethod]
    public void EscapedQuotesStayInsideTextValues()
    {
        const string script = """
            on Start {
                let text be 'Hello ''World'', I''m here';
                publish Done(arg1: text);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual("Hello 'World', I'm here", result.EmittedEvents[0].Arguments[0].AsText());
    }

    [TestMethod]
    public void TaggedTypeDeclarationsCanConvertIncomingValues()
    {
        const string script = """
            on Start {
                let tagOk as :tag be :name;
                let numberOk as :decimal be '12.2';
                let numberFail as :decimal be 'abc';
                let integerOk as :integer be '12.7';
                let booleanOk as :boolean be 'true';
                let listOk as :list be 'ab';
                let setOk be :set[1, 1, 2];
                let diceOk as :dice be [6, 2, 4];
                publish Done(arg1: tagOk, arg2: numberOk, arg3: numberFail, arg4: integerOk, arg5: booleanOk, arg6: :len listOk, arg7: :len setOk, arg8: diceOk[1], arg9: :len diceOk);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueType.Tag, args[0].Type);
        Assert.AreEqual("name", args[0].AsText());
        Assert.AreEqual(12.2m, args[1].AsNumber());
        Assert.IsTrue(args[2].IsNaN());
        Assert.AreEqual(12, Convert.ToInt32(args[3].AsInteger()));
        Assert.IsTrue(args[4].AsBoolean());
        Assert.AreEqual(2, Convert.ToInt32(args[5].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(args[6].AsInteger()));
        Assert.AreEqual(6, Convert.ToInt32(args[7].AsInteger()));
        Assert.AreEqual(3, Convert.ToInt32(args[8].AsInteger()));
    }

    [TestMethod]
    public void TaggedLengthChecksCanMeasureSizedValues()
    {
        const string script = """
            on Start(optionalValue) {
                let textLen be :len 'abc';
                let listLen be :len [1, 2, 3];
                let dictLen be :len [first: 1, second: 2];
                let tags be :set[1, 1, 2];
                let setLen be :len tags;
                let diceLen be :len :dice 4d6;
                let nothingLen be :len missing;
                let optionalLen be :len optionalValue;
                let decimalLen be :len 12.5;
                let booleanLen be :len true;
                publish Done(arg1: textLen, arg2: listLen, arg3: dictLen, arg4: setLen, arg5: diceLen, arg6: nothingLen, arg7: optionalLen, arg8: decimalLen, arg9: booleanLen);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(2, 6, 3, 5);
        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(random, "Start", EventScriptValue.OptionalSome("x"));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(3, Convert.ToInt32(args[0].AsInteger()));
        Assert.AreEqual(3, Convert.ToInt32(args[1].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(args[2].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(args[3].AsInteger()));
        Assert.AreEqual(4, Convert.ToInt32(args[4].AsInteger()));
        Assert.AreEqual(0, Convert.ToInt32(args[5].AsInteger()));
        Assert.AreEqual(1, Convert.ToInt32(args[6].AsInteger()));
        Assert.AreEqual(EventScriptValueType.Nothing, args[7].Type);
        Assert.AreEqual(EventScriptValueType.Nothing, args[8].Type);
    }

    [TestMethod]
    public void OneBasedLookupsReturnNothingWhenValuesAreMissing()
    {
        const string script = """
            on Start {
                let values be [10, 20, 30];
                let entries be [name: 'Ada'];
                let tags be :set['alpha', 'beta'];
                publish Done(arg1: values[1], arg2: values[3], arg3: values[0], arg4: values[4], arg5: entries['name'], arg6: entries['missing'], arg7: tags[1], arg8: tags[3]);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(10m, args[0].AsNumber());
        Assert.AreEqual(30m, args[1].AsNumber());
        Assert.AreEqual(EventScriptValueType.Nothing, args[2].Type);
        Assert.AreEqual(EventScriptValueType.Nothing, args[3].Type);
        Assert.AreEqual("Ada", args[4].AsText());
        Assert.AreEqual(EventScriptValueType.Nothing, args[5].Type);
        Assert.AreEqual("alpha", args[6].AsText());
        Assert.AreEqual(EventScriptValueType.Nothing, args[7].Type);
    }

    [TestMethod]
    public void DictionaryValuesCanBeReadByPropertyLiteralTextTagOrDynamicKey()
    {
        const string script = """
            on Start(entry, keyName) {
                let lookupProperty be :name;
                publish Done(arg1: entry.name, arg2: entry['name'], arg3: entry[:name], arg4: entry[lookupProperty], arg5: entry[keyName], arg6: entry['missing']);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Ada" }),
            EventScriptValue.Text("name"));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual("Ada", args[0].AsText());
        Assert.AreEqual("Ada", args[1].AsText());
        Assert.AreEqual("Ada", args[2].AsText());
        Assert.AreEqual("Ada", args[3].AsText());
        Assert.AreEqual("Ada", args[4].AsText());
        Assert.AreEqual(EventScriptValueType.Nothing, args[5].Type);
    }

    [TestMethod]
    public void ValueChecksAndFallbacksWorkAcrossOptionalsAndContainers()
    {
        const string script = """
            on Start(opt, emptyText, emptyList, emptyDict, zeroValue, falseValue, diceValue, nanValue, infinityValue) {
                let hasOpt be has value opt;
                let hasEmptyText be has value emptyText;
                let hasEmptyList be has value emptyList;
                let hasEmptyDict be has value emptyDict;
                let hasZero be has value zeroValue;
                let hasFalse be has value falseValue;
                let hasNaN be has value nanValue;
                let hasInfinity be has value infinityValue;
                let isEmptyOpt be empty opt;
                let isEmptyText be empty emptyText;
                let isEmptyList be empty emptyList;
                let isEmptyDict be empty emptyDict;
                let isEmptyDice be empty diceValue;
                let isEmptyNaN be empty nanValue;
                let isEmptyInfinity be empty infinityValue;
                let isNotEmptyList be not empty [1];
                let isNotFalse be not falseValue;
                let optValue be opt :default 10;
                let listValue be emptyList :default [1, 2];
                let textValue be emptyText :default 'fallback';
                let dictValue be emptyDict :default [name: 'default'];
                let missingTextValue be emptyDict['name'] :default 'fallback';
                publish Done(arg1: hasOpt, arg2: hasEmptyText, arg3: hasEmptyList, arg4: hasEmptyDict, arg5: hasZero, arg6: hasFalse, arg7: hasNaN, arg8: hasInfinity, arg9: isEmptyOpt, arg10: isEmptyText, arg11: isEmptyList, arg12: isEmptyDict, arg13: isEmptyDice, arg14: isEmptyNaN, arg15: isEmptyInfinity, arg16: isNotEmptyList, arg17: isNotFalse, arg18: optValue, arg19: listValue[1], arg20: textValue, arg21: dictValue.name, arg22: missingTextValue);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.OptionalSome(7m),
            "",
            EventScriptValue.List([]),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>()),
            0m,
            false,
            EventScriptValue.Dice(EventScriptDiceValue.Create(Array.Empty<int>())),
            EventScriptValue.DecimalNaN(),
            EventScriptValue.DecimalInfinity());
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsFalse(args[1].AsBoolean());
        Assert.IsFalse(args[2].AsBoolean());
        Assert.IsFalse(args[3].AsBoolean());
        Assert.IsTrue(args[4].AsBoolean());
        Assert.IsTrue(args[5].AsBoolean());
        Assert.IsFalse(args[6].AsBoolean());
        Assert.IsFalse(args[7].AsBoolean());
        Assert.IsFalse(args[8].AsBoolean());
        Assert.IsTrue(args[9].AsBoolean());
        Assert.IsTrue(args[10].AsBoolean());
        Assert.IsTrue(args[11].AsBoolean());
        Assert.IsTrue(args[12].AsBoolean());
        Assert.IsFalse(args[13].AsBoolean());
        Assert.IsFalse(args[14].AsBoolean());
        Assert.IsTrue(args[15].AsBoolean());
        Assert.IsTrue(args[16].AsBoolean());
        Assert.AreEqual(7m, args[17].AsNumber());
        Assert.AreEqual(1m, args[18].AsNumber());
        Assert.AreEqual("fallback", args[19].AsText());
        Assert.AreEqual("default", args[20].AsText());
        Assert.AreEqual("fallback", args[21].AsText());
    }

    [TestMethod]
    public void UnaryMinusSupportsNegativeDecimalIntegerAndPercentageExpressions()
    {
        const string script = """
            on Start {
                let negInt be -12;
                let negDecimal be -12.34;
                let negPercent be -25%;
                let restored be -negDecimal;
                let boolNegation be not false;
                publish Done(arg1: negInt, arg2: negDecimal, arg3: negPercent, arg4: restored, arg5: boolNegation);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.Invoke("Start").EmittedEvents[0].Arguments;

        Assert.AreEqual(-12m, args[0].AsNumber());
        Assert.AreEqual(-12.34m, args[1].AsNumber());
        Assert.AreEqual("-25%", args[2].AsText());
        Assert.AreEqual(12.34m, args[3].AsNumber());
        Assert.IsTrue(args[4].AsBoolean());
    }

    [TestMethod]
    public void DomainStyleBooleanChecksBehaveLikeTheirTechnicalForms()
    {
        const string script = """
            on Start(hp, mana, hand, target) {
                let hpCheck be hp is 0 or less;
                let manaCheck be mana is at least 3;
                let handCheck be hand is empty;
                let targetCheck be target has value;
                publish Done(arg1: hpCheck, arg2: manaCheck, arg3: handCheck, arg4: targetCheck);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.Decimal(-1m),
            EventScriptValue.Integer(3),
            EventScriptValue.List([]),
            EventScriptValue.Text("orc"));
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsTrue(args[1].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
        Assert.IsTrue(args[3].AsBoolean());
    }

    [TestMethod]
    public void ExistingValueAndEmptyChecksRemainTheSingleRuntimeSemantics()
    {
        const string script = """
            on Start(hand, target) {
                let oldHasValue be has value target;
                let newHasValue be target has value;
                let oldEmpty be empty hand;
                let newEmpty be hand is empty;
                publish Done(arg1: oldHasValue, arg2: newHasValue, arg3: oldEmpty, arg4: newEmpty);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.List([]),
            EventScriptValue.Text("orc"));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(args[0], args[1]);
        Assert.AreEqual(args[2], args[3]);
        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
    }

    [TestMethod]
    public void ContainmentChecksWorkForTextCollectionsAndDictionaryKeys()
    {
        const string script = """
            on Start {
                let inText be 'ell' in 'Hello';
                let inList be 2 in [1, 2, 3];
                let inSet be 'beta' in :set['alpha', 'beta'];
                let inDice be 6 in :dice 4d6;
                let inDict be 'name' in [name: 'Ada'];
                let missingKey be 'level' in [name: 'Ada'];
                let dictValue be 'Ada' value in [name: 'Ada'];
                let startsText be 'Hello' starts with 'He';
                let endsText be 'Hello' ends with 'lo';
                let startsList be [1, 2, 3] starts with [1, 2];
                let endsList be [1, 2, 3] ends with [2, 3];
                let startsDice be :dice 4d6 starts with [6, 5];
                let endsDice be :dice 4d6 ends with [3, 2];
                publish Done(arg1: inText, arg2: inList, arg3: inSet, arg4: inDice, arg5: inDict, arg6: missingKey, arg7: dictValue, arg8: startsText, arg9: endsText, arg10: startsList, arg11: endsList, arg12: startsDice, arg13: endsDice);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(2, 6, 3, 5, 2, 6, 3, 5, 2, 6, 3, 5);
        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional(random, "Start").EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsTrue(args[1].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
        Assert.IsTrue(args[3].AsBoolean());
        Assert.IsTrue(args[4].AsBoolean());
        Assert.IsFalse(args[5].AsBoolean());
        Assert.IsTrue(args[6].AsBoolean());
        Assert.IsTrue(args[7].AsBoolean());
        Assert.IsTrue(args[8].AsBoolean());
        Assert.IsTrue(args[9].AsBoolean());
        Assert.IsTrue(args[10].AsBoolean());
        Assert.IsTrue(args[11].AsBoolean());
        Assert.IsTrue(args[12].AsBoolean());
    }

    [TestMethod]
    public void PatternChecksRecognizeCommonDiceHands()
    {
        const string script = """
            on Start(pairRoll, tripleRoll, fullHouseRoll, straightRoll, sixRoll, sevenRoll) {
                publish Done(arg1: pairRoll[:has pair], arg2: pairRoll[:has pair of 6], arg3: pairRoll[:has three of a kind], arg4: tripleRoll[:has three of a kind], arg5: tripleRoll[:has three of 6], arg6: sixRoll[:has six of a kind], arg7: sevenRoll[:has seven of 6], arg8: fullHouseRoll[:has full house], arg9: straightRoll[:has straight]);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 6, 6, 4, 3, 2 })),
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 6, 6, 6, 4, 3 })),
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 5, 5, 5, 2, 2 })),
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 6, 6, 5, 4, 3, 2 })),
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 4, 4, 4, 4, 4, 4 })),
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 6, 6, 6, 6, 6, 6, 6 })));
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsTrue(args[1].AsBoolean());
        Assert.IsFalse(args[2].AsBoolean());
        Assert.IsTrue(args[3].AsBoolean());
        Assert.IsTrue(args[4].AsBoolean());
        Assert.IsTrue(args[5].AsBoolean());
        Assert.IsTrue(args[6].AsBoolean());
        Assert.IsTrue(args[7].AsBoolean());
        Assert.IsTrue(args[8].AsBoolean());
    }

    [TestMethod]
    public void PatternSelectionsCanExtractMatchingSubCollections()
    {
        const string script = """
            on Start(roll, cards, noMatch) {
                let pairRoll be roll[:take pair];
                let fullHouseRoll be roll[:take full house];
                let straightCards be cards[:take straight];
                let missingPair be noMatch[:take pair] :default ['fallback'];
                publish Done(arg1: pairRoll[1], arg2: pairRoll[2], arg3: :len pairRoll, arg4: fullHouseRoll[1], arg5: fullHouseRoll[2], arg6: fullHouseRoll[3], arg7: fullHouseRoll[4], arg8: fullHouseRoll[5], arg9: straightCards[1], arg10: straightCards[2], arg11: straightCards[3], arg12: straightCards[4], arg13: missingPair[1]);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.Dice(EventScriptDiceValue.Create(new[] { 6, 6, 5, 5, 5 })),
            EventScriptValue.List([1m, 2m, 2m, 3m, 4m]),
            EventScriptValue.List([1m, 2m, 3m]));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(6L, args[0].AsInteger());
        Assert.AreEqual(6L, args[1].AsInteger());
        Assert.AreEqual(2L, args[2].AsInteger());
        Assert.AreEqual(6L, args[3].AsInteger());
        Assert.AreEqual(6L, args[4].AsInteger());
        Assert.AreEqual(5L, args[5].AsInteger());
        Assert.AreEqual(5L, args[6].AsInteger());
        Assert.AreEqual(5L, args[7].AsInteger());
        Assert.AreEqual(1m, args[8].AsNumber());
        Assert.AreEqual(2m, args[9].AsNumber());
        Assert.AreEqual(3m, args[10].AsNumber());
        Assert.AreEqual(4m, args[11].AsNumber());
        Assert.AreEqual("fallback", args[12].AsText());
    }

    [TestMethod]
    public void PatternChecksAlsoWorkForLists()
    {
        const string script = """
            on Start(cards, orderedCards, plainText) {
                publish Done(arg1: cards[:has pair], arg2: cards[:has three of 'King'], arg3: cards[:has full house], arg4: orderedCards[:has straight], arg5: plainText[:has pair]);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.List([
                EventScriptValue.Text("King"),
                EventScriptValue.Text("King"),
                EventScriptValue.Text("King"),
                EventScriptValue.Text("Queen"),
                EventScriptValue.Text("Queen")
            ]),
            EventScriptValue.List([1m, 2m, 3m, 4m, 5m]),
            EventScriptValue.Text("Hello"));
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsTrue(args[1].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
        Assert.IsTrue(args[3].AsBoolean());
        Assert.IsFalse(args[4].AsBoolean());
    }

    [TestMethod]
    public void FallbacksCoverOptionalNoneAndNothing()
    {
        const string script = """
            on Start(opt, missingValue) {
                publish Done(arg1: opt :default 10, arg2: missingValue :default 20);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Start", EventScriptValue.OptionalNone(), EventScriptValue.Nothing);
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(10m, args[0].AsNumber());
        Assert.AreEqual(20m, args[1].AsNumber());
    }

    [TestMethod]
    public void ConditionalValuesUseTheFirstMatchingBranch()
    {
        const string script = """
            on Start(age) {
                let score as :decimal be 12 when age is :boolean, or 15 when age is :decimal, otherwise 5;
                publish Done(arg1: score);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);

        var boolResult = interpreter.InvokePositional("Start", true);
        var numberResult = interpreter.InvokePositional("Start", 7m);
        var textResult = interpreter.InvokePositional("Start", "x");

        Assert.AreEqual(12m, boolResult.EmittedEvents[0].Arguments[0].AsNumber());
        Assert.AreEqual(15m, numberResult.EmittedEvents[0].Arguments[0].AsNumber());
        Assert.AreEqual(5m, textResult.EmittedEvents[0].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void ConditionalValuesCanCombineChecksWithWordOr()
    {
        const string script = """
            on Start(age) {
                let score as :decimal be 12 when age is :decimal or age is :boolean otherwise 5;
                publish Done(arg1: score);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);

        Assert.AreEqual(12m, interpreter.InvokePositional("Start", true).EmittedEvents[0].Arguments[0].AsNumber());
        Assert.AreEqual(12m, interpreter.InvokePositional("Start", 2m).EmittedEvents[0].Arguments[0].AsNumber());
        Assert.AreEqual(5m, interpreter.InvokePositional("Start", "x").EmittedEvents[0].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void TaggedRoundingOperationsAndIntegerConversionStayPredictable()
    {
        const string script = """
            on Start {
                let base as :integer be '12.7';
                publish Done(arg1: base, arg2: :floor 12.7, arg3: :ceil 12.1, arg4: :roundeven 12.5, arg5: :roundeven 13.5, arg6: :rounddown 12.1, arg7: :roundup 12.1, arg8: :round 12.5);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(12, Convert.ToInt32(args[0].AsInteger()));
        Assert.AreEqual(12, Convert.ToInt32(args[1].AsInteger()));
        Assert.AreEqual(13, Convert.ToInt32(args[2].AsInteger()));
        Assert.AreEqual(12, Convert.ToInt32(args[3].AsInteger()));
        Assert.AreEqual(14, Convert.ToInt32(args[4].AsInteger()));
        Assert.AreEqual(12, Convert.ToInt32(args[5].AsInteger()));
        Assert.AreEqual(13, Convert.ToInt32(args[6].AsInteger()));
        Assert.AreEqual(12, Convert.ToInt32(args[7].AsInteger()));
    }

    [TestMethod]
    public void InvalidNumbersAndMissingValuesStayLenient()
    {
        const string script = """
            on Start {
                let nanValue as :decimal be 'abc';
                let nanPropagated be nanValue + 5;
                let nothing be missing;
                let nothingPropagated be nothing + 5;
                publish Done(arg1: nanValue, arg2: nanPropagated, arg3: nothing, arg4: nothingPropagated);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].IsNaN());
        Assert.IsTrue(args[1].IsNaN());
        Assert.AreEqual(EventScriptValueType.Nothing, args[2].Type);
        Assert.AreEqual(EventScriptValueType.Nothing, args[3].Type);
    }

    [TestMethod]
    public void ReversedRandomBoundsDoNotCrashARun()
    {
        const string script = """
            on Roll(min, max) {
                let value be :random from min to max;
                publish Done(arg1: value);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Roll", 9m, 2m);
        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void RandomCanProduceDecimalResultsWhenEitherBoundIsDecimal()
    {
        const string script = """
            on Roll {
                let integerValue be :random from 1 to 6;
                let decimalValue be :random from 0.0 to 1.0;
                let mixedValue be :random from 1 to 2.0;
                publish Done(arg1: integerValue, arg2: decimalValue, arg3: mixedValue);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(4, 0.25m, 1.5m);
        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.InvokePositional(random, "Roll").EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueType.Integer, args[0].Type);
        Assert.AreEqual(4L, args[0].AsInteger());
        Assert.AreEqual(EventScriptValueType.Decimal, args[1].Type);
        Assert.AreEqual(0.25m, args[1].AsNumber());
        Assert.AreEqual(EventScriptValueType.Decimal, args[2].Type);
        Assert.AreEqual(1.5m, args[2].AsNumber());
    }

    [TestMethod]
    public void XorSupportsWordAndSymbolForms()
    {
        const string script = """
            on Start {
                publish Done(arg1: true xor false, arg2: true xor true, arg3: true ^ false, arg4: false ^ false);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var args = interpreter.Invoke("Start").EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsFalse(args[1].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
        Assert.IsFalse(args[3].AsBoolean());
    }

    [TestMethod]
    public void ReadingAPropertyFromNothingReturnsNothing()
    {
        const string script = """
            on Inspect(item) {
                publish Done(arg1: item.name);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokeClr("Inspect", ("item", null));
        Assert.AreEqual(EventScriptValueType.Nothing, result.EmittedEvents[0].Arguments[0].Type);
    }

    [TestMethod]
    public void MissingDictionaryPropertiesReturnNothing()
    {
        const string script = """
            on Inspect(item) {
                publish Done(arg1: item.name);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Inspect", EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>()));
        Assert.AreEqual(EventScriptValueType.Nothing, result.EmittedEvents[0].Arguments[0].Type);
    }

    [TestMethod]
    public void DictionaryPropertiesCanBeReadByName()
    {
        const string script = """
            on Inspect(item) {
                publish Done(arg1: item.points);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Inspect", EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["points"] = 7m }));

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(7m, result.EmittedEvents[0].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void DividingByZeroProducesInfinity()
    {
        const string script = """
            on Start {
                let value be 6 / 0;
                publish Done(arg1: value);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        var value = result.EmittedEvents[0].Arguments[0];
        Assert.IsTrue(value.IsInfinity());
        Assert.IsFalse(value.IsNegativeInfinity());
    }

    [TestMethod]
    public void ModuloByZeroProducesNaN()
    {
        const string script = """
            on Start {
                let value be 6 % 0;
                publish Done(arg1: value);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        Assert.IsTrue(result.EmittedEvents[0].Arguments[0].IsNaN());
    }

    [TestMethod]
    public void HandlerSnapshotsKeepOuterVariablesWithoutLeakingInnerScopes()
    {
        const string script = """
            on Inspect(values, threshold) {
                let passed be values[:any value where value > threshold];
                for item in values {
                    let doubled be item + item;
                }
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Inspect", EventScriptValue.List(new EventScriptValue[] { 1m, 5m, 9m }), 7m);

        Assert.IsEmpty(result.Variables);
        Assert.IsEmpty(result.EmittedEvents);
    }

    [TestMethod]
    public void HostCanProcessPublishedMessagesThroughTheQueue()
    {
        const string script = """
            on Start(value) {
                publish Next(value: value + 1);
            }

            on Next(value) {
                publish Done(arg1: value);
            }
            """;

        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script));
        host.Publish("Start", ("value", EventScriptValue.Decimal(2m)));
        var emitted = collector.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        Assert.HasCount(2, emitted);
        Assert.AreEqual("Next", emitted[0].Name);
        Assert.AreEqual(3m, emitted[0].Arguments[0].AsNumber());
        Assert.AreEqual("Done", emitted[1].Name);
        Assert.AreEqual(3m, emitted[1].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void HostStopsEventLoopsAtTheConfiguredProcessingLimit()
    {
        const string script = """
            on Start {
                publish Loop;
            }

            on Loop {
                publish Start;
            }
            """;

        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithMaxProcessedEventsPerRun(4)
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script));
        host.Publish(EventScriptMessage.Message("Start"));
        var emitted = collector.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();
        Assert.HasCount(4, emitted);
        Assert.AreEqual("Loop", emitted[0].Name);
        Assert.AreEqual("Start", emitted[1].Name);
        Assert.AreEqual("Loop", emitted[2].Name);
        Assert.AreEqual("Start", emitted[3].Name);
    }

    [TestMethod]
    public void HostCanRoutePublishedMessagesToExternalSubscribers()
    {
        const string script = """
            on Start(playerId) {
                publish Notify(playerId: playerId, count: 3);
            }
            """;

        var invocations = new List<EventScriptValue[]>();
        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script))
            .Subscribe("Notify", ["playerId", "count"], (message, context) => invocations.Add([message.Arguments["playerId"], message.Arguments["count"]]));

        host.Publish("Start", ("playerId", EventScriptValue.Text("p1")));
        var emitted = collector.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        Assert.HasCount(1, invocations);
        Assert.AreEqual("p1", invocations[0][0].AsText());
        Assert.AreEqual(3m, invocations[0][1].AsNumber());
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Notify", emitted[0].Name);
    }

    [TestMethod]
    public void HostRunsExternalSubscribersAfterScriptHandlersByDefaultPriority()
    {
        const string script = """
            on Start(value) {
                publish Notify(value: value);
            }

            on Notify(value) {
                publish SeenByScript(arg1: value + 1);
            }
            """;

        var invocations = new List<string>();
        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script))
            .Subscribe("Notify", ["value"], (message, context) => invocations.Add($"external:{message.Arguments["value"].AsNumber()}"));

        host.Publish("Start", ("value", EventScriptValue.Decimal(4m)));
        var emitted = collector.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        Assert.HasCount(1, invocations);
        Assert.AreEqual("external:4", invocations[0]);
        Assert.HasCount(2, emitted);
        Assert.AreEqual("Notify", emitted[0].Name);
        Assert.AreEqual("SeenByScript", emitted[1].Name);
    }

    [TestMethod]
    public void HostRunsMultipleExternalSubscribersInRegistrationOrder()
    {
        const string script = """
            on Start(value) {
                publish Notify(value: value);
            }
            """;

        var invocations = new List<string>();
        var host = EventScriptHost.CreateBuilder()
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script))
            .Subscribe("Notify", ["value"], (message, context) => invocations.Add($"first:{message.Arguments["value"].AsNumber()}"))
            .Subscribe("Notify", ["value"], (message, context) => invocations.Add($"second:{message.Arguments["value"].AsNumber()}"));

        host.Publish("Start", ("value", EventScriptValue.Decimal(4m)));

        CollectionAssert.AreEqual(new[] { "first:4", "second:4" }, invocations);
    }

    [TestMethod]
    public void HostIgnoresFailingExternalSubscribersAndContinues()
    {
        const string script = """
            on Start(value) {
                publish Notify(value: value);
            }
            """;

        var invocations = new List<string>();
        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script))
            .Subscribe("Notify", ["value"], (_, _) => throw new InvalidOperationException("boom"))
            .Subscribe("Notify", ["value"], (message, context) => invocations.Add($"ok:{message.Arguments["value"].AsNumber()}"));

        host.Publish("Start", ("value", EventScriptValue.Decimal(4m)));
        var emitted = collector.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        CollectionAssert.AreEqual(new[] { "ok:4" }, invocations);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Notify", emitted[0].Name);
    }

    [TestMethod]
    public void HostExternalSubscribersCanPublishFollowUpMessagesThroughTheSameRun()
    {
        const string script = """
            on Start(value) {
                publish Notify(value: value);
            }
            """;

        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script))
            .Subscribe("Notify", ["value"], (message, context) => context.Publish("Done", new Dictionary<string, EventScriptValue>
            {
                ["value"] = message.Arguments["value"]
            }));

        host.Publish("Start", ("value", EventScriptValue.Decimal(2m)));
        var emitted = collector.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        Assert.AreEqual("Notify", emitted[0].Name);
        Assert.AreEqual("Done", emitted[1].Name);
        Assert.AreEqual(2m, emitted[1].Arguments["value"].AsNumber());
    }

    [TestMethod]
    public void HostPublishClrUsesTheSamePublishPath()
    {
        const string script = """
            on Start(value) {
                publish Next(value: value + 1);
            }

            on Next(value) {
                publish Done(arg1: value);
            }
            """;

        var collectorA = new EventScriptDiagnosticTraceCollector();
        var hostA = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collectorA)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script));
        hostA.Publish("Start", ("value", EventScriptValue.Decimal(2m)));
        var publishResult = collectorA.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        var collectorB = new EventScriptDiagnosticTraceCollector();
        var hostB = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collectorB)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script));
        hostB.Publish(EventScriptMessage.Message("Start", ("value", 2m )));
        var publishClrResult = collectorB.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        CollectionAssert.AreEqual(
            publishResult.Select(evt => evt.Name).ToArray(),
            publishClrResult.Select(evt => evt.Name).ToArray());
        Assert.AreEqual(
            publishResult[1].Arguments[0].AsNumber(),
            publishClrResult[1].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void HostBuilderRejectsNonPositiveProcessingLimit()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EventScriptHost.CreateBuilder().WithMaxProcessedEventsPerRun(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EventScriptHost.CreateBuilder().WithMaxProcessedEventsPerRun(-1));
    }

    [TestMethod]
    public void HostBuilderConfiguredDiagnosticCollectorRecordsTheWholePublishRun()
    {
        const string script = """
            on Start(value) {
                publish Next(value: value + 1);
            }
            """;

        var collector = new EventScriptDiagnosticTraceCollector();
        var compiled = EventScriptManager.Compile(script, new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true });
        var host = EventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(compiled);

        host.Publish("Start", ("value", EventScriptValue.Decimal(2m)));

        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.DispatchStarted && evt.Name == "Start"));
        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.HandlerInvoked && evt.Name == "Start"));
        Assert.IsTrue(collector.Events.Any(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished && evt.Name == "Next"));
    }

    [TestMethod]
    public void HostBuilderConfiguredRandomIsUsedAcrossPublishRuns()
    {
        const string script = """
            on Roll {
                publish Result(value: :random 1 to 6);
            }
            """;

        var collector = new EventScriptDiagnosticTraceCollector();
        var host = EventScriptHost.CreateBuilder()
            .WithRandom(EventScriptRandomGenerator.FromSequence(2, 5))
            .WithDiagnosticCollector(collector)
            .Build()
            .SubscribeForScript(EventScriptManager.Compile(script));

        host.Publish(EventScriptMessage.Message("Roll"));
        host.Publish(EventScriptMessage.Message("Roll"));
        var emitted = collector.Events.Where(evt => evt.Kind == EventScriptDiagnosticEventKind.EventPublished).ToArray();

        Assert.AreEqual(2, Convert.ToInt32(emitted[0].Arguments["value"].AsInteger()));
        Assert.AreEqual(5, Convert.ToInt32(emitted[1].Arguments["value"].AsInteger()));
    }

    [TestMethod]
    public void IfAndForCanUseSingleStatementsWithoutBlocks()
    {
        const string script = """
            on Start(first, second, items) {
                if first publish One
                else if second publish Two
                else publish Three

                for item in items publish Seen(item: item)
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            false,
            true,
            EventScriptValue.List(new EventScriptValue[] { 2m, 4m }));

        Assert.AreEqual("Two", result.EmittedEvents[0].Message);
        Assert.AreEqual("Seen", result.EmittedEvents[1].Message);
        Assert.AreEqual(2m, result.EmittedEvents[1].Arguments["item"].AsNumber());
        Assert.AreEqual("Seen", result.EmittedEvents[2].Message);
        Assert.AreEqual(4m, result.EmittedEvents[2].Arguments["item"].AsNumber());
    }

    [TestMethod]
    public void BlockScopesDoNotLeakVariablesOutsideIfForAndSeededRandomBlocks()
    {
        const string script = """
            on Start(items, seed) {
                if true {
                    let fromIf be 10
                }

                for item in items {
                    let fromLoop be item
                }

                :random with seed {
                    let fromRandom be :random from 1 to 6
                    publish Inner(value: fromRandom)
                }

                if fromIf has value publish IfLeak
                if fromLoop has value publish LoopLeak
                if fromRandom has value publish RandomLeak
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.List(new EventScriptValue[] { 1m, 2m }),
            7m);

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Inner", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void SeededRandomExpressionsReplayTheSameSequenceForTheSameSeed()
    {
        const string script = """
            on Start(seed) {
                let first be :random with seed :list[:select item from 1 to 4 -> :random from 1 to 20]
                let second be :random with seed :list[:select item from 1 to 4 -> :random from 1 to 20]
                publish Done(first: first, second: second)
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Start", 42m);
        var args = result.EmittedEvents[0].Arguments;

        CollectionAssert.AreEqual(
            args["first"].AsList().Select(item => item.AsInteger()).ToArray(),
            args["second"].AsList().Select(item => item.AsInteger()).ToArray());
    }

    [TestMethod]
    public void SeededRandomStatementScopesPublishDeterministicValuesWithoutChangingTheOuterSequence()
    {
        const string script = """
            on Start(seed) {
                let outerBefore be :random from 1 to 20
                :random with seed {
                    publish Inner(value: :random from 1 to 20)
                }
                let outerAfter be :random from 1 to 20
                publish Done(before: outerBefore, after: outerAfter)
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(5, 9);
        var interpreter = EventScriptManager.Compile(script);

        var first = interpreter.InvokePositional(new EventScriptInvocationContext { Random = random }, "Start", 42m);
        var second = interpreter.InvokePositional(new EventScriptInvocationContext { Random = EventScriptRandomGenerator.FromSequence(5, 9) }, "Start", 42m);

        Assert.AreEqual("Inner", first.EmittedEvents[0].Message);
        Assert.AreEqual(first.EmittedEvents[0].Arguments["value"].AsInteger(), second.EmittedEvents[0].Arguments["value"].AsInteger());
        Assert.AreEqual(5L, first.EmittedEvents[1].Arguments["before"].AsInteger());
        Assert.AreEqual(9L, first.EmittedEvents[1].Arguments["after"].AsInteger());
    }

    [TestMethod]
    public void TextIterationYieldsSingleCharacterItems()
    {
        const string script = """
            on Start(text) {
                for item in text {
                    if item = 'a' {
                        publish Found(arg1: item);
                    }
                }
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional("Start", "ab");

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Found", result.EmittedEvents[0].Message);
        Assert.AreEqual("a", result.EmittedEvents[0].Arguments[0].AsText());
    }

    [TestMethod]
    public void DroppingTooManyDiceLeavesAnEmptyRoll()
    {
        const string script = """
            on Roll {
                let value be :dice 2d6[:drop lowest 3];
                publish Done(arg1: :len value);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(1, 2);
        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(random, "Roll");
        Assert.AreEqual(0, Convert.ToInt32(result.EmittedEvents[0].Arguments[0].AsInteger()));
    }

    [TestMethod]
    public void DiceCanBeSlicedAndDegradeToListsWhenExplicitlyResorted()
    {
        const string script = """
            on Roll {
                let baseDice be :dice 4d6;
                let kept be :dice 4d6[:take highest 2];
                let dropped be :dice 4d6[:drop lowest 1];
                let resorted be baseDice[:sort ascending];
                publish Done(arg1: baseDice, arg2: :len baseDice, arg3: baseDice[1], arg4: baseDice[2], arg5: baseDice[3], arg6: baseDice[4], arg7: kept, arg8: dropped, arg9: resorted);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(
            2, 6, 3, 5,
            1, 4, 6, 2,
            3, 2, 6, 1);
        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(random, "Roll");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueType.Dice, args[0].Type);
        Assert.AreEqual(4, Convert.ToInt32(args[1].AsInteger()));
        Assert.AreEqual(6, Convert.ToInt32(args[2].AsInteger()));
        Assert.AreEqual(5, Convert.ToInt32(args[3].AsInteger()));
        Assert.AreEqual(3, Convert.ToInt32(args[4].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(args[5].AsInteger()));

        Assert.AreEqual(EventScriptValueType.Dice, args[6].Type);
        Assert.AreEqual(10m, args[6].AsNumber());
        CollectionAssert.AreEqual(new long[] { 6, 4 }, args[6].AsList().Select(x => x.AsInteger()).ToArray());

        Assert.AreEqual(EventScriptValueType.Dice, args[7].Type);
        Assert.AreEqual(11m, args[7].AsNumber());
        CollectionAssert.AreEqual(new long[] { 6, 3, 2 }, args[7].AsList().Select(x => x.AsInteger()).ToArray());

        Assert.AreEqual(EventScriptValueType.List, args[8].Type);
        CollectionAssert.AreEqual(new long[] { 2, 3, 5, 6 }, args[8].AsList().Select(x => x.AsInteger()).ToArray());
    }

    [TestMethod]
    public void TypeTagsCanRecognizeSupportedValueKinds()
    {
        const string script = """
            on Start(a, b, c, d, e, f, g, h) {
                publish Done(arg1: a is :tag, arg2: b is :decimal, arg3: c is :integer, arg4: d is :text, arg5: e is :list, arg6: f is :dictionary, arg7: g is :optional, arg8: h is :set);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(
            "Start",
            EventScriptValue.Tag("name"),
            EventScriptValue.Decimal(1.5m),
            EventScriptValue.Integer(2),
            EventScriptValue.Text("x"),
            EventScriptValue.List(new EventScriptValue[] { 1m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["v"] = 1m }),
            EventScriptValue.OptionalNone(),
            EventScriptValue.Set(new EventScriptValue[] { 1m }));

        var args = result.EmittedEvents[0].Arguments;
        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsTrue(args[1].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
        Assert.IsTrue(args[3].AsBoolean());
        Assert.IsTrue(args[4].AsBoolean());
        Assert.IsTrue(args[5].AsBoolean());
        Assert.IsTrue(args[6].AsBoolean());
    }

    [TestMethod]
    public void IncompatibleIfAndForSourcesStayLenient()
    {
        const string script = """
            on Start {
                if 'abc' {
                    publish IfTrue;
                } else {
                    publish IfFalse;
                }

                for item in 123 {
                    publish Loop(item: item);
                }
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("IfFalse", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void SqlStyleInequalityChangesBranchingOutcomes()
    {
        const string script = """
            on Start {
                if 1 <> 2 {
                    publish Different;
                }
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Different", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void DiceCanContributeTheirTotalToArithmetic()
    {
        const string script = """
            on Start {
                let value be :dice 2d6 + 1;
                publish Done(arg1: value);
            }
            """;

        var random = EventScriptRandomGenerator.FromSequence(4, 2);
        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.InvokePositional(random, "Start");
        var value = result.EmittedEvents[0].Arguments[0];

        Assert.IsFalse(value.IsNaN());
        Assert.AreEqual(7m, value.AsNumber());
    }

    [TestMethod]
    public void OverflowingDecimalArithmeticProducesInfinity()
    {
        const string script = """
            on Start {
                let pos be 79228162514264337593543950335 + 1;
                let neg be 0 - 79228162514264337593543950335 - 1;
                publish Done(arg1: pos, arg2: neg);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].IsInfinity());
        Assert.IsFalse(args[0].IsNegativeInfinity());
        Assert.IsTrue(args[1].IsInfinity());
        Assert.IsTrue(args[1].IsNegativeInfinity());
    }

    [TestMethod]
    public void MissingAndInvalidValuesPropagateWithoutThrowing()
    {
        const string script = """
            on Start {
                let invalidNumber as :decimal be 'abc';
                let optResult be invalidNumber + 2;
                let nothing be missing;
                let nothingResult be nothing * 3;
                publish Done(arg1: invalidNumber, arg2: optResult, arg3: nothingResult);
            }
            """;

        var interpreter = EventScriptManager.Compile(script);
        var result = interpreter.Invoke("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].IsNaN());
        Assert.IsTrue(args[1].IsNaN());
        Assert.AreEqual(EventScriptValueType.Nothing, args[2].Type);
    }

}
