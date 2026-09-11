// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace GameEventScript.Runtime;

internal static class TextLiteralReader
{
    internal static string? Read(string text, ref int position)
    {
        if (position >= text.Length || text[position] is not ('\'' or '"')) return null;
        var quote = text[position++];
        var start = position;
        StringBuilder? builder = null;
        while (position < text.Length)
        {
            if (text[position++] != quote) continue;
            if (position < text.Length && text[position] == quote)
            {
                builder ??= new StringBuilder();
                builder.Append(text, start, position - start);
                position++;
                start = position;
                continue;
            }
            if (builder is null) return text.Substring(start, position - start - 1);
            builder.Append(text, start, position - start - 1);
            return builder.ToString();
        }
        return null;
    }

    internal static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"') builder.Append('"');
            builder.Append(value[i]);
        }
        builder.Append('"');
    }
}
