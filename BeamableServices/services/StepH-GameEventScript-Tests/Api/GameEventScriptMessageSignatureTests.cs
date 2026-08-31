using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptMessageSignatureTests
{
    [TestMethod]
    public void MessageSignaturesCompareBySignatureId()
    {
        var left = GameEventScriptMessageSignature.Create(" Ping ", [" amount "]);
        var right = GameEventScriptMessageSignature.Create("Ping", ["amount"]);
        var differentParameter = GameEventScriptMessageSignature.Create("Ping", ["value"]);
        var differentShape = GameEventScriptMessageSignature.Create("Ping", ["amount", "kind"]);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, differentParameter);
        Assert.AreNotEqual(left, differentShape);
    }

    [TestMethod]
    public void MessagesCompareBySignatureArgumentsAndTags()
    {
        var left = GameEventScriptMessage.Create("Ping", new Dictionary<string, GesValue> { ["amount"] = GesValue.GesInteger(7) }, ["radio"]);
        var right = GameEventScriptMessage.Create("Ping", new Dictionary<string, GesValue> { ["amount"] = GesValue.GesInteger(7) }, ["#radio"]);
        var differentArgument = GameEventScriptMessage.Create("Ping", new Dictionary<string, GesValue> { ["amount"] = GesValue.GesInteger(8) }, ["radio"]);
        var differentTags = GameEventScriptMessage.Create("Ping", new Dictionary<string, GesValue> { ["amount"] = GesValue.GesInteger(7) }, ["silent"]);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, differentArgument);
        Assert.AreNotEqual(left, differentTags);
    }

    [TestMethod]
    public void MessagesCompareUnlabeledArgumentsByOrderedValue()
    {
        var signature = GameEventScriptMessageSignature.Create("Ping", ["_"]);
        var left = signature.WithArguments(GesValue.GesInteger(7));
        var right = GameEventScriptMessageSignature.Create("Ping", ["_"]).WithArguments(GesValue.GesInteger(7));
        var differentValue = signature.WithArguments(GesValue.GesInteger(8));

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, differentValue);
    }

    [TestMethod]
    public void HandlerValuesCompareBySignature()
    {
        var left = GesValue.GesHandler(GameEventScriptMessageSignature.Create("Ping", ["amount"]));
        var right = GesValue.GesHandler(GameEventScriptMessageSignature.Create("Ping", ["amount"]));
        var different = GesValue.GesHandler(GameEventScriptMessageSignature.Create("Ping", ["value"]));

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, different);
    }

    [TestMethod]
    public void MessageSignatureAlwaysCreatesNormalMessages()
    {
        var signature = GameEventScriptMessageSignature.Create("Ping", ["amount"]);

        var message = signature.CreateMessage([GesValue.GesInteger(7)]);
        Assert.IsNotNull(message);
        Assert.AreEqual("Ping(amount)", message.SignatureId);
    }
}
