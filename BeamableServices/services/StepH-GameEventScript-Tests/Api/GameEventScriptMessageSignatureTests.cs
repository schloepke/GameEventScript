using StepH.GameEventScript.Api;

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
        var left = GameEventScriptMessage.Create("Ping", new Dictionary<string, GameEventScriptBoxedValue> { ["amount"] = GameEventScriptBoxedValue.FromInteger(7) }, ["radio"]);
        var right = GameEventScriptMessage.Create("Ping", new Dictionary<string, GameEventScriptBoxedValue> { ["amount"] = GameEventScriptBoxedValue.FromInteger(7) }, [":radio"]);
        var differentArgument = GameEventScriptMessage.Create("Ping", new Dictionary<string, GameEventScriptBoxedValue> { ["amount"] = GameEventScriptBoxedValue.FromInteger(8) }, ["radio"]);
        var differentTags = GameEventScriptMessage.Create("Ping", new Dictionary<string, GameEventScriptBoxedValue> { ["amount"] = GameEventScriptBoxedValue.FromInteger(7) }, ["silent"]);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, differentArgument);
        Assert.AreNotEqual(left, differentTags);
    }

    [TestMethod]
    public void MessagesCompareUnlabeledArgumentsByOrderedValue()
    {
        var signature = GameEventScriptMessageSignature.Create("Ping", ["_"]);
        var left = signature.WithArguments(GameEventScriptBoxedValue.FromInteger(7));
        var right = GameEventScriptMessageSignature.Create("Ping", ["_"]).WithArguments(GameEventScriptBoxedValue.FromInteger(7));
        var differentValue = signature.WithArguments(GameEventScriptBoxedValue.FromInteger(8));

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, differentValue);
    }

    [TestMethod]
    public void HandlerValuesCompareBySignature()
    {
        var left = GameEventScriptBoxedValue.FromHandler(GameEventScriptMessageSignature.Create("Ping", ["amount"]));
        var right = GameEventScriptBoxedValue.FromHandler(GameEventScriptMessageSignature.Create("Ping", ["amount"]));
        var different = GameEventScriptBoxedValue.FromHandler(GameEventScriptMessageSignature.Create("Ping", ["value"]));

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, different);
    }

    [TestMethod]
    public void HandlerDescriptorCanMatchByMessageName()
    {
        var exact = GameEventScriptMessageSignature.Create("Ping", ["message"]);
        var wildcard = new GameEventScriptMessageHandlerDescriptor(
            GameEventScriptMessageSignature.Create("Ping", ["ignored"]),
            (_, _) => { },
            matchArguments: false);
        var message = GameEventScriptMessage.Create("Ping", ("amount", GameEventScriptBoxedValue.FromInteger(7)));

        Assert.AreEqual("Ping(message)", exact.SignatureId);
        Assert.AreEqual("Ping(*)", wildcard.DispatchSignatureId);
        Assert.AreEqual("Ping(ignored)", wildcard.Signature.SignatureId);
        Assert.IsFalse(exact.Matches(message));
        Assert.IsFalse(wildcard.Signature.Matches(message));
        Assert.IsFalse(wildcard.MatchArguments);
    }

    [TestMethod]
    public void HandlerDescriptorWildcardKeepsTheSignatureShape()
    {
        var noParameters = new GameEventScriptMessageHandlerDescriptor(GameEventScriptMessageSignature.Create("Ping", []), (_, _) => { }, matchArguments: false);
        var multipleParameters = new GameEventScriptMessageHandlerDescriptor(GameEventScriptMessageSignature.Create("Ping", ["one", "two"]), (_, _) => { }, matchArguments: false);

        Assert.AreEqual("Ping(*)", noParameters.DispatchSignatureId);
        Assert.AreEqual("Ping(*)", multipleParameters.DispatchSignatureId);
        Assert.IsEmpty(noParameters.Signature.Parameters);
        CollectionAssert.AreEqual(new[] { "one", "two" }, multipleParameters.Signature.Parameters.ToArray());
    }

    [TestMethod]
    public void MessageSignatureAlwaysCreatesNormalMessages()
    {
        var signature = GameEventScriptMessageSignature.Create("Ping", ["amount"]);

        Assert.IsTrue(signature.TryCreateMessage([GameEventScriptBoxedValue.FromInteger(7)], out var message));
        Assert.AreEqual("Ping(amount)", message.SignatureId);
    }
}
