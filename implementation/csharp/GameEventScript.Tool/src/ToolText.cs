// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace GameEventScript.Tool;

internal static class ToolText
{
    // A display string in JSON quotes, as the Swift CLI writes it: only quotes, backslashes and control characters are
    // escaped, so Unicode text and paths stay readable.
    internal static string Quote(string text)
    {
        var result = new StringBuilder(text.Length + 2).Append('"');
        foreach (var character in text)
        {
            switch (character)
            {
                case '"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case < ' ': result.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); break;
                default: result.Append(character); break;
            }
        }

        return result.Append('"').ToString();
    }
}
