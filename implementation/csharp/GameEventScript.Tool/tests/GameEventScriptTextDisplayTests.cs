// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies the CLI's terminal widths, which match the Swift CLI per grapheme cluster.</summary>
[TestClass]
public sealed class GameEventScriptTextDisplayTests
{
    /// <summary>Verifies the width of one grapheme cluster.</summary>
    /// <param name="text">The cluster.</param>
    /// <param name="width">The expected terminal columns.</param>
    [TestMethod]
    [DataRow("a", 1)]
    [DataRow("e\u0301", 1)]
    [DataRow("\u65E5", 2)]
    [DataRow("\u2764\uFE0F", 2)]
    [DataRow("\uD83D\uDC4D\uD83C\uDFFD", 2)]
    [DataRow("\uD83C\uDDE9\uD83C\uDDEA", 2)]
    [DataRow("\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67", 2)]
    public void ClusterWidth(string text, int width) => Assert.AreEqual(width, ToolTextDisplay.Width(text));

    /// <summary>Verifies that line columns expand tabs to the next stop after wide clusters.</summary>
    [TestMethod]
    public void ColumnsExpandTabs()
    {
        Assert.AreEqual(5, ToolTextDisplay.Columns("a\tb"));
        Assert.AreEqual(9, ToolTextDisplay.Columns("\u65E5\u2764\uFE0F\tb"));
        Assert.AreEqual("\u65E5\u2764\uFE0F    b", ToolTextDisplay.ExpandTabs("\u65E5\u2764\uFE0F\tb"));
    }
}
