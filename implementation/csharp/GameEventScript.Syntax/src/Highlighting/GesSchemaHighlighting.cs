// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
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
        "within", "integer", "optional", "key", "max", "depth", "tags", "wire", "model", "let",
        "default", "true", "false", "abstract", "packed", "with", "all", "without", "computed", "by", "when", "otherwise", "or", "not", "of",
        "min", "clamp", "abs", "has", "sqrt", "cbrt", "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "hypot", "exp", "ln",
        "floor", "ceil", "truncate", "round", "rad", "deg"
    };

    internal static void Highlight(string text, GesSpanBuilder spans, List<GesHighlightDiagnostic> diagnostics)
    {
        var position = 0;
        var previous = "";
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
            else if (c == '`' && QuotedEnd(text, position) is var quoted and > 0)
            {
                // A name in backticks is a name, also where its word is a keyword.
                position = quoted;
                kind = GesSyntaxKind.Identifier;
            }
            else if (GesText.IsAsciiLetter(c))
            {
                position = WordEnd(text, position);
                if (GesText.IsAsciiUpper(c)) position = GesText.UpperNameEnd(text, position);
                var word = text.Substring(start, position - start);

                // The words of `round half even`, `round half up` and `round half down` are keywords only there.
                var mode = previous == "round" && word == "half" || previous == "half" && word is "even" or "up" or "down";
                kind = Keywords.Contains(word) || mode ? GesSyntaxKind.Keyword : char.IsUpper(c) ? GesSyntaxKind.Message : GesSyntaxKind.Identifier;
                previous = word;
                spans.Add(start, position - start, kind);
                continue;
            }
            else
            {
                position += char.IsHighSurrogate(c) && position + 1 < text.Length ? 2 : 1;
                kind = GesSyntaxKind.Plain;
            }

            if (kind != GesSyntaxKind.Plain || c is not (' ' or '\t' or '\r' or '\n')) previous = "";
            spans.Add(start, position - start, kind);
        }

        if (GesSchemaSourceReader.Read(text).Error is { } error)
        {
            // The scalar at the error, or a position at the end of the text, as Swift measures it.
            var length = error.Offset >= text.Length ? 0 : char.IsHighSurrogate(text[error.Offset]) && error.Offset + 1 < text.Length ? 2 : 1;
            diagnostics.Add(new GesHighlightDiagnostic(error.Code, error.Message, Math.Min(error.Offset, text.Length), length));
        }
    }

    // The end of a name in backticks on one line, or 0 where the backtick closes none.
    private static int QuotedEnd(string text, int position)
    {
        var end = position + 1;
        while (end < text.Length && GesText.IsAsciiLetter(text[end]) || end < text.Length && text[end] is >= '0' and <= '9') end++;
        return end > position + 1 && end < text.Length && text[end] == '`' ? end + 1 : 0;
    }

    private static int WordEnd(string text, int position)
    {
        while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9')) position++;
        return position;
    }

    private static bool IsNumberEnd(string text, int index)
        => text[index] is ' ' or '\t' or '\r' or '\n' or ',' or '(' or ')' or '{' or '}' or '[' or ']' or '|' or '#' || (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/');
}
