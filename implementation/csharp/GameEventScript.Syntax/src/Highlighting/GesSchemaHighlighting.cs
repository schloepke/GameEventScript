// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Code = GameEventScript.Syntax.GesSchemaDiagnostic;

namespace GameEventScript.Syntax;

// Categorizes schema source token by token, also after an error: keywords, type names, tags, constants, numbers and
// comments. The diagnostic is the reader's.
internal static class GesSchemaHighlighting
{
    private static readonly HashSet<string> Keywords = new(System.StringComparer.Ordinal)
    {
        "schema", "message", "messages", "constant", "parameter", "as", "be", "between", "and", "wrapping", "step", "delta",
        "within", "integer", "optional", "key", "max", "tags"
    };

    internal static void Highlight(string text, GesSpanBuilder spans, List<GesHighlightDiagnostic> diagnostics)
    {
        var position = 0;
        if (text.Length > 0 && text[0] == '﻿') spans.Add(position++, 1, GesSyntaxKind.Plain);
        while (position < text.Length)
        {
            var start = position;
            var c = text[position];
            GesSyntaxKind kind;
            if (c == '/' && position + 1 < text.Length && text[position + 1] == '/')
            {
                while (position < text.Length && text[position] is not ('\r' or '\n')) position++;
                kind = GesSyntaxKind.Comment;
            }
            else if (c == ':' && position + 1 < text.Length && GesText.IsAsciiUpper(text[position + 1]))
            {
                position = GesText.UpperNameEnd(text, WordEnd(text, position + 1));
                kind = GesSyntaxKind.Type;
            }
            else if (c == '#' && position + 1 < text.Length && GesText.IsAsciiLetter(text[position + 1]))
            {
                position = WordEnd(text, position + 1);
                kind = GesSyntaxKind.Tag;
            }
            else if (c == '$' && position + 1 < text.Length && GesText.IsAsciiLetter(text[position + 1]))
            {
                position = WordEnd(text, position + 1);
                kind = GesSyntaxKind.Constant;
            }
            else if (c is >= '0' and <= '9' || (c == '-' && position + 1 < text.Length && text[position + 1] is >= '0' and <= '9'))
            {
                position++;
                while (position < text.Length && !IsNumberEnd(text, position)) position++;
                kind = GesSyntaxKind.Number;
            }
            else if (GesText.IsAsciiLetter(c))
            {
                position = WordEnd(text, position);
                if (GesText.IsAsciiUpper(c)) position = GesText.UpperNameEnd(text, position);
                var word = text.Substring(start, position - start);
                kind = Keywords.Contains(word) ? GesSyntaxKind.Keyword : char.IsUpper(c) ? GesSyntaxKind.Message : GesSyntaxKind.Identifier;
            }
            else
            {
                position += char.IsHighSurrogate(c) && position + 1 < text.Length ? 2 : 1;
                kind = GesSyntaxKind.Plain;
            }

            spans.Add(start, position - start, kind);
        }

        if (GesSchemaSourceReader.Read(text).Error is { } error)
            diagnostics.Add(new GesHighlightDiagnostic(error.Code, error.Message, error.Offset, 1));
    }

    private static int WordEnd(string text, int position)
    {
        while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9')) position++;
        return position;
    }

    private static bool IsNumberEnd(string text, int index)
        => text[index] is ' ' or '\t' or '\r' or '\n' or ',' or '(' or ')' or '{' or '}' or '|' or '#' || (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/');
}
