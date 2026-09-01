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

    [TestMethod]
    public void PublicNamesUsePortableAsciiGrammarAndAsciiTrimming()
    {
        Assert.AreEqual("Ping", GameEventScriptMessageSignature.NormalizeMessageName("\tPing "));
        Assert.AreEqual("amount_2", GameEventScriptMessageSignature.NormalizeParameterName(" amount_2\t"));
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptMessageSignature.Create("Pïng", []));
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptMessageSignature.Create("Ping", ["ämount"]));
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptMessageSignature.Create("\u00A0Ping\u00A0", []));
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptMessage.Create("Ping", arguments: null, tags: ["#réady"]));
    }

    [TestMethod]
    public void HostTagValuesRemainCaseSensitiveAsciiSymbols()
    {
        Assert.AreEqual("Ready", GesValue.GesTag("Ready").TextValue);
        Assert.AreNotEqual(GesValue.GesTag("Ready"), GesValue.GesTag("ready"));
        Assert.ThrowsExactly<ArgumentException>(() => GesValue.GesTag("réady"));
    }
}
