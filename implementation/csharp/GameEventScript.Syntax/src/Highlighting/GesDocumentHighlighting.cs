// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Code = GameEventScript.Syntax.GesDocumentDiagnostic;

namespace GameEventScript.Syntax;

// Categorizes GES document text token by token, also after an error: keys and labels before their colon, the schema
// header, typed forms with their message and parameter names, scalars and comments, and in a catalog file its header,
// imports, module blocks and references. The diagnostic is the reader's.
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
        var catalog = GesDocumentReader.ReadCatalog(text);
        var isCatalog = catalog.IsCatalog;
        var contexts = new List<Context>();
        // Whether a module name follows, until its `{`.
        var inModuleName = false;
        // The form a `:Type` names until its `(` opens, and whether the next token is the first inside a context.
        Context? pending = null;
        var first = false;
        var afterSeriesKind = false;
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

            if (isCatalog && contexts.Count == 0 && CatalogSpan(text, ref position, ref inModuleName) is { } catalogKind)
            {
                spans.Add(start, position - start, catalogKind);
                pending = null;
                first = false;
                afterSeriesKind = false;
                continue;
            }
            var opening = pending;
            var firstInContext = first;
            var seriesKindFollows = afterSeriesKind;
            if (c != ':') afterSeriesKind = false;
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
                kind = name == "Schema" || (isCatalog && name == "Catalog") ? GesSyntaxKind.Keyword : GesSyntaxKind.Type;
                pending = name switch
                {
                    "Message" => Context.MessageForm,
                    "Handler" => Context.HandlerForm,
                    "Series" => Context.SeriesForm,
                    _ => Context.Form
                };
            }
            else if (c == '@' && position + 1 < text.Length && GesText.IsAsciiUpper(text[position + 1]))
            {
                position = HandleSpans(text, position, spans);
                continue;
            }
            else if (c == '@' && position + 1 < text.Length && IsKeyCharacter(text[position + 1]))
            {
                position = ReferenceSpans(text, position, spans);
                continue;
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
                if (context is Context.MessageForm or Context.HandlerForm && firstInContext && GesText.IsAsciiUpper(c))
                {
                    kind = GesSyntaxKind.Message;
                    pending = context == Context.HandlerForm ? Context.Handler : Context.Message;
                }
                else if (FollowedByColon(text, position) || context == Context.Handler)
                {
                    kind = GesSyntaxKind.Identifier;
                    afterSeriesKind = word == "kind" && context == Context.SeriesForm;
                }
                else if (word is "true" or "false" or "nothing") kind = GesSyntaxKind.Keyword;
                else if (word == "with" && context == Context.MessageForm) kind = GesSyntaxKind.Keyword;
                else if (word is "fibonacci" or "factorial" && context == Context.SeriesForm && (firstInContext || seriesKindFollows)) kind = GesSyntaxKind.Builtin;
                else if (word is "Infinity" or "NaN") kind = GesSyntaxKind.Number;
            }
            else position += GesText.ScalarUtf16LengthAt(text, position);
            spans.Add(start, position - start, kind);
        }

        var diagnostic = isCatalog ? catalog.Diagnostic : GesDocumentReader.Read(text, references: true).Diagnostic;
        if (diagnostic is not null)
            diagnostics.Add(new GesHighlightDiagnostic(diagnostic.Code, Code.Explain(diagnostic.Code, diagnostic.LimitName, diagnostic.LimitValue), diagnostic.Start, diagnostic.End - diagnostic.Start));
    }

    // A token of a catalog file outside every bracket that the catalog grammar owns: braces, `import` and `module`
    // without a key colon, a module name and a key. Returns null for every other token, which documents categorize.
    private static GesSyntaxKind? CatalogSpan(string text, ref int position, ref bool inModuleName)
    {
        var c = text[position];
        if (c is '{' or '}')
        {
            inModuleName = false;
            position++;
            return GesSyntaxKind.Symbol;
        }
        if (inModuleName && c == '.')
        {
            position++;
            return GesSyntaxKind.Symbol;
        }
        if (!IsKeyCharacter(c)) return null;
        var end = position;
        while (end < text.Length && IsKeyCharacter(text[end])) end++;
        var word = text.Substring(position, end - position);
        if (inModuleName)
        {
            position = end;
            return GesSyntaxKind.Identifier;
        }
        if (FollowedByColon(text, end))
        {
            position = end;
            return GesSyntaxKind.Identifier;
        }
        if (word is "import" or "module")
        {
            inModuleName = word == "module";
            position = end;
            return GesSyntaxKind.Keyword;
        }
        return null;
    }

    // A reference: `@`, `.` and `~` are Symbol and each key Identifier. Returns the end.
    private static int ReferenceSpans(string text, int start, GesSpanBuilder spans)
    {
        spans.Add(start, 1, GesSyntaxKind.Symbol);
        var position = start + 1;
        while (true)
        {
            var keyStart = position;
            while (position < text.Length && IsKeyCharacter(text[position])) position++;
            spans.Add(keyStart, position - keyStart, GesSyntaxKind.Identifier);
            if (position + 1 < text.Length && text[position] == '.' && IsKeyCharacter(text[position + 1]))
            {
                spans.Add(position++, 1, GesSyntaxKind.Symbol);
                continue;
            }
            if (position < text.Length && text[position] == '~') spans.Add(position++, 1, GesSyntaxKind.Symbol);
            return position;
        }
    }

    private static bool IsKeyCharacter(char c) => GesText.IsAsciiLetter(c) || GesText.IsAsciiDigit(c) || c is '_' or '-';

    // A handle: `@Type` is Type; `:`, `~` and `/` are Symbol; a quoted id, part or ref is String, a ref `#name` Tag, an
    // integer id, part or version and a hash Number, and other bare ids and parts Plain. Without a well-formed handle only
    // `@Type` is a token. Returns the end.
    private static int HandleSpans(string text, int start, GesSpanBuilder spans)
    {
        var typeEnd = NameEnd(text, start + 1);
        spans.Add(start, typeEnd - start, GesSyntaxKind.Type);
        var end = GesHandleLiteral.Read(text, start, true, out _, out _, out _);
        if (end < 0) return typeEnd;
        var position = typeEnd;
        var isPart = false;
        while (position < end)
        {
            var c = text[position];
            var tokenStart = position;
            GesSyntaxKind kind;
            if (c is ':' or '~' || c == '/' && !isPart)
            {
                isPart |= c == '/';
                position++;
                kind = GesSyntaxKind.Symbol;
            }
            else if (c == '\'')
            {
                position = QuotedEnd(text, position);
                kind = GesSyntaxKind.String;
            }
            else if (c == '#')
            {
                position = NameEnd(text, position + 1);
                kind = GesSyntaxKind.Tag;
            }
            else
            {
                while (position < end && text[position] is not (':' or '~' or '\'') && (isPart || text[position] != '/')) position++;
                var word = text.AsSpan(tokenStart, position - tokenStart);
                var afterTilde = tokenStart > 0 && text[tokenStart - 1] == '~';
                kind = GesHandleLiteral.ReadsAsInteger(word) || afterTilde ? GesSyntaxKind.Number : GesSyntaxKind.Plain;
            }
            spans.Add(tokenStart, position - tokenStart, kind);
        }
        return end;
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
        var end = GesNumberLiteral.TokenEnd(text, position, brackets: true);
        while (position < end && !IsComment(text, position)) position++;
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
