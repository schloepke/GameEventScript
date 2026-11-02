// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
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

    [TestMethod]
    public void EverySpellingPrecedesTheShorterSpellingsItStartsOrEndsWith()
    {
        var suffixes = GesQuantityUnits.Suffixes;
        for (var later = 0; later < suffixes.Length; later++)
            for (var earlier = 0; earlier < later; earlier++)
            {
                var (longer, shorter) = (suffixes[later].Text, suffixes[earlier].Text);
                Assert.IsFalse(longer.Length > shorter.Length && (longer.StartsWith(shorter, StringComparison.Ordinal) || longer.EndsWith(shorter, StringComparison.Ordinal)), $"{longer} after {shorter}");
            }
    }
}
