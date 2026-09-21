// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
                var width = Math.Max(0, UnicodeWidth.GetWidth(text.AsSpan(start, index - start)));
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
}
