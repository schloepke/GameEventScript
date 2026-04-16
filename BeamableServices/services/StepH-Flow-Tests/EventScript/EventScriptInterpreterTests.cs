using StepH.Flow.EventScript;

namespace StepH_Flow_Tests.EventScript;

[TestClass]
public class EventScriptInterpreterTests
{
    [TestMethod]
    public void Invoke_ExecutesControlFlowAndEmitsEvents()
    {
        const string script = """
            on Start(values, threshold) {
                let passed = values[any value where value > threshold];
                if passed {
                    emit Passed(values[count]);
                } else {
                    emit Failed;
                }
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", new object[] { 1m, 5m, 9m }, 7m);

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Passed", result.EmittedEvents[0].Message);
        Assert.AreEqual(3, Convert.ToInt32(result.EmittedEvents[0].Arguments[0]));
    }

    [TestMethod]
    public void Invoke_RandomAndDiceExpressions_WorkWithDeterministicRandom()
    {
        const string script = """
            on Roll {
                let a = random 1 to 6;
                let b = dice 4d6 keep highest 2;
                emit Result(a, b);
            }
            """;

        var random = new QueueRandom(4, 1, 6, 3, 5);
        var interpreter = EventScriptInterpreter.Compile(script, random);

        var result = interpreter.Emit("Roll");
        var args = result.EmittedEvents[0].Arguments;

        Assert.AreEqual(4, Convert.ToInt32(args[0]));
        Assert.AreEqual(11, Convert.ToInt32(args[1]));
    }

    [TestMethod]
    public void Invoke_SelectFilterAndSumSelectors_Work()
    {
        const string script = """
            on Compute(items) {
                let names = items[select item -> item.name];
                let positive = items[filter item where item.points > 0];
                let total = items[sum item -> item.points];
                emit Done(names[count], positive[count], total);
            }
            """;

        var items = new object?[]
        {
            new Dictionary<string, object?> { ["name"] = "a", ["points"] = 3m },
            new Dictionary<string, object?> { ["name"] = "b", ["points"] = -1m },
            new Dictionary<string, object?> { ["name"] = "c", ["points"] = 4m }
        };

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Compute", (object?)items);

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(3, Convert.ToInt32(result.EmittedEvents[0].Arguments[0]));
        Assert.AreEqual(2, Convert.ToInt32(result.EmittedEvents[0].Arguments[1]));
        Assert.AreEqual(6m, result.EmittedEvents[0].Arguments[2]);
    }

    [TestMethod]
    public void Invoke_RandomInvalidRange_ThrowsRuntimeException()
    {
        const string script = """
            on Roll(min, max) {
                let value = random min to max;
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        Assert.ThrowsExactly<EventScriptRuntimeException>(() => interpreter.Emit("Roll", 9m, 2m));
    }

    [TestMethod]
    public void Invoke_MemberAccessOnNull_ThrowsRuntimeException()
    {
        const string script = """
            on Inspect(item) {
                let value = item.name;
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var exception = Assert.ThrowsExactly<EventScriptRuntimeException>(
            () => interpreter.Emit("Inspect", new object?[] { null }));

        StringAssert.Contains(exception.Message, "Cannot access member 'name' on null");
    }

    [TestMethod]
    public void Invoke_MissingMember_ThrowsRuntimeException()
    {
        const string script = """
            on Inspect(item) {
                let value = item.name;
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var exception = Assert.ThrowsExactly<EventScriptRuntimeException>(
            () => interpreter.Emit("Inspect", (object?)new Dictionary<string, object?>()));

        StringAssert.Contains(exception.Message, "Member 'name' not found");
    }

    [TestMethod]
    public void Invoke_MemberAccess_WorksWithTypedDictionary()
    {
        const string script = """
            on Inspect(item) {
                emit Done(item.points);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Inspect", (object?)new Dictionary<string, decimal> { ["points"] = 7m });

        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.AreEqual(7m, result.EmittedEvents[0].Arguments[0]);
    }

    [TestMethod]
    public void Invoke_DivideByZero_ThrowsRuntimeException()
    {
        const string script = """
            on Start {
                let value = 6 / 0;
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var exception = Assert.ThrowsExactly<EventScriptRuntimeException>(() => interpreter.Emit("Start"));

        StringAssert.Contains(exception.Message, "Division by zero");
    }

    [TestMethod]
    public void Invoke_ModuloByZero_ThrowsRuntimeException()
    {
        const string script = """
            on Start {
                let value = 6 % 0;
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var exception = Assert.ThrowsExactly<EventScriptRuntimeException>(() => interpreter.Emit("Start"));

        StringAssert.Contains(exception.Message, "Modulo by zero");
    }

    [TestMethod]
    public void Invoke_ReturnsHandlerVariableSnapshotWithoutLeakingInnerScopes()
    {
        const string script = """
            on Inspect(values, threshold) {
                let passed = values[any value where value > threshold];
                for item in values {
                    let doubled = item + item;
                }
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Inspect", new object?[] { new object?[] { 1m, 5m, 9m }, 7m });

        CollectionAssert.AreEquivalent(
            new[] { "values", "threshold", "passed" },
            result.Variables.Keys.ToArray());
        Assert.AreEqual(7m, result.Variables["threshold"]);
        Assert.AreEqual(true, result.Variables["passed"]);
        Assert.IsFalse(result.Variables.ContainsKey("item"));
        Assert.IsFalse(result.Variables.ContainsKey("doubled"));
    }

    [TestMethod]
    public void Invoke_EmitDispatchesInternalMessages()
    {
        const string script = """
            on Start(value) {
                emit Next(value + 1);
            }

            on Next(value) {
                emit Done(value);
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", 2m);

        Assert.AreEqual(2, result.EmittedEvents.Count);
        Assert.AreEqual("Next", result.EmittedEvents[0].Message);
        Assert.AreEqual(3m, result.EmittedEvents[0].Arguments[0]);
        Assert.AreEqual("Done", result.EmittedEvents[1].Message);
        Assert.AreEqual(3m, result.EmittedEvents[1].Arguments[0]);
    }

    [TestMethod]
    public void Invoke_EmitRecursion_ThrowsRuntimeException()
    {
        const string script = """
            on Start {
                emit Loop;
            }

            on Loop {
                emit Start;
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var exception = Assert.ThrowsExactly<EventScriptRuntimeException>(() => interpreter.Emit("Start"));

        StringAssert.Contains(exception.Message, "Emit recursion detected");
    }

    [TestMethod]
    public void Invoke_EmitsDeclaredExternalMessages_ToBoundExternalEndpoint()
    {
        const string script = """
            external on Notify(playerId, points);

            on Start(playerId) {
                emit Notify(playerId, 3);
            }
            """;

        var invocations = new List<object?[]>();
        var compileContext = new EventScriptCompilationContext()
            .BindExternal("Notify", args => invocations.Add(args.ToArray()), parameterCount: 2);
        var interpreter = EventScriptInterpreter.Compile(script, context: compileContext);

        var result = interpreter.Emit("Start", "p1");

        Assert.AreEqual(1, invocations.Count);
        Assert.AreEqual("p1", invocations[0][0]);
        Assert.AreEqual(3m, invocations[0][1]);
        Assert.AreEqual(1, result.EmittedEvents.Count);
        Assert.AreEqual("Notify", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void Invoke_EmitsContextKnownExternalMessages_WithoutDeclaration()
    {
        const string script = """
            on Start(value) {
                emit Notify(value);
            }
            """;

        var invocations = new List<object?[]>();
        var compileContext = new EventScriptCompilationContext()
            .BindExternal("Notify", args => invocations.Add(args.ToArray()), parameterCount: 1);
        var interpreter = EventScriptInterpreter.Compile(script, context: compileContext);

        interpreter.Emit("Start", 4m);

        Assert.AreEqual(1, invocations.Count);
        Assert.AreEqual(4m, invocations[0][0]);
    }

    [TestMethod]
    public void Invoke_ForLoopOverString_UsesSingleCharacterStrings()
    {
        const string script = """
            on Start(text) {
                for item in text {
                    if item == 'a' {
                        emit Found(item);
                    }
                }
            }
            """;

        var interpreter = EventScriptInterpreter.Compile(script);
        var result = interpreter.Emit("Start", "ab");

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Found", result.EmittedEvents[0].Message);
        Assert.AreEqual("a", result.EmittedEvents[0].Arguments[0]);
    }

    [TestMethod]
    public void Invoke_DiceDropTooMany_ThrowsRuntimeException()
    {
        const string script = """
            on Roll {
                let value = dice 2d6 drop lowest 3;
            }
            """;

        Assert.ThrowsExactly<EventScriptParseException>(() => EventScriptInterpreter.Compile(script));
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
