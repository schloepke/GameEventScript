// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Native.Api;

[TestClass]
public sealed class GameEventScriptMessageImmutabilityTests
{
    [TestMethod]
    public void PublicTagsCannotChangeAnAlreadyQueuedDelivery()
    {
        var host = GameEventScriptHost.CreateBuilder().Build();
        var delivered = new List<GameEventScriptMessage>();
        host.Subscribe(GameEventScriptMessageSignature.Create("Tagged", []), (message, _) => delivered.Add(message), ["green"]);
        var input = GameEventScriptMessage.Create("Tagged", [], ["green"]);
        Assert.IsTrue(host.Receive(input));

        MutateFirstItemIfWritable(input.Tags, "red");
        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.Completed, result.State);
        Assert.HasCount(1, delivered, "Mutating a public view must not change the tag match captured for an accepted message.");
        CollectionAssert.AreEqual(new[] { "green" }, input.Tags.ToArray());
        CollectionAssert.AreEqual(new[] { "green" }, delivered[0].Tags.ToArray());
    }

    [TestMethod]
    public void PublicParametersCannotChangeExistingOrFutureMessages()
    {
        var signature = GameEventScriptMessageSignature.Create("Shape", ["original"]);
        var existing = signature.WithArguments(GesValue.GesInteger(7));

        MutateFirstItemIfWritable(signature.Parameters, "changed");
        var following = signature.WithArguments(GesValue.GesInteger(8));

        CollectionAssert.AreEqual(new[] { "original" }, signature.Parameters.ToArray());
        Assert.AreEqual("Shape(original)", signature.SignatureId);
        AssertMessageShape(existing, 7);
        AssertMessageShape(following, 8);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicArgumentLabelsCannotChangeMessageOrSharedSignature(bool bindThroughSignature)
    {
        var signature = GameEventScriptMessageSignature.Create("Shape", ["original"]);
        var message = bindThroughSignature
            ? signature.WithArguments(GesValue.GesInteger(7))
            : GameEventScriptMessage.Create("Shape", [new GameEventScriptMessageArgument("original", GesValue.GesInteger(7))]);

        MutateFirstItemIfWritable(message.Arguments.SignatureLabels, "changed");

        AssertMessageShape(message, 7);
        CollectionAssert.AreEqual(new[] { "original" }, signature.Parameters.ToArray());
        AssertMessageShape(signature.WithArguments(GesValue.GesInteger(8)), 8);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SignatureMessageFactoriesCopyCallerOwnedValues(bool useWithArguments)
    {
        var signature = GameEventScriptMessageSignature.Create("Shape", ["original"]);
        var values = new[] { GesValue.GesInteger(7) };
        var message = useWithArguments ? signature.WithArguments(values) : signature.CreateMessage((IReadOnlyList<GesValue>)values)!;
        var hash = message.GetHashCode();

        values[0] = GesValue.GesInteger(99);

        AssertMessageShape(message, 7);
        Assert.AreEqual(hash, message.GetHashCode());
        Assert.AreEqual(signature.WithArguments(GesValue.GesInteger(7)), message);
    }

    [TestMethod]
    public void MergedTagsCannotChangeOriginalOrCopiedMessages()
    {
        var originalTags = new[] { "green" };
        var addedTags = new[] { "blue", "green" };
        var original = GameEventScriptMessage.Create("Tagged", [], originalTags);
        var merged = original.WithTags(addedTags);
        var unchanged = merged.WithTags("green");
        var hash = merged.GetHashCode();

        originalTags[0] = "red";
        addedTags[0] = "red";
        MutateFirstItemIfWritable(original.Tags, "red");
        MutateFirstItemIfWritable(merged.Tags, "red");
        MutateFirstItemIfWritable(unchanged.Tags, "red");

        CollectionAssert.AreEqual(new[] { "green" }, original.Tags.ToArray());
        CollectionAssert.AreEqual(new[] { "green", "blue" }, merged.Tags.ToArray());
        CollectionAssert.AreEqual(new[] { "green", "blue" }, unchanged.Tags.ToArray());
        Assert.AreEqual(hash, merged.GetHashCode());
        Assert.AreEqual(GameEventScriptMessage.Create("Tagged", [], ["green", "blue"]), merged);
    }

    [TestMethod]
    [DataRow("emit Shape(original: 7) with #green")]
    [DataRow("let message be Shape(original: 7)\n emit message with #green")]
    [DataRow("let handler be Shape(original)\n let message be handler(original: 7)\n emit message with #green")]
    public void ScriptMessageViewsCannotChangeCurrentOrSubsequentDeliveries(string body)
    {
        var program = GameEventScriptBuilder.Create().AddScript("module immutablemessages\non Start { " + body + "\n }").Compile();
        var host = GameEventScriptHost.CreateBuilder().Build();
        var delivered = new List<GameEventScriptMessage>();
        host.Subscribe(GameEventScriptMessageSignature.Create("Shape", ["original"]), (message, _) =>
        {
            MutateFirstItemIfWritable(message.Arguments.SignatureLabels, "changed");
            MutateFirstItemIfWritable(message.Tags, "red");
            delivered.Add(message);
        }, ["green"]);
        host.Load(program);

        for (var iteration = 0; iteration < 2; iteration++)
        {
            Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Start")));
            Assert.AreEqual(GameEventScriptExecutionState.Completed, host.RunToCompletion().State);
        }

        Assert.HasCount(2, delivered);
        foreach (var message in delivered)
        {
            AssertMessageShape(message, 7);
            CollectionAssert.AreEqual(new[] { "green" }, message.Tags.ToArray());
        }
    }

    private static void AssertMessageShape(GameEventScriptMessage message, long value)
    {
        Assert.AreEqual("Shape(original)", message.SignatureId);
        CollectionAssert.AreEqual(new[] { "original" }, message.Arguments.SignatureLabels.ToArray());
        Assert.AreEqual("original", message.Arguments.NameAt(0));
        Assert.AreEqual(0, message.Arguments.IndexOf("original"));
        Assert.AreEqual(-1, message.Arguments.IndexOf("changed"));
        Assert.AreEqual(value, message.Arguments.GetAsInteger("original"));
    }

    private static void MutateFirstItemIfWritable(IReadOnlyList<string> view, string replacement)
    {
        // Arrays implement IList<T> even though they are exposed through IReadOnlyList<T>.
        // A truly immutable view may omit the mutable interfaces or reject the setter.
        try
        {
            if (view is IList<string> generic) generic[0] = replacement;
        }
        catch (NotSupportedException) { }
        try
        {
            if (view is IList nongeneric) nongeneric[0] = replacement;
        }
        catch (NotSupportedException) { }
        try
        {
            if (view is ICollection { SyncRoot: IList storage }) storage[0] = replacement;
        }
        catch (NotSupportedException) { }
    }
}
