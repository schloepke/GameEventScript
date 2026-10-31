// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Compiler;
using GameEventScript.Syntax;

namespace GameEventScript.Tests.Native.Compiler;

/// <summary>
/// Keeps the hand-written unit-suffix precheck of the lexer in step with the suffix table.
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
}
