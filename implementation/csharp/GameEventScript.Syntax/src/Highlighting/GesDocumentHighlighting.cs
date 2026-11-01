// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Code = GameEventScript.Syntax.GesDocumentDiagnostic;

namespace GameEventScript.Syntax;

// Categorizes GES document text token by token, also after an error: keys and labels before their colon, the schema
// header, typed forms with their message and parameter names, scalars and comments. The diagnostic is the reader's.
internal static class GesDocumentHighlighting
{
    private enum Context : byte
    {
        Container,
        Form,
        MessageForm,
        HandlerForm,
        SeriesForm,
        Message,
        Handler
    }

    internal static void Highlight(string text, GesSpanBuilder spans, List<GesHighlightDiagnostic> diagnostics)
    {
        var contexts = new List<Context>();
        // The form a `:Type` names until its `(` opens, and whether the next token is the first inside a context.
        Context? pending = null;
        var first = false;
        var position = 0;
        if (text.Length > 0 && text[0] == '\uFEFF') spans.Add(position++, 1, GesSyntaxKind.Plain);
        while (position < text.Length)
        {
            var c = text[position];
            var start = position;
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                while (position < text.Length && text[position] is ' ' or '\t' or '\r' or '\n') position++;
                spans.Add(start, position - start, GesSyntaxKind.Plain);
                continue;
            }
            if (IsComment(text, position))
            {
                while (position < text.Length && text[position] is not ('\r' or '\n')) position++;
                spans.Add(start, position - start, GesSyntaxKind.Comment);
                continue;
            }

            var opening = pending;
            var firstInContext = first;
            pending = null;
            first = false;
            var kind = GesSyntaxKind.Plain;
            if (c is '\'' or '"')
            {
                position = QuotedEnd(text, position);
                kind = GesSyntaxKind.String;
            }
            else if (c == ':' && position + 1 < text.Length && GesText.IsAsciiLetter(text[position + 1]))
            {
                position = NameEnd(text, position + 1);
                var name = text.Substring(start + 1, position - start - 1);
                kind = name == "Schema" ? GesSyntaxKind.Keyword : GesSyntaxKind.Type;
                pending = name switch
                {
                    "Message" => Context.MessageForm,
                    "Handler" => Context.HandlerForm,
                    "Series" => Context.SeriesForm,
                    _ => Context.Form
                };
            }
            else if (c is '[' or '(')
            {
                position++;
                kind = GesSyntaxKind.Symbol;
                contexts.Add(c == '[' ? Context.Container : opening ?? Context.Container);
                first = true;
            }
            else if (c is ']' or ')')
            {
                position++;
                kind = GesSyntaxKind.Symbol;
                if (contexts.Count > 0) contexts.RemoveAt(contexts.Count - 1);
            }
            else if (c is ',' or ':')
            {
                position++;
                kind = GesSyntaxKind.Symbol;
            }
            else if (c == '#')
            {
                position = NameEnd(text, position + 1);
                kind = GesSyntaxKind.Tag;
            }
            else if (StartsNumber(text, position))
            {
                position = ValueEnd(text, position);
                kind = GesSyntaxKind.Number;
            }
            else if (GesText.IsAsciiLetter(c) || c == '_')
            {
                position = NameEnd(text, position);
                var word = text.Substring(start, position - start);
                var context = contexts.Count > 0 ? contexts[^1] : Context.Container;
                if (context is Context.MessageForm or Context.HandlerForm && firstInContext && (GesText.IsAsciiUpper(c) || GesText.IsSystemEndpointName(word)))
                {
                    kind = GesSyntaxKind.Message;
                    pending = context == Context.HandlerForm ? Context.Handler : Context.Message;
                }
                else if (FollowedByColon(text, position) || context == Context.Handler) kind = GesSyntaxKind.Identifier;
                else if (word is "true" or "false" or "nothing") kind = GesSyntaxKind.Keyword;
                else if (word == "with" && context == Context.MessageForm) kind = GesSyntaxKind.Keyword;
                else if (word is "fibonacci" or "factorial" && context == Context.SeriesForm && firstInContext) kind = GesSyntaxKind.Builtin;
                else if (word is "Infinity" or "NaN") kind = GesSyntaxKind.Number;
            }
            else position += GesText.ScalarUtf16LengthAt(text, position);
            spans.Add(start, position - start, kind);
        }

        var syntax = GesDocumentReader.Read(text);
        if (syntax.Diagnostic is { } diagnostic)
            diagnostics.Add(new GesHighlightDiagnostic(diagnostic.Code, Code.Explain(diagnostic.Code, diagnostic.LimitName, diagnostic.LimitValue), diagnostic.Start, diagnostic.End - diagnostic.Start));
    }

    private static bool IsComment(string text, int position) => position + 1 < text.Length && text[position] == '/' && text[position + 1] == '/';

    // The end of a quoted Text with doubled delimiters, or the end of the text when it stays open.
    private static int QuotedEnd(string text, int position)
    {
        var quote = text[position++];
        while (position < text.Length)
        {
            if (text[position++] != quote) continue;
            if (position < text.Length && text[position] == quote) position++;
            else return position;
        }
        return position;
    }

    private static int NameEnd(string text, int position)
    {
        while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || GesText.IsAsciiDigit(text[position]) || text[position] == '_')) position++;
        return position;
    }

    // A number starts with a digit, or a sign or `.` before a digit, or a sign before `Infinity`.
    private static bool StartsNumber(string text, int position)
    {
        var c = text[position];
        if (GesText.IsAsciiDigit(c)) return true;
        if (c is not ('+' or '-' or '.') || position + 1 >= text.Length) return false;
        var next = text[position + 1];
        return GesText.IsAsciiDigit(next) || next == '.' && c != '.' && position + 2 < text.Length && GesText.IsAsciiDigit(text[position + 2])
            || c != '.' && string.CompareOrdinal(text, position + 1, "Infinity", 0, 8) == 0;
    }

    // An unquoted value token, such as `21.4kN` or `90%`, ends at whitespace, a separator, a bracket or a comment.
    private static int ValueEnd(string text, int position)
    {
        while (position < text.Length && text[position] is not (' ' or '\t' or '\r' or '\n' or ',' or '[' or ']' or ')') && !IsComment(text, position)) position++;
        return position;
    }

    // A word is a key or label when a colon follows on its line.
    private static bool FollowedByColon(string text, int position)
    {
        while (position < text.Length)
        {
            if (text[position] is ' ' or '\t') position++;
            else if (IsComment(text, position)) return false;
            else return text[position] == ':';
        }
        return false;
    }
}
