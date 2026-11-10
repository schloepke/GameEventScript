// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Syntax;

// Categorizes GESA line by line; a dumper string may contain line breaks and continues its line. A source segment holds
// GES up to the first line, outside a GES token, that starts with `.region-end` or `.segment`; a source-line annotation
// holds one GES line after its `|`. Both are highlighted as GES.
internal static class GesAssemblyHighlighting
{
    internal static void Highlight(string text, GesSpanBuilder spans)
    {
        var labels = DefinedLabels(text);
        var position = 0;
        while (position < text.Length)
        {
            var end = LogicalLineEnd(text, position);
            var next = LineEnd(text, end);
            var sourceSegment = Line(text, position, end, labels, spans);
            spans.Add(end, next - end, GesSyntaxKind.Plain);
            position = next;
            if (!sourceSegment) continue;

            var blockEnd = SourceSegmentEnd(text, position);
            GesSourceHighlighting.Highlight(text.Substring(position, blockEnd - position), position, spans, null);
            position = blockEnd;
        }
    }

    // The names defined as `name:` at the start of a line; operands that name one are label references.
    private static HashSet<string> DefinedLabels(string text)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        while (position < text.Length)
        {
            var end = LogicalLineEnd(text, position);
            var start = SkipSpaces(text, position, end);
            var word = WordEnd(text, start, end);
            if (word > start && word < end && text[word] == ':' && !IsDigit(text[start])) labels.Add(text.Substring(start, word - start));
            position = LineEnd(text, end);
        }
        return labels;
    }

    // Where the content of the line starting at `start` ends: at the first line break outside a dumper string. A comment
    // and a source-line annotation end with their physical line.
    private static int LogicalLineEnd(string text, int start)
    {
        var end = ContentEnd(text, start);
        if (IsDirective(text, SkipSpaces(text, start, end), end, ".source-line")) return end;
        var position = start;
        while (position < text.Length && text[position] is not ('\r' or '\n'))
        {
            if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/') return ContentEnd(text, position);
            position = text[position] == '"' ? StringEnd(text, position, text.Length) : position + 1;
        }
        return position;
    }

    // The first line start after `start`, outside a GES token, whose line begins with `.region-end` or `.segment`.
    private static int SourceSegmentEnd(string text, int start)
    {
        var source = text.Substring(start);
        if (EndsSourceSegment(source, 0)) return start;
        var lexer = new GesLexer(source);
        while (true)
        {
            var token = lexer.ReadNextToken();
            if (token.Kind == GesTokenKind.EndOfFile) return text.Length;
            if (token.Kind == GesTokenKind.NewLine && EndsSourceSegment(source, token.End)) return start + token.End;
        }
    }

    private static bool EndsSourceSegment(string source, int position)
    {
        var end = ContentEnd(source, position);
        var start = SkipSpaces(source, position, end);
        return IsDirective(source, start, end, ".region-end") || IsDirective(source, start, end, ".segment");
    }

    // Adds the pieces of one line's content and reports whether it opens a source segment.
    private static bool Line(string text, int start, int end, HashSet<string> labels, GesSpanBuilder spans)
    {
        var position = SkipSpaces(text, start, end);
        spans.Add(start, position - start, GesSyntaxKind.Plain);
        if (IsDirective(text, position, end, ".region") || IsDirective(text, position, end, ".region-end"))
        {
            spans.Add(position, end - position, GesSyntaxKind.Comment);
            return false;
        }

        if (IsDirective(text, position, end, ".source-line"))
        {
            SourceLine(text, position, end, spans);
            return false;
        }

        var sourceSegment = false;
        var mnemonic = true;
        var segment = false;
        while (position < end)
        {
            var c = text[position];
            if (c is ' ' or '\t')
            {
                var spaces = SkipSpaces(text, position, end);
                spans.Add(position, spaces - position, GesSyntaxKind.Plain);
                position = spaces;
                continue;
            }

            if (c == '/' && position + 1 < end && text[position + 1] == '/')
            {
                spans.Add(position, end - position, GesSyntaxKind.Comment);
                return sourceSegment;
            }

            if (c == '"')
            {
                var close = StringEnd(text, position, end);
                spans.Add(position, close - position, GesSyntaxKind.String);
                position = close;
                continue;
            }

            if (c == '.' && position + 1 < end && IsWordStart(text[position + 1]))
            {
                var directive = DirectiveEnd(text, position + 1, end);
                segment = string.CompareOrdinal(text, position, ".segment", 0, directive - position) == 0 && directive - position == 8;
                spans.Add(position, directive - position, GesSyntaxKind.Keyword);
                position = directive;
                mnemonic = false;
                continue;
            }

            if (c == '#')
            {
                var immediate = ImmediateEnd(text, position + 1, end);
                spans.Add(position, immediate - position, GesSyntaxKind.Number);
                position = immediate;
                continue;
            }

            if (c == '@' && position + 1 < end && IsDigit(text[position + 1]))
            {
                var address = DigitsEnd(text, position + 1, end);
                spans.Add(position, address - position, GesSyntaxKind.Comment);
                position = address;
                continue;
            }

            if (IsDigit(c))
            {
                var number = WordEnd(text, position, end);
                spans.Add(position, number - position, GesSyntaxKind.Number);
                position = number;
                continue;
            }

            if (IsWordStart(c))
            {
                var wordEnd = WordEnd(text, position, end);
                var word = text.Substring(position, wordEnd - position);
                var after = SkipSpaces(text, wordEnd, end);
                GesSyntaxKind kind;
                if (segment)
                {
                    kind = GesSyntaxKind.Tag;
                    sourceSegment = word == "source";
                    segment = false;
                }
                else if (mnemonic && wordEnd < end && text[wordEnd] == ':' && labels.Contains(word)) kind = GesSyntaxKind.Label;
                else if (mnemonic)
                {
                    kind = GesSyntaxKind.Keyword;
                    mnemonic = false;
                }
                else if (word == "unit" && wordEnd < end && text[wordEnd] == ':')
                {
                    spans.Add(position, wordEnd - position, GesSyntaxKind.Keyword);
                    var unit = WordEnd(text, wordEnd + 1, end);
                    spans.Add(wordEnd, unit - wordEnd, GesSyntaxKind.Tag);
                    position = unit;
                    continue;
                }
                else if (IsRegister(word) && wordEnd < end && text[wordEnd] == '(')
                {
                    spans.Add(position, wordEnd - position, GesSyntaxKind.Register);
                    var name = WordEnd(text, wordEnd + 1, end);
                    if (name < end && text[name] == ')')
                    {
                        spans.Add(wordEnd, 1, GesSyntaxKind.Symbol);
                        spans.Add(wordEnd + 1, name - wordEnd - 1, GesSyntaxKind.Label);
                        spans.Add(name, 1, GesSyntaxKind.Symbol);
                        position = name + 1;
                    }
                    else position = wordEnd;
                    continue;
                }
                else if (IsRegister(word)) kind = GesSyntaxKind.Register;
                else if (after < end && text[after] == '=') kind = GesSyntaxKind.Identifier;
                else if (labels.Contains(word) || word.IndexOf('_') >= 0) kind = GesSyntaxKind.Label;
                else kind = c is >= 'A' and <= 'Z' ? GesSyntaxKind.Type : GesSyntaxKind.Plain;
                spans.Add(position, wordEnd - position, kind);
                position = wordEnd;
                continue;
            }

            spans.Add(position, 1, c switch
            {
                '[' or ']' or '(' or ')' or ',' or ':' => GesSyntaxKind.Symbol,
                '=' => GesSyntaxKind.Keyword,
                _ => GesSyntaxKind.Plain
            });
            position++;
        }

        return sourceSegment;
    }

    // `.source-line "file" 3 | text`: the text after the separator is one GES line.
    private static void SourceLine(string text, int position, int end, GesSpanBuilder spans)
    {
        var directive = DirectiveEnd(text, position + 1, end);
        spans.Add(position, directive - position, GesSyntaxKind.Keyword);
        position = directive;
        while (position < end)
        {
            var c = text[position];
            if (c == '|')
            {
                spans.Add(position, 1, GesSyntaxKind.Symbol);
                GesSourceHighlighting.Highlight(text.Substring(position + 1, end - position - 1), position + 1, spans, null);
                return;
            }

            var next = c == '"' ? StringEnd(text, position, end) : IsDigit(c) ? DigitsEnd(text, position, end) : position + 1;
            spans.Add(position, next - position, c == '"' ? GesSyntaxKind.String : IsDigit(c) ? GesSyntaxKind.Number : GesSyntaxKind.Plain);
            position = next;
        }
    }

    private static bool IsDirective(string text, int start, int end, string directive)
        => end - start >= directive.Length && string.CompareOrdinal(text, start, directive, 0, directive.Length) == 0
            && (start + directive.Length == end || text[start + directive.Length] is ' ' or '\t');

    private static bool IsRegister(string word)
    {
        if (word.Length < 2 || word[0] != 'r') return false;
        for (var i = 1; i < word.Length; i++)
            if (!IsDigit(word[i])) return false;
        return true;
    }

    // A dumper string with `\\` and `\"` escapes; an unterminated one ends at `end`.
    private static int StringEnd(string text, int start, int end)
    {
        var position = start + 1;
        while (position < end)
        {
            if (text[position] == '\\' && position + 1 < end) position += 2;
            else if (text[position++] == '"') return position;
        }
        return end;
    }

    private static int ImmediateEnd(string text, int position, int end)
    {
        while (position < end && (IsWordPart(text[position]) || text[position] is '+' or '-' or '.')) position++;
        return position;
    }

    private static int DirectiveEnd(string text, int position, int end)
    {
        while (position < end && (IsWordPart(text[position]) || text[position] == '-')) position++;
        return position;
    }

    private static int WordEnd(string text, int position, int end)
    {
        while (position < end && IsWordPart(text[position])) position++;
        return position;
    }

    private static int DigitsEnd(string text, int position, int end)
    {
        while (position < end && IsDigit(text[position])) position++;
        return position;
    }

    private static int SkipSpaces(string text, int position, int end)
    {
        while (position < end && text[position] is ' ' or '\t') position++;
        return position;
    }

    private static int ContentEnd(string text, int position)
    {
        while (position < text.Length && text[position] is not ('\r' or '\n')) position++;
        return position;
    }

    private static int LineEnd(string text, int contentEnd)
    {
        if (contentEnd < text.Length && text[contentEnd] == '\r') contentEnd++;
        else if (contentEnd < text.Length && text[contentEnd] == '\n') return contentEnd + 1;
        if (contentEnd < text.Length && text[contentEnd] == '\n') contentEnd++;
        return contentEnd;
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsWordStart(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';

    private static bool IsWordPart(char c) => IsWordStart(c) || IsDigit(c);
}
