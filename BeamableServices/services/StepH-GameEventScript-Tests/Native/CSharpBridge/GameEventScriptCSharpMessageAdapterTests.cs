using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Native.CSharpBridge;

[TestClass]
public sealed class GameEventScriptCSharpMessageAdapterTests
{
    [TestMethod]
    public void CSharpDictionaryAdapterBindsValuesInKnownSignatureOrder()
    {
        var signature = GameEventScriptMessageSignature.Create("Move", ["target", "unit"]);
        var message = GameEventScriptCSharpMessage.Create(
            signature,
            new Dictionary<string, GesValue>
            {
                ["unit"] = GesValue.GesText("u1"),
                ["target"] = GesValue.GesText("t1")
            });

        Assert.AreEqual("Move(target,unit)", message.SignatureId);
        Assert.AreEqual("t1", message.Arguments.GetAsText(0));
        Assert.AreEqual("u1", message.Arguments.GetAsText(1));
    }
}
