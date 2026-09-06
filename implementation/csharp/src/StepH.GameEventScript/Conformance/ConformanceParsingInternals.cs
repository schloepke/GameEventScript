// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StepH.GameEventScript.Conformance;

internal sealed class ConformanceFailure : Exception
{
    internal ConformanceFailure(string code, string message, ConformanceSourceRange range) : base(message)
    {
        Diagnostic = new ConformanceDiagnostic(code, message, range);
    }

    internal ConformanceDiagnostic Diagnostic { get; }
}

internal readonly struct ConformanceLine
{
    internal ConformanceLine(string text, int lineNumber, int byteOffset, int byteLength, int endingByteLength)
    {
        Text = text;
        LineNumber = lineNumber;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        EndingByteLength = endingByteLength;
    }

    internal string Text { get; }
    internal int LineNumber { get; }
    internal int ByteOffset { get; }
    internal int ByteLength { get; }
    internal int EndingByteLength { get; }
    internal int NextByteOffset => ByteOffset + ByteLength + EndingByteLength;

    internal ConformanceSourceRange Range(int characterOffset = 0, int? characterLength = null)
    {
        var length = characterLength ?? Math.Max(0, Text.Length - characterOffset);
        var prefix = Text.Substring(0, characterOffset);
        var value = Text.Substring(characterOffset, length);
        var byteOffset = ByteOffset + Encoding.UTF8.GetByteCount(prefix);
        var byteLength = Encoding.UTF8.GetByteCount(value);
        var column = CountScalars(prefix) + 1;
        return new ConformanceSourceRange(byteOffset, byteLength, LineNumber, column, LineNumber, column + CountScalars(value));
    }

    private static int CountScalars(string value)
    {
        var count = 0;
        for (var index = 0; index < value.Length; index++, count++)
        {
            if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) index++;
        }

        return count;
    }
}

internal sealed class ConformanceText
{
    private ConformanceText(byte[] original, bool hasBom, List<ConformanceLine> lines, string lineEnding)
    {
        Original = original;
        HasBom = hasBom;
        Lines = lines;
        LineEnding = lineEnding;
    }

    internal byte[] Original { get; }
    internal bool HasBom { get; }
    internal List<ConformanceLine> Lines { get; }
    internal string LineEnding { get; }

    internal static ConformanceText Decode(ReadOnlySpan<byte> source, ConformanceParserLimits limits)
    {
        if (limits.MaxDocumentBytes <= 0 || source.Length > limits.MaxDocumentBytes)
        {
            throw AtStart(ConformanceDiagnosticCodes.YamlLimitExceeded, "The conformance document exceeds MaxDocumentBytes.");
        }

        var original = source.ToArray();
        var hasBom = source.Length >= 3 && source[0] == 0xef && source[1] == 0xbb && source[2] == 0xbf;
        var start = hasBom ? 3 : 0;
        var strictUtf8 = new UTF8Encoding(false, true);
        try
        {
            _ = strictUtf8.GetString(original, start, original.Length - start);
        }
        catch (DecoderFallbackException)
        {
            throw AtStart(ConformanceDiagnosticCodes.InvalidUtf8, "The conformance document is not valid UTF-8.");
        }

        for (var index = start; index < original.Length; index++)
        {
            if (original[index] == 0)
            {
                throw new ConformanceFailure(ConformanceDiagnosticCodes.InvalidUtf8, "The conformance document contains a NUL byte.", new ConformanceSourceRange(index - start, 1, 1, 1, 1, 2));
            }
        }

        var lines = new List<ConformanceLine>();
        var lineStart = start;
        var lineNumber = 1;
        var lf = 0;
        var crlf = 0;
        var cr = 0;
        for (var index = start; index < original.Length; index++)
        {
            var ending = 0;
            if (original[index] == 0x0a)
            {
                ending = 1;
                lf++;
            }
            else if (original[index] == 0x0d)
            {
                if (index + 1 < original.Length && original[index + 1] == 0x0a)
                {
                    ending = 2;
                    crlf++;
                }
                else
                {
                    ending = 1;
                    cr++;
                }
            }

            if (ending == 0) continue;
            var contentLength = index - lineStart;
            lines.Add(new ConformanceLine(strictUtf8.GetString(original, lineStart, contentLength), lineNumber++, lineStart - start, contentLength, ending));
            index += ending - 1;
            lineStart = index + 1;
        }

        if (lineStart < original.Length || original.Length == start || (original.Length > start && original[^1] != 0x0a && original[^1] != 0x0d))
        {
            var contentLength = original.Length - lineStart;
            lines.Add(new ConformanceLine(strictUtf8.GetString(original, lineStart, contentLength), lineNumber, lineStart - start, contentLength, 0));
        }

        var lineEnding = crlf >= lf && crlf >= cr && crlf > 0 ? "\r\n" : cr > lf && cr > 0 ? "\r" : "\n";
        return new ConformanceText(original, hasBom, lines, lineEnding);
    }

    internal ConformanceSourceRange LinesRange(int first, int lastInclusive)
    {
        var firstLine = Lines[first];
        var lastLine = Lines[lastInclusive];
        var end = lastLine.ByteOffset + lastLine.ByteLength;
        return new ConformanceSourceRange(firstLine.ByteOffset, end - firstLine.ByteOffset, firstLine.LineNumber, 1, lastLine.LineNumber, ScalarLength(lastLine.Text) + 1);
    }

    internal static string JoinPayload(IReadOnlyList<ConformanceLine> lines)
    {
        if (lines.Count == 0) return string.Empty;
        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0) builder.Append('\n');
            builder.Append(lines[index].Text);
        }

        return builder.ToString();
    }

    internal static ConformanceSourceRange PayloadRange(IReadOnlyList<ConformanceLine> lines, ConformanceLine openingFence)
    {
        if (lines.Count == 0)
        {
            return new ConformanceSourceRange(openingFence.NextByteOffset, 0, openingFence.LineNumber + 1, 1, openingFence.LineNumber + 1, 1);
        }

        var first = lines[0];
        var last = lines[^1];
        return new ConformanceSourceRange(first.ByteOffset, last.ByteOffset + last.ByteLength - first.ByteOffset, first.LineNumber, 1, last.LineNumber, ScalarLength(last.Text) + 1);
    }

    private static int ScalarLength(string value)
    {
        var result = 0;
        for (var index = 0; index < value.Length; index++, result++)
        {
            if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) index++;
        }

        return result;
    }

    private static ConformanceFailure AtStart(string code, string message)
        => new(code, message, new ConformanceSourceRange(0, 0, 1, 1, 1, 1));
}

internal enum YamlNodeKind
{
    Null,
    Boolean,
    Integer,
    Decimal,
    String,
    Sequence,
    Mapping
}

internal sealed class YamlNode
{
    internal YamlNode(YamlNodeKind kind, string? scalar, ConformanceSourceRange range)
    {
        Kind = kind;
        Scalar = scalar;
        Range = range;
    }

    internal YamlNodeKind Kind { get; }
    internal string? Scalar { get; }
    internal ConformanceSourceRange Range { get; }
    internal List<YamlNode> Items { get; } = new();
    internal List<YamlProperty> Properties { get; } = new();
}

internal sealed class YamlProperty
{
    internal YamlProperty(string name, YamlNode value, ConformanceSourceRange nameRange)
    {
        Name = name;
        Value = value;
        NameRange = nameRange;
    }

    internal string Name { get; }
    internal YamlNode Value { get; }
    internal ConformanceSourceRange NameRange { get; }
}

internal sealed class RestrictedYamlParser
{
    private readonly IReadOnlyList<ConformanceLine> _lines;
    private readonly ConformanceParserLimits _limits;
    private int _index;
    private int _nodeCount;

    internal RestrictedYamlParser(IReadOnlyList<ConformanceLine> lines, ConformanceParserLimits limits)
    {
        _lines = lines;
        _limits = limits;
    }

    internal YamlNode ParseRootMapping()
    {
        SkipEmpty();
        if (_index >= _lines.Count) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A YAML root mapping is required.", EmptyRange());
        var root = ParseBlock(0, 0);
        SkipEmpty();
        if (_index != _lines.Count) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "Unexpected YAML content.", _lines[_index].Range());
        if (root.Kind != YamlNodeKind.Mapping) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "The YAML root must be a mapping.", root.Range);
        return root;
    }

    private YamlNode ParseBlock(int indent, int depth)
    {
        CheckDepth(depth);
        SkipEmpty();
        if (_index >= _lines.Count) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A YAML value is required.", EmptyRange());
        ValidateIndent(_lines[_index], indent);
        var content = ContentAt(_lines[_index], indent);
        if (content is "---" or "..." || content.StartsWith("%", StringComparison.Ordinal))
            throw Error(ConformanceDiagnosticCodes.YamlUnsupportedFeature, "YAML directives and multiple documents are not supported.", _lines[_index].Range(indent));
        return content.StartsWith("-", StringComparison.Ordinal)
            ? ParseSequence(indent, depth)
            : ParseMapping(indent, depth);
    }

    private YamlNode ParseMapping(int indent, int depth)
    {
        var start = _lines[_index].Range(indent);
        var result = New(YamlNodeKind.Mapping, null, start);
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (_index < _lines.Count)
        {
            if (IsEmpty(_lines[_index].Text)) { _index++; continue; }
            var actualIndent = CountIndent(_lines[_index]);
            if (actualIndent < indent) break;
            if (actualIndent > indent) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "YAML indentation must increase only after an empty mapping or sequence value.", _lines[_index].Range());
            var line = _lines[_index];
            var content = StripComment(ContentAt(line, indent)).TrimEnd(' ', '\t');
            if (content.StartsWith("-", StringComparison.Ordinal)) break;
            var colon = FindMappingColon(content);
            if (colon < 1) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A mapping entry must contain a key followed by ':'.", line.Range(indent));
            var keyToken = content.Substring(0, colon).TrimEnd(' ');
            var key = ParseKey(keyToken, line.Range(indent, keyToken.Length));
            if (!names.Add(key)) throw Error(ConformanceDiagnosticCodes.YamlDuplicateKey, $"Duplicate YAML key '{key}'.", line.Range(indent, keyToken.Length));
            var valueText = content.Substring(colon + 1).TrimStart(' ');
            var valueOffset = indent + colon + 1 + (content.Substring(colon + 1).Length - valueText.Length);
            _index++;
            YamlNode value;
            if (valueText.Length == 0)
            {
                var next = NextContentIndex(_index);
                if (next >= _lines.Count || CountIndent(_lines[next]) <= indent)
                {
                    value = New(YamlNodeKind.Null, null, line.Range(Math.Min(line.Text.Length, indent + colon + 1), 0));
                }
                else
                {
                    if (CountIndent(_lines[next]) != indent + 2) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "YAML uses exactly two spaces per indentation level.", _lines[next].Range());
                    while (_index < next) _index++;
                    value = ParseBlock(indent + 2, depth + 1);
                }
            }
            else
            {
                value = ParseInline(valueText, line, valueOffset, depth + 1);
            }

            result.Properties.Add(new YamlProperty(key, value, line.Range(indent, keyToken.Length)));
        }

        return result;
    }

    private YamlNode ParseSequence(int indent, int depth)
    {
        var result = New(YamlNodeKind.Sequence, null, _lines[_index].Range(indent));
        while (_index < _lines.Count)
        {
            if (IsEmpty(_lines[_index].Text)) { _index++; continue; }
            var actualIndent = CountIndent(_lines[_index]);
            if (actualIndent < indent) break;
            if (actualIndent != indent) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "Invalid YAML sequence indentation.", _lines[_index].Range());
            var line = _lines[_index];
            var raw = StripComment(ContentAt(line, indent)).TrimEnd(' ', '\t');
            if (raw.Length == 0 || raw[0] != '-' || (raw.Length > 1 && raw[1] != ' ')) break;
            var itemText = raw.Length == 1 ? string.Empty : raw.Substring(2);
            _index++;
            if (itemText.Length == 0)
            {
                var next = NextContentIndex(_index);
                if (next >= _lines.Count || CountIndent(_lines[next]) <= indent) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A sequence item requires a value.", line.Range(indent));
                if (CountIndent(_lines[next]) != indent + 2) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "YAML uses exactly two spaces per indentation level.", _lines[next].Range());
                while (_index < next) _index++;
                result.Items.Add(ParseBlock(indent + 2, depth + 1));
                continue;
            }

            var colon = FindMappingColon(itemText);
            if (colon > 0 && !StartsFlowOrQuote(itemText))
            {
                var map = New(YamlNodeKind.Mapping, null, line.Range(indent + 2));
                var names = new HashSet<string>(StringComparer.Ordinal);
                AddInlineSequenceMapProperty(map, names, itemText, line, indent + 2, colon, depth + 1, false);
                ParseSequenceMapContinuation(map, names, indent + 2, depth + 1);
                result.Items.Add(map);
            }
            else
            {
                result.Items.Add(ParseInline(itemText, line, indent + 2, depth + 1));
            }
        }

        return result;
    }

    private void ParseSequenceMapContinuation(YamlNode map, HashSet<string> names, int indent, int depth)
    {
        while (true)
        {
            var next = NextContentIndex(_index);
            if (next >= _lines.Count || CountIndent(_lines[next]) < indent) return;
            if (CountIndent(_lines[next]) > indent) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "Unexpected YAML indentation.", _lines[next].Range());
            if (ContentAt(_lines[next], indent).StartsWith("-", StringComparison.Ordinal)) return;
            while (_index < next) _index++;
            var line = _lines[_index];
            var content = StripComment(ContentAt(line, indent)).TrimEnd(' ', '\t');
            var colon = FindMappingColon(content);
            if (colon < 1) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A mapping entry must contain a key followed by ':'.", line.Range(indent));
            AddInlineSequenceMapProperty(map, names, content, line, indent, colon, depth, true);
        }
    }

    private void AddInlineSequenceMapProperty(YamlNode map, HashSet<string> names, string content, ConformanceLine line, int offset, int colon, int depth, bool advanceLine)
    {
        var keyToken = content.Substring(0, colon).TrimEnd(' ');
        var key = ParseKey(keyToken, line.Range(offset, keyToken.Length));
        if (!names.Add(key)) throw Error(ConformanceDiagnosticCodes.YamlDuplicateKey, $"Duplicate YAML key '{key}'.", line.Range(offset, keyToken.Length));
        var valueText = content.Substring(colon + 1).TrimStart(' ');
        var valueOffset = offset + colon + 1 + content.Substring(colon + 1).Length - valueText.Length;
        if (advanceLine) _index++;
        YamlNode value;
        if (valueText.Length == 0)
        {
            var next = NextContentIndex(_index);
            if (next >= _lines.Count || CountIndent(_lines[next]) <= offset)
            {
                value = New(YamlNodeKind.Null, null, line.Range(offset + colon + 1, 0));
            }
            else
            {
                if (CountIndent(_lines[next]) != offset + 2) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "YAML uses exactly two spaces per indentation level.", _lines[next].Range());
                while (_index < next) _index++;
                value = ParseBlock(offset + 2, depth + 1);
            }
        }
        else
        {
            value = ParseInline(valueText, line, valueOffset, depth + 1);
        }

        map.Properties.Add(new YamlProperty(key, value, line.Range(offset, keyToken.Length)));
    }

    private YamlNode ParseInline(string value, ConformanceLine line, int characterOffset, int depth)
    {
        CheckDepth(depth);
        var parser = new FlowParser(this, value, line, characterOffset, depth);
        return parser.Parse();
    }

    private string ParseKey(string token, ConformanceSourceRange range)
    {
        if (token.Length == 0) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A mapping key cannot be empty.", range);
        if (token[0] == '\'' || token[0] == '"')
        {
            var fakeLine = new ConformanceLine(token, range.Line, range.ByteOffset, range.ByteLength, 0);
            var parsed = ParseInline(token, fakeLine, 0, 1);
            if (parsed.Kind != YamlNodeKind.String) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A quoted key must be a string.", range);
            return parsed.Scalar!;
        }

        if (token == "<<") throw Error(ConformanceDiagnosticCodes.YamlUnsupportedFeature, "YAML merge keys are not supported.", range);
        if (!IsKeyToken(token)) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A plain mapping key uses [A-Za-z][A-Za-z0-9_.-]*.", range);
        return token;
    }

    private YamlNode New(YamlNodeKind kind, string? scalar, ConformanceSourceRange range)
    {
        _nodeCount++;
        if (_limits.MaxYamlNodes <= 0 || _nodeCount > _limits.MaxYamlNodes) throw Error(ConformanceDiagnosticCodes.YamlLimitExceeded, "The YAML node limit was exceeded.", range);
        if (scalar is not null && Encoding.UTF8.GetByteCount(scalar) > _limits.MaxScalarBytes) throw Error(ConformanceDiagnosticCodes.YamlLimitExceeded, "A YAML scalar exceeds MaxScalarBytes.", range);
        return new YamlNode(kind, scalar, range);
    }

    private void CheckDepth(int depth)
    {
        if (_limits.MaxYamlDepth <= 0 || depth > _limits.MaxYamlDepth) throw Error(ConformanceDiagnosticCodes.YamlLimitExceeded, "The YAML nesting depth exceeds MaxYamlDepth.", EmptyRange());
    }

    private void ValidateIndent(ConformanceLine line, int expected)
    {
        var actual = CountIndent(line);
        if (actual != expected || actual % 2 != 0) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "YAML uses exactly two spaces per indentation level.", line.Range());
    }

    private static int CountIndent(ConformanceLine line)
    {
        var count = 0;
        while (count < line.Text.Length && line.Text[count] == ' ') count++;
        if (count < line.Text.Length && line.Text[count] == '\t') throw Error(ConformanceDiagnosticCodes.YamlSyntax, "Tabs are invalid in YAML indentation.", line.Range(count, 1));
        return count;
    }

    private static string ContentAt(ConformanceLine line, int indent) => line.Text.Substring(Math.Min(indent, line.Text.Length));
    private int NextContentIndex(int start) { while (start < _lines.Count && IsEmpty(_lines[start].Text)) start++; return start; }
    private void SkipEmpty() { _index = NextContentIndex(_index); }
    private static bool IsEmpty(string text) { var trimmed = text.Trim(' ', '\t'); return trimmed.Length == 0 || trimmed[0] == '#'; }
    private ConformanceSourceRange EmptyRange() => _lines.Count == 0 ? new ConformanceSourceRange(0, 0, 1, 1, 1, 1) : _lines[Math.Min(_index, _lines.Count - 1)].Range();

    private static int FindMappingColon(string value)
    {
        var single = false;
        var doubled = false;
        var square = 0;
        var curly = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            if (doubled)
            {
                if (c == '\\') { index++; continue; }
                if (c == '"') doubled = false;
                continue;
            }
            if (single)
            {
                if (c == '\'' && index + 1 < value.Length && value[index + 1] == '\'') { index++; continue; }
                if (c == '\'') single = false;
                continue;
            }
            if (c == '"') { doubled = true; continue; }
            if (c == '\'') { single = true; continue; }
            if (c == '[') square++;
            else if (c == ']') square--;
            else if (c == '{') curly++;
            else if (c == '}') curly--;
            else if (c == ':' && square == 0 && curly == 0 && (index + 1 == value.Length || value[index + 1] == ' ')) return index;
        }
        return -1;
    }

    private static string StripComment(string value)
    {
        var single = false;
        var doubled = false;
        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            if (doubled)
            {
                if (c == '\\') { index++; continue; }
                if (c == '"') doubled = false;
            }
            else if (single)
            {
                if (c == '\'' && index + 1 < value.Length && value[index + 1] == '\'') index++;
                else if (c == '\'') single = false;
            }
            else if (c == '"') doubled = true;
            else if (c == '\'') single = true;
            else if (c == '#' && (index == 0 || char.IsWhiteSpace(value[index - 1]))) return value.Substring(0, index);
        }
        return value;
    }

    private static bool StartsFlowOrQuote(string value) => value[0] == '{' || value[0] == '[' || value[0] == '\'' || value[0] == '"';
    private static bool IsKeyToken(string value)
    {
        if (value.Length == 0 || !IsAsciiLetter(value[0])) return false;
        for (var index = 1; index < value.Length; index++)
        {
            var c = value[index];
            if (!IsAsciiLetter(c) && (c < '0' || c > '9') && c != '_' && c != '.' && c != '-') return false;
        }
        return true;
    }
    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static ConformanceFailure Error(string code, string message, ConformanceSourceRange range) => new(code, message, range);

    private sealed class FlowParser
    {
        private readonly RestrictedYamlParser _owner;
        private readonly string _text;
        private readonly ConformanceLine _line;
        private readonly int _baseOffset;
        private readonly int _depth;
        private int _position;

        internal FlowParser(RestrictedYamlParser owner, string text, ConformanceLine line, int baseOffset, int depth)
        {
            _owner = owner; _text = text; _line = line; _baseOffset = baseOffset; _depth = depth;
        }

        internal YamlNode Parse()
        {
            SkipSpaces();
            var node = ParseValue(_depth);
            SkipSpaces();
            if (_position < _text.Length && _text[_position] == '#' && (_position == 0 || char.IsWhiteSpace(_text[_position - 1]))) return node;
            if (_position != _text.Length) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "Unexpected characters after YAML value.", Range(_position));
            return node;
        }

        private YamlNode ParseValue(int depth)
        {
            _owner.CheckDepth(depth);
            SkipSpaces();
            if (_position >= _text.Length) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A YAML value is required.", Range(_position));
            return _text[_position] switch
            {
                '[' => ParseSequence(depth),
                '{' => ParseMapping(depth),
                '\'' => ParseSingleQuoted(),
                '"' => ParseDoubleQuoted(),
                '|' or '>' or '&' or '*' or '!' or '%' or '@' or '`' => throw Error(ConformanceDiagnosticCodes.YamlUnsupportedFeature, "This YAML feature is not supported.", Range(_position)),
                _ => ParsePlain()
            };
        }

        private YamlNode ParseSequence(int depth)
        {
            var start = _position++;
            var result = _owner.New(YamlNodeKind.Sequence, null, Range(start));
            SkipSpaces();
            if (Consume(']')) return result;
            while (true)
            {
                result.Items.Add(ParseValue(depth + 1));
                SkipSpaces();
                if (Consume(']')) return result;
                Require(',');
                SkipSpaces();
            }
        }

        private YamlNode ParseMapping(int depth)
        {
            var start = _position++;
            var result = _owner.New(YamlNodeKind.Mapping, null, Range(start));
            var names = new HashSet<string>(StringComparer.Ordinal);
            SkipSpaces();
            if (Consume('}')) return result;
            while (true)
            {
                SkipSpaces();
                var keyStart = _position;
                string key;
                if (_position < _text.Length && (_text[_position] == '\'' || _text[_position] == '"'))
                {
                    var keyNode = _text[_position] == '\'' ? ParseSingleQuoted() : ParseDoubleQuoted();
                    key = keyNode.Scalar!;
                }
                else
                {
                    while (_position < _text.Length && _text[_position] != ':' && _text[_position] != '}' && _text[_position] != ',') _position++;
                    var token = _text.Substring(keyStart, _position - keyStart).TrimEnd(' ');
                    key = _owner.ParseKey(token, Range(keyStart, token.Length));
                }
                if (!names.Add(key)) throw Error(ConformanceDiagnosticCodes.YamlDuplicateKey, $"Duplicate YAML key '{key}'.", Range(keyStart));
                SkipSpaces();
                Require(':');
                var value = ParseValue(depth + 1);
                result.Properties.Add(new YamlProperty(key, value, Range(keyStart)));
                SkipSpaces();
                if (Consume('}')) return result;
                Require(',');
            }
        }

        private YamlNode ParseSingleQuoted()
        {
            var start = _position++;
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                var c = _text[_position++];
                if (c != '\'') { builder.Append(c); continue; }
                if (_position < _text.Length && _text[_position] == '\'') { builder.Append('\''); _position++; continue; }
                return _owner.New(YamlNodeKind.String, builder.ToString(), Range(start, _position - start));
            }
            throw Error(ConformanceDiagnosticCodes.YamlSyntax, "Unterminated single-quoted string.", Range(start));
        }

        private YamlNode ParseDoubleQuoted()
        {
            var start = _position++;
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                var c = _text[_position++];
                if (c == '"') return _owner.New(YamlNodeKind.String, builder.ToString(), Range(start, _position - start));
                if (c != '\\')
                {
                    if (char.IsHighSurrogate(c))
                    {
                        if (_position >= _text.Length || !char.IsLowSurrogate(_text[_position]))
                            throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "Unpaired surrogate in string.", Range(_position - 1));
                        builder.Append(c).Append(_text[_position++]);
                        continue;
                    }
                    if (char.IsLowSurrogate(c)) throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "Unpaired surrogate in string.", Range(_position - 1));
                    builder.Append(c);
                    continue;
                }
                if (_position >= _text.Length) break;
                var escaped = _text[_position++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u': AppendUnicodeEscape(builder); break;
                    default: throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "Unsupported escape in double-quoted string.", Range(_position - 2, 2));
                }
            }
            throw Error(ConformanceDiagnosticCodes.YamlSyntax, "Unterminated double-quoted string.", Range(start));
        }

        private void AppendUnicodeEscape(StringBuilder builder)
        {
            var first = ReadHex4();
            if (first is >= 0xd800 and <= 0xdbff)
            {
                if (_position + 2 > _text.Length || _text[_position] != '\\' || _text[_position + 1] != 'u') throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "A high surrogate must be followed by a low surrogate.", Range(_position));
                _position += 2;
                var second = ReadHex4();
                if (second is < 0xdc00 or > 0xdfff) throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "Invalid low surrogate.", Range(_position - 4, 4));
                builder.Append((char)first).Append((char)second);
            }
            else if (first is >= 0xdc00 and <= 0xdfff)
            {
                throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "Unpaired low surrogate.", Range(_position - 4, 4));
            }
            else builder.Append((char)first);
        }

        private int ReadHex4()
        {
            if (_position + 4 > _text.Length) throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "Incomplete Unicode escape.", Range(_position));
            var value = 0;
            for (var index = 0; index < 4; index++)
            {
                var c = _text[_position++];
                var digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0) throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "Invalid Unicode escape.", Range(_position - 1, 1));
                value = value * 16 + digit;
            }
            return value;
        }

        private YamlNode ParsePlain()
        {
            var start = _position;
            while (_position < _text.Length)
            {
                var c = _text[_position];
                if (c == ',' || c == ']' || c == '}' || (c == '#' && (_position == start || char.IsWhiteSpace(_text[_position - 1])))) break;
                _position++;
            }
            var token = _text.Substring(start, _position - start).TrimEnd(' ');
            if (token.Length == 0) throw Error(ConformanceDiagnosticCodes.YamlInvalidScalar, "An empty plain scalar is invalid.", Range(start));
            var range = Range(start, token.Length);
            if (token == "null") return _owner.New(YamlNodeKind.Null, null, range);
            if (token == "true" || token == "false") return _owner.New(YamlNodeKind.Boolean, token, range);
            if (IsInteger(token)) return _owner.New(YamlNodeKind.Integer, token, range);
            if (IsDecimal(token)) return _owner.New(YamlNodeKind.Decimal, token, range);
            if (token is ".nan" or ".inf" or "-.inf" || token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || token.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
                throw Error(ConformanceDiagnosticCodes.YamlUnsupportedFeature, "This YAML numeric form is not supported.", range);
            if (token[0] is '#' or '&' or '*' or '!' or '%' or '@' or '`') throw Error(ConformanceDiagnosticCodes.YamlUnsupportedFeature, "This plain scalar prefix is not supported.", range);
            if (token.Contains(": ", StringComparison.Ordinal)) throw Error(ConformanceDiagnosticCodes.YamlSyntax, "A plain scalar containing ': ' must be quoted.", range);
            return _owner.New(YamlNodeKind.String, token, range);
        }

        private static bool IsInteger(string value)
        {
            var index = value.Length > 0 && value[0] == '-' ? 1 : 0;
            if (index >= value.Length) return false;
            if (value[index] == '0') return index + 1 == value.Length;
            if (value[index] is < '1' or > '9') return false;
            for (index++; index < value.Length; index++) if (value[index] is < '0' or > '9') return false;
            return true;
        }

        private static bool IsDecimal(string value)
        {
            var index = 0;
            if (value.Length > 0 && value[0] == '-') index++;
            if (index >= value.Length) return false;
            if (value[index] == '0') index++;
            else
            {
                if (value[index] is < '1' or > '9') return false;
                while (index < value.Length && value[index] is >= '0' and <= '9') index++;
            }
            var decimalForm = false;
            if (index < value.Length && value[index] == '.')
            {
                decimalForm = true;
                index++;
                var fractionStart = index;
                while (index < value.Length && value[index] is >= '0' and <= '9') index++;
                if (index == fractionStart) return false;
            }
            if (index < value.Length && (value[index] == 'e' || value[index] == 'E'))
            {
                decimalForm = true;
                index++;
                if (index < value.Length && (value[index] == '+' || value[index] == '-')) index++;
                var exponentStart = index;
                while (index < value.Length && value[index] is >= '0' and <= '9') index++;
                if (index == exponentStart) return false;
            }
            return decimalForm && index == value.Length &&
                   double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                   !double.IsNaN(parsed) && !double.IsInfinity(parsed);
        }

        private bool Consume(char expected) { if (_position < _text.Length && _text[_position] == expected) { _position++; return true; } return false; }
        private void Require(char expected) { SkipSpaces(); if (!Consume(expected)) throw Error(ConformanceDiagnosticCodes.YamlSyntax, $"Expected '{expected}'.", Range(_position)); SkipSpaces(); }
        private void SkipSpaces() { while (_position < _text.Length && _text[_position] == ' ') _position++; }
        private ConformanceSourceRange Range(int offset, int length = 1) => _line.Range(_baseOffset + offset, Math.Min(length, Math.Max(0, _line.Text.Length - _baseOffset - offset)));
    }
}
