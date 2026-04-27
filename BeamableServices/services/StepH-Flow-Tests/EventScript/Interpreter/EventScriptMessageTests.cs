using StepH.Flow.EventScript;
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
    public void SignatureIdIncludesMessageNameAndNormalizesWhitespace()
    {
        var shoot = new EventScriptMessageSignature(" Shoot ", ["unit", "target"]);
        var run = new EventScriptMessageSignature("Run", ["target", "unit"]);
        var message = new EventScriptMessage(" Shoot ", new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            ["target"] = EventScriptValue.Integer(2),
            ["unit"] = EventScriptValue.Integer(1)
        });

        Assert.AreEqual("Shoot(target,unit)", shoot.SignatureId);
        Assert.AreEqual("Run(target,unit)", run.SignatureId);
        Assert.AreNotEqual(shoot.SignatureId, run.SignatureId);
        Assert.AreEqual("Shoot", message.Name);
        Assert.AreEqual(shoot.SignatureId, message.SignatureId);
        Assert.IsTrue(shoot.Matches(message));
    }

    [TestMethod]
    public void CompiledScriptExposesMessageDefinitions()
    {
        const string script = """
                              module MessageDefinitions
                              on Start(playerId) {
                                  publish Done(playerId: playerId)
                              }
                              """;

        var compiled = EventScriptManager.Compile(script);

        Assert.IsTrue(compiled.MessageDefinitions.ContainsKey("Start"));
        Assert.HasCount(1, compiled.MessageDefinitions["Start"]);
    }
}
