// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
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
            var length = ClusterLength(text);
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

    // The length of the grapheme cluster at the start of text. .NET's text elements lack the Indic conjunct rule GB9c,
    // so a cluster ending in consonant, linker and marks joins a following consonant, like a Swift Character.
    internal static int ClusterLength(ReadOnlySpan<char> text)
    {
        var length = StringInfo.GetNextTextElementLength(text);
        // The cheap consonant test on the next scalar comes first, so other scripts skip the backward scan.
        while (length < text.Length
            && Rune.DecodeFromUtf16(text[length..], out var next, out _) == OperationStatus.Done
            && IsConjunctConsonant(next.Value)
            && EndsInConjunctLinker(text[..length]))
            length += StringInfo.GetNextTextElementLength(text[length..]);
        return length;
    }

    // The range guard keeps the table search out of text in other scripts.
    private static bool IsConjunctConsonant(int scalar)
    {
        var consonants = ToolTextWidths.ConjunctConsonants;
        return scalar >= consonants[0] && scalar <= consonants[^1] && consonants.BinarySearch(scalar) >= 0;
    }

    // Consonant [Extend|Linker]* Linker [Extend|Linker]*, with zero-width scalars standing in for InCB=Extend.
    // ZWNJ is zero-width but not InCB=Extend: it deliberately prevents the conjunct.
    private static bool EndsInConjunctLinker(ReadOnlySpan<char> cluster)
    {
        var linked = false;
        while (Rune.DecodeLastFromUtf16(cluster, out var rune, out var size) == OperationStatus.Done)
        {
            if (rune.Value == 0x200C) return false;
            if (ToolTextWidths.ConjunctLinkers.BinarySearch(rune.Value) >= 0) linked = true;
            else if (ScalarWidth(rune.Value) != 0) return linked && IsConjunctConsonant(rune.Value);
            cluster = cluster[..^size];
        }
        return false;
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
    internal static int NextCluster(string text, int index) => index + ClusterLength(text.AsSpan(index));

    // The number of grapheme clusters in text.
    internal static int ClusterCount(ReadOnlySpan<char> text)
    {
        var count = 0;
        for (; !text.IsEmpty; count++) text = text[ClusterLength(text)..];
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
