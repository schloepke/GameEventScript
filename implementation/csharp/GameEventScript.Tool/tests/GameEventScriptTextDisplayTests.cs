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
    [DataRow("\u231A", 2)]
    [DataRow("\u2764\uFE0F", 2)]
    [DataRow("\uD83C\uDDE6", 2)]
    [DataRow("\uD83D\uDC4D\uD83C\uDFFD", 2)]
    [DataRow("\uD83C\uDDE9\uD83C\uDDEA", 2)]
    [DataRow("\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67", 2)]
    [DataRow("\uD835\uDC00", 1)]
    [DataRow("\u65E5\uDB40\uDD00", 2)]
    [DataRow("\uD880\uDC00", 2)]
    [DataRow("\u200B", 0)]
    public void ClusterWidth(string text, int width) => Assert.AreEqual(width, ToolTextDisplay.Width(text));

    /// <summary>Verifies that line columns expand tabs to the next stop after wide clusters.</summary>
    [TestMethod]
    public void ColumnsExpandTabs()
    {
        Assert.AreEqual(5, ToolTextDisplay.Columns("a\tb"));
        Assert.AreEqual(9, ToolTextDisplay.Columns("\u65E5\u2764\uFE0F\tb"));
        Assert.AreEqual("\u65E5\u2764\uFE0F    b", ToolTextDisplay.ExpandTabs("\u65E5\u2764\uFE0F\tb"));
    }

    /// <summary>Verifies that cursor movement never splits a grapheme cluster.</summary>
    [TestMethod]
    public void ClusterNavigation()
    {
        const string text = "a\u2764\uFE0F\uD83D\uDC68\u200D\uD83D\uDC69b";
        Assert.AreEqual(3, ToolTextDisplay.NextCluster(text, 1));
        Assert.AreEqual(8, ToolTextDisplay.NextCluster(text, 3));
        Assert.AreEqual(3, ToolTextDisplay.PreviousCluster(text, 8));
        Assert.AreEqual(1, ToolTextDisplay.PreviousCluster(text, 3));
        Assert.AreEqual(1, ToolTextDisplay.PreviousCluster(text, 2));
        Assert.AreEqual(0, ToolTextDisplay.PreviousCluster(text, 1));
        Assert.AreEqual(4, ToolTextDisplay.ClusterCount(text));
        Assert.AreEqual(8, ToolTextDisplay.ClusterIndex(text, 3));
        Assert.AreEqual(text.Length, ToolTextDisplay.ClusterIndex(text, 9));
    }

    /// <summary>Verifies the generated width table's shape: ascending runs from U+0000 with one width each.</summary>
    [TestMethod]
    public void GeneratedWidthTable()
    {
        var starts = ToolTextWidths.Starts;
        Assert.AreEqual(starts.Length, ToolTextWidths.Widths.Length);
        Assert.AreEqual(0, starts[0]);
        for (var index = 1; index < starts.Length; index++) Assert.IsLessThan(starts[index], starts[index - 1]);
        foreach (var width in ToolTextWidths.Widths) Assert.IsLessThanOrEqualTo(2, width);
    }

    /// <summary>Verifies waiting-input positions: tab spaces wrap individually, wide clusters move whole.</summary>
    [TestMethod]
    public void PromptPositions()
    {
        Assert.AreEqual(((1, 4), (1, 3)), RunWaitingInput.Position("abcd\tx", 5, 10));
        Assert.AreEqual(((1, 2), (0, 9)), RunWaitingInput.Position("abcd\u65E5", 4, 10));
        Assert.AreEqual(((1, 6), (0, 5)), RunWaitingInput.Position("a\nb", 0, 10));
    }
}
