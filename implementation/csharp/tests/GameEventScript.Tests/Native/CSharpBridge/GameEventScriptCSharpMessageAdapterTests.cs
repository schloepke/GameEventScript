// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.CSharpBridge;

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
