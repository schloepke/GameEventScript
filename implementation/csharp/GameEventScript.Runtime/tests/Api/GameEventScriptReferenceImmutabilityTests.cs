// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Api;

[TestClass]
public sealed class GameEventScriptReferenceImmutabilityTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExtensionLabelsRemainConsistentWithTheirSignature(bool enumerableOnly)
    {
        var input = new[] { "first", "second" };
        var reference = new GameEventScriptExtensionReference("test", "combine", enumerableOnly ? input.Select(label => label) : input);
        input[0] = "changed";

        CollectionMutationProbe.ReplaceFirstIfWritable(reference.ArgumentLabels, "changed");

        CollectionAssert.AreEqual(new[] { "first", "second" }, reference.ArgumentLabels.ToArray());
        Assert.AreEqual("test.combine(first,second)", reference.SignatureId);
        Assert.AreEqual(reference.SignatureId, new GameEventScriptExtensionReference(reference.ExtensionName, reference.FunctionName, reference.ArgumentLabels).SignatureId);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConstructorLabelsRemainConsistentWithTheirSignature(bool enumerableOnly)
    {
        var input = new[] { "first", "second" };
        var reference = new GameEventScriptExternalTypeConstructorReference("Sample", enumerableOnly ? input.Select(label => label) : input);
        input[0] = "changed";

        CollectionMutationProbe.ReplaceFirstIfWritable(reference.ArgumentLabels, "changed");

        CollectionAssert.AreEqual(new[] { "first", "second" }, reference.ArgumentLabels.ToArray());
        Assert.AreEqual("Sample(first,second)", reference.SignatureId);
        Assert.AreEqual(reference.SignatureId, GameEventScriptExternalTypeConstructorReference.CreateSignatureId(reference.TypeName, reference.ArgumentLabels));
    }
}
