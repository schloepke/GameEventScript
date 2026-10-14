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
        var signature = GesMessageSignature.Create("Move", ["target", "unit"]);
        var message = GesBridgeMessage.Create(
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

    [TestMethod]
    public void PublishSinkDelegateReceivesOutboundMessagesAndReportsAcceptance()
    {
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishSink(new GesBridgePublishSink(message =>
            {
                published.Add(message.SignatureId);
                return message.Name == "Accepted";
            }))
            .Build();
        var accepted = default(GesPublishResult);
        var rejected = default(GesPublishResult);
        host.SubscribeMessageName("Start", (_, context) =>
        {
            accepted = context.Publish(GameEventScriptMessage.Create("Accepted"));
            rejected = context.Publish(GameEventScriptMessage.Create("Rejected"));
        });
        Assert.AreEqual(GesStartState.Ready, host.Start().State);

        host.Receive(GameEventScriptMessage.Create("Start"));
        Assert.AreEqual(GesExecutionState.Completed, host.RunToCompletion().State);

        CollectionAssert.AreEqual(new[] { "Accepted()", "Rejected()" }, published);
        Assert.IsTrue(accepted.OutboundAccepted);
        Assert.IsFalse(rejected.OutboundAccepted);
        Assert.ThrowsExactly<ArgumentNullException>(() => new GesBridgePublishSink(null!));
    }
}
