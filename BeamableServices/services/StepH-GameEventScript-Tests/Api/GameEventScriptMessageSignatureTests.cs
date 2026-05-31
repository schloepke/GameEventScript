using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptMessageSignatureTests
{
    [TestMethod]
    public void WildcardSignatureHasDistinctSignatureIdAndMatchesByMessageName()
    {
        var exact = GameEventScriptMessageSignature.Create("Ping", ["envelope"]);
        var wildcard = GameEventScriptMessageSignature.Create("Ping", ["ignored"], matchArguments: false);
        var message = GameEventScriptMessage.Create("Ping", ("amount", GameEventScriptNumberValue.CreateInteger(7)));

        Assert.AreEqual("Ping(envelope)", exact.SignatureId);
        Assert.AreEqual("Ping(*)", wildcard.SignatureId);
        Assert.IsEmpty(wildcard.Parameters);
        Assert.IsFalse(exact.Matches(message));
        Assert.IsTrue(wildcard.Matches(message));
    }

    [TestMethod]
    public void WildcardSignatureDoesNotUseParametersForMatching()
    {
        var noParameters = GameEventScriptMessageSignature.Create("Ping", [], matchArguments: false);
        var multipleParameters = GameEventScriptMessageSignature.Create("Ping", ["one", "two"], matchArguments: false);

        Assert.AreEqual("Ping(*)", noParameters.SignatureId);
        Assert.AreEqual("Ping(*)", multipleParameters.SignatureId);
        Assert.IsEmpty(noParameters.Parameters);
        Assert.IsEmpty(multipleParameters.Parameters);
    }

    [TestMethod]
    public void WildcardSignatureCannotCreateNormalMessage()
    {
        var wildcard = GameEventScriptMessageSignature.Create("Ping", [], matchArguments: false);

        Assert.IsFalse(wildcard.TryCreateMessage([GameEventScriptNumberValue.CreateInteger(7)], out _));
    }
}
