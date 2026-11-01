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
    // emoji variation selector or with an Emoji_Presentation scalar occupies two columns, any other the sum of its scalars.
    internal static int Width(ReadOnlySpan<char> text)
    {
        var total = 0;
        Span<char> scalar = stackalloc char[2];
        while (!text.IsEmpty)
        {
            var length = StringInfo.GetNextTextElementLength(text);
            var width = 0;
            var emoji = false;
            foreach (var rune in text[..length].EnumerateRunes())
            {
                width += Math.Max(0, UnicodeWidth.GetWidth(scalar[..rune.EncodeToUtf16(scalar)]));
                emoji |= rune.Value is 0x200D or 0xFE0F || IsEmojiPresentation(rune.Value);
            }
            total += emoji ? 2 : width;
            text = text[length..];
        }
        return total;
    }

    // Unicode Emoji_Presentation as Swift's Unicode.Scalar.Properties.isEmojiPresentation reports it, as inclusive
    // ranges; neither the terminal tables nor PrettyPrompt measure all of these scalars as wide.
    private static ReadOnlySpan<int> EmojiPresentation =>
    [
        0x231A, 0x231B, 0x23E9, 0x23EC, 0x23F0, 0x23F0, 0x23F3, 0x23F3, 0x25FD, 0x25FE, 0x2614, 0x2615, 0x2648, 0x2653, 0x267F, 0x267F,
        0x2693, 0x2693, 0x26A1, 0x26A1, 0x26AA, 0x26AB, 0x26BD, 0x26BE, 0x26C4, 0x26C5, 0x26CE, 0x26CE, 0x26D4, 0x26D4, 0x26EA, 0x26EA,
        0x26F2, 0x26F3, 0x26F5, 0x26F5, 0x26FA, 0x26FA, 0x26FD, 0x26FD, 0x2705, 0x2705, 0x270A, 0x270B, 0x2728, 0x2728, 0x274C, 0x274C,
        0x274E, 0x274E, 0x2753, 0x2755, 0x2757, 0x2757, 0x2795, 0x2797, 0x27B0, 0x27B0, 0x27BF, 0x27BF, 0x2B1B, 0x2B1C, 0x2B50, 0x2B50,
        0x2B55, 0x2B55, 0x1F004, 0x1F004, 0x1F0CF, 0x1F0CF, 0x1F18E, 0x1F18E, 0x1F191, 0x1F19A, 0x1F1E6, 0x1F1FF, 0x1F201, 0x1F201, 0x1F21A, 0x1F21A,
        0x1F22F, 0x1F22F, 0x1F232, 0x1F236, 0x1F238, 0x1F23A, 0x1F250, 0x1F251, 0x1F300, 0x1F320, 0x1F32D, 0x1F335, 0x1F337, 0x1F37C, 0x1F37E, 0x1F393,
        0x1F3A0, 0x1F3CA, 0x1F3CF, 0x1F3D3, 0x1F3E0, 0x1F3F0, 0x1F3F4, 0x1F3F4, 0x1F3F8, 0x1F43E, 0x1F440, 0x1F440, 0x1F442, 0x1F4FC, 0x1F4FF, 0x1F53D,
        0x1F54B, 0x1F54E, 0x1F550, 0x1F567, 0x1F57A, 0x1F57A, 0x1F595, 0x1F596, 0x1F5A4, 0x1F5A4, 0x1F5FB, 0x1F64F, 0x1F680, 0x1F6C5, 0x1F6CC, 0x1F6CC,
        0x1F6D0, 0x1F6D2, 0x1F6D5, 0x1F6D8, 0x1F6DC, 0x1F6DF, 0x1F6EB, 0x1F6EC, 0x1F6F4, 0x1F6FC, 0x1F7E0, 0x1F7EB, 0x1F7F0, 0x1F7F0, 0x1F90C, 0x1F93A,
        0x1F93C, 0x1F945, 0x1F947, 0x1F9FF, 0x1FA70, 0x1FA7C, 0x1FA80, 0x1FA8A, 0x1FA8E, 0x1FAC6, 0x1FAC8, 0x1FAC8, 0x1FACD, 0x1FADC, 0x1FADF, 0x1FAEA,
        0x1FAEF, 0x1FAF8,
    ];

    private static bool IsEmojiPresentation(int scalar)
    {
        if (scalar < 0x231A) return false;
        var ranges = EmojiPresentation;
        int low = 0, high = ranges.Length / 2 - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (scalar < ranges[2 * middle]) high = middle - 1;
            else if (scalar > ranges[2 * middle + 1]) low = middle + 1;
            else return true;
        }
        return false;
    }

    // The index after the grapheme cluster at index, so editing never splits a cluster.
    internal static int NextCluster(string text, int index) => index + StringInfo.GetNextTextElementLength(text.AsSpan(index));

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
