using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Interpreter;

[TestClass]
public sealed class EventScriptMessageTests
{
    [TestMethod]
    public void MessageAndMessageHandlerExposeStableSignatures()
    {
        var definition = new EventScriptMessageSignature("Start", ["a", "b"]);
        var message = new EventScriptMessage("Start", new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["b"] = EventScriptValue.Integer(2),
            ["a"] = EventScriptValue.Integer(1)
        });

        Assert.IsTrue(definition.Matches(message));
        Assert.AreEqual("Start", message.Name);
        Assert.AreEqual(definition.SignatureId, message.SignatureId);
        Assert.AreEqual(2, message.Arguments.Count);
    }

    [TestMethod]
    public void CompiledScriptExposeMessageDefinitionsAndInvokeMessageApi()
    {
        const string script = """
                              module MessageDefinitions
                              on Start(playerId) {
                                  publish Done(playerId: playerId)
                              }
                              """;

        var compiled = EventScriptManager.Compile(script);
        var result = compiled.Invoke(new EventScriptMessage("Start", new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["playerId"] = EventScriptValue.Text("p1")
        }));

        Assert.IsTrue(compiled.MessageDefinitions.ContainsKey("Start"));
        Assert.HasCount(1, compiled.MessageDefinitions["Start"]);
        Assert.AreEqual("Start", result.InvocationMessage.Name);
        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
    }

    [TestMethod]
    public void HostCanSubscribeWithMessageHandlerAndPublishMessage()
    {
        const string script = """
                              module HostMessageTypes
                              on Start(value) {
                                  publish Notify(value: value)
                              }
                              """;

        var compiled = EventScriptManager.Compile(script);
        var seen = new List<string>();
        var host = EventScriptHost.CreateBuilder()
            .Build()
            .Load(compiled)
            .Subscribe(EventScriptMessageSignature.MessageSignature("Notify", ["value"]), context =>
            {
                seen.Add(context.Message);
                context.Publish(new EventScriptMessage("Done", new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
                {
                    ["value"] = context.Arguments["value"]
                }));
            });

        var result = host.Publish(new EventScriptMessage("Start", new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["value"] = EventScriptValue.Integer(3)
        }));

        CollectionAssert.AreEqual(new[] { "Notify" }, seen);
        Assert.AreEqual("Start", result.InvocationMessage.Name);
        Assert.AreEqual("Notify", result.EmittedEvents[0].Message);
        Assert.AreEqual("Done", result.EmittedEvents[1].Message);
    }

    [TestMethod]
    public void HandlerBindingCanCreateMessageValuesAndPublishThem()
    {
        const string script = """
                              module MessageBinding
                              on Start(unit, target) {
                                  let shoot as :handler be :handler Shoot(unit, target)
                                  let msg as :message be shoot(unit: unit, target: target)
                                  let isHandler be shoot is :handler
                                  let isMessage be msg is :message
                                  publish msg
                                  publish shoot(unit: unit, target: target)
                                  publish :message Shoot(unit: unit, target: target)
                                  publish Done(isHandler: isHandler, isMessage: isMessage)
                              }
                              """;

        var compiled = EventScriptManager.Compile(script);
        IReadOnlyDictionary<string, EventScriptValue> args = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["unit"] = EventScriptValue.Text("u1"),
            ["target"] = EventScriptValue.Text("t1")
        };
        var result = compiled.Invoke("Start", args);

        Assert.HasCount(4, result.EmittedEvents);
        Assert.AreEqual("Shoot", result.EmittedEvents[0].Message);
        Assert.AreEqual("Shoot", result.EmittedEvents[1].Message);
        Assert.AreEqual("Shoot", result.EmittedEvents[2].Message);
        Assert.AreEqual("Done", result.EmittedEvents[3].Message);
        Assert.IsTrue(result.EmittedEvents[3].Arguments["isHandler"].AsBoolean());
        Assert.IsTrue(result.EmittedEvents[3].Arguments["isMessage"].AsBoolean());
    }

    [TestMethod]
    public void InvalidHandlerBindingOrNonMessagePublishIsLenientNoOp()
    {
        const string script = """
                              module MessageBindingNoOp
                              on Start(unit, hp) {
                                  let shoot as :handler be :handler Shoot(unit, target)
                                  let invalid as :message be shoot(unit: unit, hp: hp)
                                  publish invalid
                                  publish unit
                                  publish Done(isMessage: invalid is :message, isNothing: invalid is :nothing)
                              }
                              """;

        var compiled = EventScriptManager.Compile(script);
        IReadOnlyDictionary<string, EventScriptValue> args = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["unit"] = EventScriptValue.Text("u1"),
            ["hp"] = EventScriptValue.Integer(10)
        };
        var result = compiled.Invoke("Start", args);

        Assert.HasCount(1, result.EmittedEvents);
        Assert.AreEqual("Done", result.EmittedEvents[0].Message);
        Assert.IsFalse(result.EmittedEvents[0].Arguments["isMessage"].AsBoolean());
        Assert.IsTrue(result.EmittedEvents[0].Arguments["isNothing"].AsBoolean());
    }
}
