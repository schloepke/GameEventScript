using StepH.Flow.EventScript;

namespace StepH_Flow_Tests.EventScript;

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
                    publish Passed(:len values);
                } else {
                    publish Failed;
                }
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", EventScriptValue.List([1m, 5m, 9m]), 7m);

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
                publish Result(a, b);
            }
            """;

        var random = new QueueRandom(4, 1, 6, 3, 5);
        var interpreter = EventScriptInterpreter.Compile(script, random);

        var result = interpreter.Emit("Roll");
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
                    publish SeenKey(key, player[key]);
                }

                for item in valueIterator {
                    publish SeenValue(item);
                }

                for target in maybeValues {
                    publish SeenMaybe(target);
                }

                for entry in entries {
                    publish SeenEntry(entry.key, entry[:value]);
                }

                publish Done(keyCount, valueCount, maybeCount, entryCount, dictKeys, valueIterator, entries);
            }
            """;

        var player = EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>
        {
            ["name"] = "Mark",
            ["age"] = 25m
        });
        var values = EventScriptValue.List([10m, 20m, 30m]);
        var maybeTarget = EventScriptValue.OptionalSome(EventScriptValue.Text("boss"));

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", player, values, maybeTarget);

        Assert.AreEqual("SeenKey", result.EmittedEvents[0].Message);
        Assert.AreEqual(EventScriptValueKind.Tag, result.EmittedEvents[0].Arguments[0].Kind);
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
        Assert.AreEqual(EventScriptValueKind.Iterator, done[4].Kind);
        Assert.AreEqual(EventScriptValueKind.Iterator, done[5].Kind);
        Assert.AreEqual(EventScriptValueKind.Iterator, done[6].Kind);
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
                publish Done(alwaysHit, neverHit, quarterHit, weightedTarget.name, weightedPair[1].name, weightedPair[2].name);
            }
            """;

        var units = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Goblin", ["weight"] = 1m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["weight"] = 3m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Dragon", ["weight"] = 6m })
        ]);

        var random = new QueueRandom(249999, 900000, 0, 999999);
        var interpreter = EventScriptInterpreter.Compile(script, random);
        var args = interpreter.Emit("Start", units).EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].AsBoolean());
        Assert.IsFalse(args[1].AsBoolean());
        Assert.IsTrue(args[2].AsBoolean());
        Assert.AreEqual("Dragon", args[3].AsText());
        Assert.AreEqual("Goblin", args[4].AsText());
        Assert.AreEqual("Dragon", args[5].AsText());
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
                publish Done(:len names, :len positive, total, positiveCount, averagePoints, highestItem.name, lowestItem.name);
            }
            """;

        var items = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "a", ["points"] = 3m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "b", ["points"] = -1m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "c", ["points"] = 4m })
        ]);

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Compute", items);

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
                publish Done(
                    hand[1], hand[2], hand[3],
                    restCards[1], restCards[2],
                    target.name,
                    randomTargets[1].name, randomTargets[2].name);
            }
            """;

        var enemies = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Goblin", ["alive"] = false }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Orc", ["alive"] = true }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Mage", ["alive"] = true }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["alive"] = true })
        ]);

        var random = new QueueRandom(1, 0, 1, 1, 0, 1);
        var interpreter = EventScriptInterpreter.Compile(script, random);
        var args = interpreter.Emit("Start", enemies).EmittedEvents[0].Arguments;

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
                publish Done(distance, boundedHigh, boundedLow, highest, lowest);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var args = interpreter.Emit("Start").EmittedEvents[0].Arguments;

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
                publish Done(
                    ratio,
                    ratio as :decimal,
                    ratio as :integer,
                    percent,
                    hp[:current],
                    hp[:maximum],
                    hp[:percentage],
                    hp is :meter,
                    stalled[:percentage]);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var args = interpreter.Emit("Start").EmittedEvents[0].Arguments;

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
                publish Done(weakest.name, fastest.name);
            }
            """;

        var units = EventScriptValue.List([
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Knight", ["hp"] = 10m, ["speed"] = 3m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Mage", ["hp"] = 4m, ["speed"] = 5m }),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Orc", ["hp"] = 8m, ["speed"] = 2m })
        ]);

        var interpreter = EventScriptInterpreter.Compile(script);
        var args = interpreter.Emit("Start", units).EmittedEvents[0].Arguments;

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
                publish Done(
                    appended[4], prepended[1], mergedList[5],
                    commonList[1], commonList[2], :len leftOnly, leftOnly[2],
                    3 in mergedSet, 2 in commonSet,
                    mergedDict.age, mergedDict.city, mergedDictByMerge.age, mergedDictByPlus.age,
                    zipped[1].left, zipped[1].right, zipped[2].left, zipped[2].right, :len zipped);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var args = interpreter.Emit("Start").EmittedEvents[0].Arguments;

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
                publish Done(
                    ascendingValues[1], ascendingValues[2], ascendingValues[3],
                    descendingValues[1], descendingValues[2], descendingValues[3],
                    byPriority[1].name, byPriority[2].name, byPriority[3].name);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
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
                publish Done(
                    firstValue, lastValue,
                    firstAlive.name, lastAlive.name, singleBoss.name, missingBoss,
                    :len distinctValues,
                    distinctFactions[1].name, distinctFactions[2].name,
                    groups[:melee][1].name, groups[:melee][2].name,
                    groups[:ranged][1].name);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
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
                publish Done(
                    sortedValues[1], sortedValues[2], sortedValues[3],
                    sortedTags[1], sortedTags[2], sortedTags[3],
                    sortedRoll[1], sortedRoll[2], sortedRoll[3], sortedRoll[4]);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
            "SortAll",
            EventScriptValue.List([3m, 1m, 2m]),
            EventScriptValue.Set([EventScriptValue.Text("beta"), EventScriptValue.Text("alpha"), EventScriptValue.Text("gamma")]),
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 6, 4, 2, 1 })));
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
                publish Done(shuffledTags, drawnTag, shuffledDice[1], :len drawnDice);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script, new QueueRandom(1, 2, 3, 1));
        var args = interpreter.Emit("Start").EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueKind.Nothing, args[0].Kind);
        Assert.AreEqual(EventScriptValueKind.Nothing, args[1].Kind);
        Assert.IsTrue(args[2].Kind is EventScriptValueKind.Integer or EventScriptValueKind.Number);
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
                publish Done(
                    reversed[1], reversed[2], reversed[3],
                    reversedDice[1], :len reversedDice,
                    reversedSet,
                    hasTwo, hasAll, hasAny,
                    textContains, textContainsAll, dictContains,
                    hasOrc, hasNestedOwner);
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

        var interpreter = EventScriptInterpreter.Compile(script, new QueueRandom(2, 5, 6));
        var args = interpreter.Emit("Start", units).EmittedEvents[0].Arguments;

        Assert.AreEqual(3m, args[0].AsNumber());
        Assert.AreEqual(2m, args[1].AsNumber());
        Assert.AreEqual(1m, args[2].AsNumber());
        Assert.AreEqual(2L, args[3].AsInteger());
        Assert.AreEqual(3, Convert.ToInt32(args[4].AsInteger()));
        Assert.AreEqual(EventScriptValueKind.Nothing, args[5].Kind);
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
                publish Done(:len myList, myValues.name, myValues.position, :len emptyValues);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(3, Convert.ToInt32(result.EmittedEvents[0].Arguments[0].AsInteger()));
        Assert.AreEqual("Hello", result.EmittedEvents[0].Arguments[1].AsText());
        Assert.AreEqual(1m, result.EmittedEvents[0].Arguments[2].AsNumber());
        Assert.AreEqual(0, Convert.ToInt32(result.EmittedEvents[0].Arguments[3].AsInteger()));
    }

    [TestMethod]
    public void EscapedQuotesStayInsideTextValues()
    {
        const string script = """
            on Start {
                let text be 'Hello ''World'', I''m here';
                publish Done(text);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");

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
                publish Done(
                    tagOk,
                    numberOk,
                    numberFail,
                    integerOk,
                    booleanOk,
                    :len listOk,
                    :len setOk,
                    diceOk[1], :len diceOk);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueKind.Tag, args[0].Kind);
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
                publish Done(textLen, listLen, dictLen, setLen, diceLen, nothingLen, optionalLen, decimalLen, booleanLen);
            }
            """;

        var random = new QueueRandom(2, 6, 3, 5);
        var interpreter = EventScriptInterpreter.Compile(script, random);
        var result = interpreter.Emit("Start", EventScriptValue.OptionalSome("x"));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(3, Convert.ToInt32(args[0].AsInteger()));
        Assert.AreEqual(3, Convert.ToInt32(args[1].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(args[2].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(args[3].AsInteger()));
        Assert.AreEqual(4, Convert.ToInt32(args[4].AsInteger()));
        Assert.AreEqual(0, Convert.ToInt32(args[5].AsInteger()));
        Assert.AreEqual(1, Convert.ToInt32(args[6].AsInteger()));
        Assert.AreEqual(EventScriptValueKind.Nothing, args[7].Kind);
        Assert.AreEqual(EventScriptValueKind.Nothing, args[8].Kind);
    }

    [TestMethod]
    public void OneBasedLookupsReturnNothingWhenValuesAreMissing()
    {
        const string script = """
            on Start {
                let values be [10, 20, 30];
                let entries be [name: 'Ada'];
                let tags be :set['alpha', 'beta'];
                publish Done(
                    values[1],
                    values[3],
                    values[0],
                    values[4],
                    entries['name'],
                    entries['missing'],
                    tags[1],
                    tags[3]);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(10m, args[0].AsNumber());
        Assert.AreEqual(30m, args[1].AsNumber());
        Assert.AreEqual(EventScriptValueKind.Nothing, args[2].Kind);
        Assert.AreEqual(EventScriptValueKind.Nothing, args[3].Kind);
        Assert.AreEqual("Ada", args[4].AsText());
        Assert.AreEqual(EventScriptValueKind.Nothing, args[5].Kind);
        Assert.AreEqual("alpha", args[6].AsText());
        Assert.AreEqual(EventScriptValueKind.Nothing, args[7].Kind);
    }

    [TestMethod]
    public void DictionaryValuesCanBeReadByPropertyLiteralTextTagOrDynamicKey()
    {
        const string script = """
            on Start(entry, keyName) {
                let lookupProperty be :name;
                publish Done(entry.name, entry['name'], entry[:name], entry[lookupProperty], entry[keyName], entry['missing']);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
            "Start",
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["name"] = "Ada" }),
            EventScriptValue.Text("name"));
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual("Ada", args[0].AsText());
        Assert.AreEqual("Ada", args[1].AsText());
        Assert.AreEqual("Ada", args[2].AsText());
        Assert.AreEqual("Ada", args[3].AsText());
        Assert.AreEqual("Ada", args[4].AsText());
        Assert.AreEqual(EventScriptValueKind.Nothing, args[5].Kind);
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
                publish Done(
                    hasOpt, hasEmptyText, hasEmptyList, hasEmptyDict, hasZero, hasFalse, hasNaN, hasInfinity,
                    isEmptyOpt, isEmptyText, isEmptyList, isEmptyDict, isEmptyDice, isEmptyNaN, isEmptyInfinity, isNotEmptyList, isNotFalse,
                    optValue, listValue[1], textValue, dictValue.name, missingTextValue);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
            "Start",
            EventScriptValue.OptionalSome(7m),
            "",
            EventScriptValue.List([]),
            EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>()),
            0m,
            false,
            EventScriptValue.Dice(EventScriptDice.Create(Array.Empty<int>())),
            EventScriptValue.NumberNaN(),
            EventScriptValue.NumberInfinity());
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
                publish Done(inText, inList, inSet, inDice, inDict, missingKey, dictValue, startsText, endsText, startsList, endsList, startsDice, endsDice);
            }
            """;

        var random = new QueueRandom(2, 6, 3, 5, 2, 6, 3, 5, 2, 6, 3, 5);
        var interpreter = EventScriptInterpreter.Compile(script, random);
        var args = interpreter.Emit("Start").EmittedEvents[0].Arguments;

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
                publish Done(
                    pairRoll[:has pair],
                    pairRoll[:has pair of 6],
                    pairRoll[:has three of a kind],
                    tripleRoll[:has three of a kind],
                    tripleRoll[:has three of 6],
                    sixRoll[:has six of a kind],
                    sevenRoll[:has seven of 6],
                    fullHouseRoll[:has full house],
                    straightRoll[:has straight]);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
            "Start",
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 6, 6, 4, 3, 2 })),
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 6, 6, 6, 4, 3 })),
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 5, 5, 5, 2, 2 })),
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 6, 6, 5, 4, 3, 2 })),
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 4, 4, 4, 4, 4, 4 })),
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 6, 6, 6, 6, 6, 6, 6 })));
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
                publish Done(
                    pairRoll[1], pairRoll[2], :len pairRoll,
                    fullHouseRoll[1], fullHouseRoll[2], fullHouseRoll[3], fullHouseRoll[4], fullHouseRoll[5],
                    straightCards[1], straightCards[2], straightCards[3], straightCards[4],
                    missingPair[1]);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
            "Start",
            EventScriptValue.Dice(EventScriptDice.Create(new[] { 6, 6, 5, 5, 5 })),
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
                publish Done(
                    cards[:has pair],
                    cards[:has three of 'King'],
                    cards[:has full house],
                    orderedCards[:has straight],
                    plainText[:has pair]);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
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
                publish Done(opt :default 10, missingValue :default 20);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", EventScriptValue.OptionalNone(), EventScriptValue.Nothing);
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
                publish Done(score);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);

        var boolResult = interpreter.Emit("Start", true);
        var numberResult = interpreter.Emit("Start", 7m);
        var textResult = interpreter.Emit("Start", "x");

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
                publish Done(score);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);

        Assert.AreEqual(12m, interpreter.Emit("Start", true).EmittedEvents[0].Arguments[0].AsNumber());
        Assert.AreEqual(12m, interpreter.Emit("Start", 2m).EmittedEvents[0].Arguments[0].AsNumber());
        Assert.AreEqual(5m, interpreter.Emit("Start", "x").EmittedEvents[0].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void TaggedRoundingOperationsAndIntegerConversionStayPredictable()
    {
        const string script = """
            on Start {
                let base as :integer be '12.7';
                publish Done(base, :floor 12.7, :ceil 12.1, :roundeven 12.5, :roundeven 13.5, :rounddown 12.1, :roundup 12.1, :round 12.5);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
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
                publish Done(nanValue, nanPropagated, nothing, nothingPropagated);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].IsNaN());
        Assert.IsTrue(args[1].IsNaN());
        Assert.AreEqual(EventScriptValueKind.Nothing, args[2].Kind);
        Assert.AreEqual(EventScriptValueKind.Nothing, args[3].Kind);
    }

    [TestMethod]
    public void ReversedRandomBoundsDoNotCrashARun()
    {
        const string script = """
            on Roll(min, max) {
                let value be :random min to max;
                publish Done(value);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Roll", 9m, 2m);
        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void ReadingAPropertyFromNothingReturnsNothing()
    {
        const string script = """
            on Inspect(item) {
                publish Done(item.name);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.EmitClr("Inspect", (object?)null);
        Assert.AreEqual(EventScriptValueKind.Nothing, result.EmittedEvents[0].Arguments[0].Kind);
    }

    [TestMethod]
    public void MissingDictionaryPropertiesReturnNothing()
    {
        const string script = """
            on Inspect(item) {
                publish Done(item.name);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Inspect", EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>()));
        Assert.AreEqual(EventScriptValueKind.Nothing, result.EmittedEvents[0].Arguments[0].Kind);
    }

    [TestMethod]
    public void DictionaryPropertiesCanBeReadByName()
    {
        const string script = """
            on Inspect(item) {
                publish Done(item.points);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Inspect", EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue> { ["points"] = 7m }));

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(7m, result.EmittedEvents[0].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void DividingByZeroProducesInfinity()
    {
        const string script = """
            on Start {
                let value be 6 / 0;
                publish Done(value);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
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
                publish Done(value);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
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

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Inspect", EventScriptValue.List(new EventScriptValue[] { 1m, 5m, 9m }), 7m);

        CollectionAssert.AreEquivalent(
            new[] { "values", "threshold", "passed" },
            result.Variables.Keys.ToArray());
        Assert.AreEqual(7m, result.Variables["threshold"].AsNumber());
        Assert.IsTrue(result.Variables["passed"].AsBoolean());
        Assert.IsFalse(result.Variables.ContainsKey("item"));
        Assert.IsFalse(result.Variables.ContainsKey("doubled"));
    }

    [TestMethod]
    public void PublishingFromOneHandlerCanTriggerTheNextMessage()
    {
        const string script = """
            on Start(value) {
                publish Next(value + 1);
            }

            on Next(value) {
                publish Done(value);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", 2m);

        Assert.HasCount(2, result.EmittedEvents);
        Assert.AreEqual("Next", result.EmittedEvents[0].Message);
        Assert.AreEqual(3m, result.EmittedEvents[0].Arguments[0].AsNumber());
        Assert.AreEqual("Done", result.EmittedEvents[1].Message);
        Assert.AreEqual(3m, result.EmittedEvents[1].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void EventLoopsStopAtTheConfiguredProcessingLimit()
    {
        const string script = """
            on Start {
                publish Loop;
            }

            on Loop {
                publish Start;
            }
            """;

        var context = new EventScriptCompilationContext
        {
            MaxProcessedEventsPerRun = 4
        };
        var interpreter = EventScriptInterpreter.Compile(script, context: context);
        var result = interpreter.Emit("Start");
        Assert.HasCount(4, result.EmittedEvents);
        Assert.AreEqual("Loop", result.EmittedEvents[0].Message);
        Assert.AreEqual("Start", result.EmittedEvents[1].Message);
        Assert.AreEqual("Loop", result.EmittedEvents[2].Message);
        Assert.AreEqual("Start", result.EmittedEvents[3].Message);
    }

    [TestMethod]
    public void PublishedMessagesCanReachBoundExternalSubscribers()
    {
        const string script = """
            on Start(playerId) {
                publish Notify(playerId, 3);
            }
            """;

        var invocations = new List<EventScriptValue[]>();
        var compileContext = new EventScriptCompilationContext()
            .BindExternal("Notify", args => invocations.Add(args.ToArray()), parameterCount: 2);
        var interpreter = EventScriptInterpreter.Compile(script, context: compileContext);

        var result = interpreter.Emit("Start", "p1");

        Assert.HasCount(1, invocations);
        Assert.AreEqual("p1", invocations[0][0].AsText());
        Assert.AreEqual(3m, invocations[0][1].AsNumber());
        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Notify", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void ExternalSubscribersRunAfterScriptHandlers()
    {
        const string script = """
            on Start(value) {
                publish Notify(value);
            }

            on Notify(value) {
                publish SeenByScript(value + 1);
            }
            """;

        var invocations = new List<string>();
        var compileContext = new EventScriptCompilationContext()
            .BindExternal("Notify", args => invocations.Add($"external:{args[0].AsNumber()}"), parameterCount: 1);
        var interpreter = EventScriptInterpreter.Compile(script, context: compileContext);

        var result = interpreter.Emit("Start", 4m);

        Assert.HasCount(1, invocations);
        Assert.AreEqual("external:4", invocations[0]);
        Assert.HasCount(2, result.EmittedEvents);
        Assert.AreEqual("Notify", result.EmittedEvents[0].Message);
        Assert.AreEqual("SeenByScript", result.EmittedEvents[1].Message);
    }

    [TestMethod]
    public void MultipleExternalSubscribersRunInRegistrationOrder()
    {
        const string script = """
            on Start(value) {
                publish Notify(value);
            }
            """;

        var invocations = new List<string>();
        var compileContext = new EventScriptCompilationContext()
            .BindExternal("Notify", args => invocations.Add($"first:{args[0].AsNumber()}"), parameterCount: 1)
            .BindExternal("Notify", args => invocations.Add($"second:{args[0].AsNumber()}"), parameterCount: 1);
        var interpreter = EventScriptInterpreter.Compile(script, context: compileContext);

        interpreter.Emit("Start", 4m);

        CollectionAssert.AreEqual(new[] { "first:4", "second:4" }, invocations);
    }

    [TestMethod]
    public void FailingExternalSubscribersDoNotStopLaterSubscribers()
    {
        const string script = """
            on Start(value) {
                publish Notify(value);
            }
            """;

        var invocations = new List<string>();
        var compileContext = new EventScriptCompilationContext()
            .BindExternal("Notify", _ => throw new InvalidOperationException("boom"), parameterCount: 1)
            .BindExternal("Notify", args => invocations.Add($"ok:{args[0].AsNumber()}"), parameterCount: 1);
        var interpreter = EventScriptInterpreter.Compile(script, context: compileContext);

        var result = interpreter.Emit("Start", 4m);

        CollectionAssert.AreEqual(new[] { "ok:4" }, invocations);
        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Notify", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void QueuedRunsDrainToTheSameResultAsEmit()
    {
        const string script = """
            on Start(value) {
                publish Next(value + 1);
            }

            on Next(value) {
                publish Done(value);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);

        var emitResult = interpreter.Emit("Start", 2m);
        var queuedResult = interpreter.Enqueue("Start", 2m).Drain();

        CollectionAssert.AreEqual(
            emitResult.EmittedEvents.Select(evt => evt.Message).ToArray(),
            queuedResult.EmittedEvents.Select(evt => evt.Message).ToArray());
        Assert.AreEqual(
            emitResult.EmittedEvents[1].Arguments[0].AsNumber(),
            queuedResult.EmittedEvents[1].Arguments[0].AsNumber());
    }

    [TestMethod]
    public void TextIterationYieldsSingleCharacterItems()
    {
        const string script = """
            on Start(text) {
                for item in text {
                    if item = 'a' {
                        publish Found(item);
                    }
                }
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", "ab");

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
                publish Done(:len value);
            }
            """;

        var random = new QueueRandom(1, 2);
        var interpreter = EventScriptInterpreter.Compile(script, random);
        var result = interpreter.Emit("Roll");
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
                publish Done(baseDice, :len baseDice, baseDice[1], baseDice[2], baseDice[3], baseDice[4], kept, dropped, resorted);
            }
            """;

        var random = new QueueRandom(
            2, 6, 3, 5,
            1, 4, 6, 2,
            3, 2, 6, 1);
        var interpreter = EventScriptInterpreter.Compile(script, random);
        var result = interpreter.Emit("Roll");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(EventScriptValueKind.Dice, args[0].Kind);
        Assert.AreEqual(4, Convert.ToInt32(args[1].AsInteger()));
        Assert.AreEqual(6, Convert.ToInt32(args[2].AsInteger()));
        Assert.AreEqual(5, Convert.ToInt32(args[3].AsInteger()));
        Assert.AreEqual(3, Convert.ToInt32(args[4].AsInteger()));
        Assert.AreEqual(2, Convert.ToInt32(args[5].AsInteger()));

        Assert.AreEqual(EventScriptValueKind.Dice, args[6].Kind);
        Assert.AreEqual(10m, args[6].AsNumber());
        CollectionAssert.AreEqual(new long[] { 6, 4 }, args[6].AsList().Select(x => x.AsInteger()).ToArray());

        Assert.AreEqual(EventScriptValueKind.Dice, args[7].Kind);
        Assert.AreEqual(11m, args[7].AsNumber());
        CollectionAssert.AreEqual(new long[] { 6, 3, 2 }, args[7].AsList().Select(x => x.AsInteger()).ToArray());

        Assert.AreEqual(EventScriptValueKind.List, args[8].Kind);
        CollectionAssert.AreEqual(new long[] { 2, 3, 5, 6 }, args[8].AsList().Select(x => x.AsInteger()).ToArray());
    }

    [TestMethod]
    public void TypeTagsCanRecognizeSupportedValueKinds()
    {
        const string script = """
            on Start(a, b, c, d, e, f, g, h) {
                publish Done(a is :tag, b is :decimal, c is :integer, d is :text, e is :list, f is :dictionary, g is :optional, h is :set);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit(
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
                    publish Loop(item);
                }
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");

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

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Different", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void DiceCanContributeTheirTotalToArithmetic()
    {
        const string script = """
            on Start {
                let value be :dice 2d6 + 1;
                publish Done(value);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script, new QueueRandom(4, 2));
        var result = interpreter.Emit("Start");
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
                publish Done(pos, neg);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
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
                publish Done(invalidNumber, optResult, nothingResult);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start");
        var args = result.EmittedEvents[0].Arguments;

        Assert.IsTrue(args[0].IsNaN());
        Assert.IsTrue(args[1].IsNaN());
        Assert.AreEqual(EventScriptValueKind.Nothing, args[2].Kind);
    }

    private sealed class QueueRandom(params int[] values) : IEventScriptRandom
    {
        private readonly Queue<int> _values = new(values);

        public int NextInclusive(int minInclusive, int maxInclusive)
        {
            if (_values.Count == 0)
            {
                throw new InvalidOperationException("No values left in random queue");
            }

            var value = _values.Dequeue();
            if (value < minInclusive || value > maxInclusive)
            {
                throw new InvalidOperationException($"Queued random value {value} out of expected range [{minInclusive}, {maxInclusive}]");
            }

            return value;
        }
    }
}
