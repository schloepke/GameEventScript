// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime;

namespace GameEventScript.Tests.Native.Runtime;

/// <summary>
/// Keeps the hand-written unit-suffix precheck of the text decoder in step with the suffix table.
/// </summary>
[TestClass]
public sealed class GesTextNumberCastSuffixTests
{
    [TestMethod]
    public void TextDecoderPrecheckAcceptsTheEndOfEverySuffix()
    {
        foreach (var suffix in GesQuantityUnits.Suffixes)
        {
            var text = "1" + suffix.Text;
            Assert.IsTrue(GesTextNumberCast.MayEndWithSuffix(text, 0, text.Length), suffix.Text);
        }
    }
}
