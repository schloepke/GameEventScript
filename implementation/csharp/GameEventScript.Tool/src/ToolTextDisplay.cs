// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using PrettyPrompt.Rendering;

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
    // emoji variation selector, or of several scalars including a wide one occupies two columns.
    internal static int Width(ReadOnlySpan<char> text)
    {
        var total = 0;
        while (!text.IsEmpty)
        {
            var length = StringInfo.GetNextTextElementLength(text);
            var width = 0;
            var runes = 0;
            var joined = false;
            var hasWide = false;
            foreach (var rune in text[..length].EnumerateRunes())
            {
                var runeWidth = Math.Max(0, UnicodeWidth.GetWidth(rune.ToString()));
                width += runeWidth;
                joined |= rune.Value is 0x200D or 0xFE0F;
                hasWide |= runeWidth == 2;
                runes++;
            }
            total += joined || runes > 1 && hasWide ? 2 : width;
            text = text[length..];
        }
        return total;
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
