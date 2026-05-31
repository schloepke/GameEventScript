using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptMessageSignatureTests
{
    [TestMethod]
    public void HandlerDescriptorCanMatchByMessageName()
    {
        var exact = GameEventScriptMessageSignature.Create("Ping", ["message"]);
        var wildcard = new GameEventScriptMessageHandlerDescriptor(
            GameEventScriptMessageSignature.Create("Ping", ["ignored"]),
            (_, _) => { },
            matchArguments: false);
        var message = GameEventScriptMessage.Create("Ping", ("amount", GameEventScriptNumberValue.CreateInteger(7)));

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

        Assert.IsTrue(signature.TryCreateMessage([GameEventScriptNumberValue.CreateInteger(7)], out var message));
        Assert.AreEqual("Ping(amount)", message.SignatureId);
    }
}
