// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace GameEventScript.Tool;

internal static class ToolTextDisplay
{
    internal const int TabSize = 4;

    // Expand plain text before applying ANSI colors. Each segment begins at a
    // tab stop or a new line; only its visible width determines the next stop.
    internal static string ExpandTabs(string text)
    {
        if (!text.Contains('\t')) return text;
        var result = new StringBuilder(text.Length);
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\t')
            {
                result.Append(text, start, index - start);
                var width = Width(text.AsSpan(start, index - start));
                result.Append(' ', TabSize - width % TabSize);
                start = index + 1;
            }
            else if (text[index] is '\r' or '\n')
            {
                result.Append(text, start, index - start + 1);
                start = index + 1;
            }
        }
        return result.Append(text, start, text.Length - start).ToString();
    }

    // The terminal width of text, measured per grapheme cluster like the Swift CLI: a cluster joined by ZWJ, with an
    // emoji variation selector or with a wide scalar occupies two columns, any other the sum of its scalars.
    internal static int Width(ReadOnlySpan<char> text)
    {
        var total = 0;
        while (!text.IsEmpty)
        {
            var length = StringInfo.GetNextTextElementLength(text);
            var width = 0;
            var wide = false;
            foreach (var rune in text[..length].EnumerateRunes())
            {
                var scalarWidth = ScalarWidth(rune.Value);
                width += scalarWidth;
                wide |= scalarWidth == 2 || rune.Value is 0x200D or 0xFE0F;
            }
            total += wide ? 2 : width;
            text = text[length..];
        }
        return total;
    }

    // The columns of one scalar from the generated table both CLIs share.
    internal static int ScalarWidth(int scalar)
    {
        if (scalar is >= 0x20 and < 0x7F) return 1;
        var starts = ToolTextWidths.Starts;
        int low = 0, high = starts.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (starts[middle] <= scalar) low = middle;
            else high = middle - 1;
        }
        return ToolTextWidths.Widths[low];
    }

    // The index after the grapheme cluster at index, so editing never splits a cluster.
    internal static int NextCluster(string text, int index) => index + StringInfo.GetNextTextElementLength(text.AsSpan(index));

    // The number of grapheme clusters in text.
    internal static int ClusterCount(ReadOnlySpan<char> text)
    {
        var count = 0;
        for (; !text.IsEmpty; count++) text = text[StringInfo.GetNextTextElementLength(text)..];
        return count;
    }

    // The index after the first count grapheme clusters of text.
    internal static int ClusterIndex(string text, int count)
    {
        var index = 0;
        for (; count > 0 && index < text.Length; count--) index = NextCluster(text, index);
        return index;
    }

    // The start of the grapheme cluster that ends at or contains index - 1.
    internal static int PreviousCluster(string text, int index)
    {
        var start = 0;
        while (start < index)
        {
            var next = NextCluster(text, start);
            if (next >= index) return start;
            start = next;
        }
        return 0;
    }

    // The display width of one line after ExpandTabs, without building the expanded text.
    internal static int Columns(ReadOnlySpan<char> line)
    {
        var column = 0;
        var start = 0;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] != '\t') continue;
            column += Width(line[start..index]);
            column += TabSize - column % TabSize;
            start = index + 1;
        }
        return column + Width(line[start..]);
    }
}
