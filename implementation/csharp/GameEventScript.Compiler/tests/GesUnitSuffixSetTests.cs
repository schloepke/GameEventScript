// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Compiler;
using GameEventScript.Runtime;

namespace GameEventScript.Tests.Native.Compiler;

/// <summary>
/// Keeps the hand-written unit-suffix prechecks of the lexer and the text decoder in step with the suffix table.
/// </summary>
[TestClass]
public sealed class GesUnitSuffixSetTests
{
    [TestMethod]
    public void LexerPrecheckAcceptsTheFirstCharacterOfEverySuffix()
    {
        foreach (var suffix in GesQuantityUnits.Suffixes)
            Assert.IsTrue(GesLexer.MayStartUnitSuffix(suffix.Text[0]), suffix.Text);
    }

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
