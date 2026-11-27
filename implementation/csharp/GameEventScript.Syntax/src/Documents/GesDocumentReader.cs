// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using Code = GameEventScript.Syntax.GesDocumentDiagnostic;

namespace GameEventScript.Syntax;

/// <summary>
/// Reads GES document text: a bracket-free root of entries, an optional schema header, comments and line-break
/// separators around the data literals of <c>parse</c>. Reading stops at the first error. Catalog files and the
/// references of their documents are read only on request (specs/Documents.md#catalogs).
/// </summary>
internal sealed class GesDocumentReader
{
    internal const int MaximumInputScalars = 1048576;
    internal const int MaximumDepth = 64;
    internal const int MaximumItems = 65536;

    private readonly string _text;
    // Whether catalog references and pinned handles may appear, as in the documents of a catalog.
    private readonly bool _references;
    private int _position;
    private int _items;
    private GesDocumentDiagnostic? _error;

    private GesDocumentReader(string text, bool references)
    {
        _text = text;
        _references = references;
    }

    // Reads a document; with `references`, a state document of a catalog entry, whose values may be references.
    internal static GesDocumentSyntax Read(string text, bool references = false)
    {
        if (GesText.CountScalars(text) > MaximumInputScalars)
            return new GesDocumentSyntax(text, null, GesDocumentSchemaSyntax.None, "", InputLimit());
        var reader = new GesDocumentReader(text, references);
        var root = reader.ReadDocument(out var schema, out var schemaName);
        return new GesDocumentSyntax(text, reader._error is null ? root : null, schema, schemaName, reader._error ?? (root is null ? reader.Error(Code.InvalidLiteral, reader._position) : null));
    }

    private static GesDocumentDiagnostic InputLimit() => new(Code.LimitExceeded, 0, 0, "MaxLiteralInputScalars", MaximumInputScalars);

    // Reads a catalog file. A text without a `:Catalog` header is no catalog file; reading reports no error for it.
    internal static GesCatalogSyntax ReadCatalog(string text)
    {
        if (GesText.CountScalars(text) > MaximumInputScalars) return new GesCatalogSyntax(text, true, "", 0, [], InputLimit());
        var reader = new GesDocumentReader(text, references: true);
        var items = new List<GesCatalogItem>();
        reader.ReadCatalogFile(items, out var isCatalog, out var typeName, out var headerOffset);
        return new GesCatalogSyntax(text, isCatalog, typeName, headerOffset, items.ToArray(), reader._error);
    }

    private void ReadCatalogFile(List<GesCatalogItem> items, out bool isCatalog, out string typeName, out int headerOffset)
    {
        typeName = "";
        if (_text.Length > 0 && _text[0] == '\uFEFF') _position = 1;
        SkipWhitespace();
        headerOffset = _position;
        isCatalog = IsCatalogHeader();
        if (!isCatalog || !ReadCatalogHeader(out typeName)) return;
        SkipSpacesAndComments();
        if (_position < _text.Length && !AtLineBreak)
        {
            Fail(Code.UnexpectedToken, _position);
            return;
        }
        SkipWhitespace();
        while (_position < _text.Length)
        {
            var itemStart = _position;
            if (IsSchemaHeader() || IsCatalogHeader())
            {
                Fail(Code.MisplacedSchemaHeader, itemStart);
                return;
            }
            if (_items >= MaximumItems)
            {
                Limit("MaxLiteralItems", MaximumItems, itemStart);
                return;
            }
            _items++;
            if (IsItemWord("import"))
            {
                _position += 6;
                SkipSpacesAndComments();
                if (ReadImportPath(out var pathOffset) is not { } path) return;
                items.Add(new GesCatalogItem(GesCatalogItemKind.Import, "", "", itemStart, null, path, pathOffset));
            }
            else if (IsItemWord("module"))
            {
                if (!ReadModule(items)) return;
            }
            else if (!ReadCatalogEntry("", items))
            {
                return;
            }
            SkipSpacesAndComments();
            if (_position == _text.Length) break;
            if (!Consume(',') && !AtLineBreak)
            {
                Fail(Code.UnexpectedToken, _position);
                return;
            }
            SkipWhitespace();
            if (_position < _text.Length && _text[_position] == ',')
            {
                Fail(Code.UnexpectedToken, _position);
                return;
            }
        }
    }

    private bool IsCatalogHeader() => StartsWord(":Catalog");

    // `:Catalog(@TypeName)`.
    private bool ReadCatalogHeader(out string typeName)
    {
        typeName = "";
        _position += 8;
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidSchemaHeader, _position) is not null;
        SkipWhitespace();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open) is not null;
        var argument = _position;
        if (!Consume('@') || ReadName() is not { } name || !GesText.IsTypeName(name)) return Fail(Code.InvalidSchemaHeader, argument) is not null;
        typeName = name;
        SkipWhitespace();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open) is not null;
        return Consume(')') || Fail(Code.InvalidSchemaHeader, _position) is not null;
    }

    // Whether the text continues with the item word `import` or `module` and no `:` follows it, which would make the
    // word a key.
    private bool IsItemWord(string word)
    {
        if (!_text.AsSpan(_position).StartsWith(word.AsSpan(), StringComparison.Ordinal)) return false;
        var position = _position + word.Length;
        if (position < _text.Length && IsCatalogKeyCharacter(_text[position])) return false;
        var saved = _position;
        _position = position;
        SkipSpacesAndComments();
        var isWord = _position == _text.Length || _text[_position] != ':';
        _position = saved;
        return isWord;
    }

    // `module name { entries }`; module blocks do not nest and hold no imports.
    private bool ReadModule(List<GesCatalogItem> items)
    {
        _position += 6;
        SkipSpacesAndComments();
        var nameStart = _position;
        var nameEnd = CatalogNameEnd(_position);
        if (nameEnd < 0) return Fail(Code.InvalidKey, nameStart) is not null;
        var name = _text.Substring(nameStart, nameEnd - nameStart);
        _position = nameEnd;
        SkipSpacesAndComments();
        var open = _position;
        if (!Consume('{')) return Fail(Code.UnexpectedToken, _position) is not null;
        SkipWhitespace();
        if (Consume('}')) return true;
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open) is not null;
        while (true)
        {
            var entryStart = _position;
            if (IsItemWord("module")) return Fail(Code.UnexpectedModule, entryStart, entryStart + 6) is not null;
            if (IsItemWord("import")) return Fail(Code.UnexpectedToken, entryStart, entryStart + 6) is not null;
            if (IsSchemaHeader() || IsCatalogHeader()) return Fail(Code.MisplacedSchemaHeader, entryStart) is not null;
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, entryStart) is not null;
            _items++;
            if (!ReadCatalogEntry(name, items)) return false;
            var separator = Separator('}', open);
            if (separator != 0) return separator > 0;
        }
    }

    // `key: value` or `key: import 'path'`.
    private bool ReadCatalogEntry(string module, List<GesCatalogItem> items)
    {
        var keyStart = _position;
        var keyEnd = keyStart;
        while (keyEnd < _text.Length && IsCatalogKeyCharacter(_text[keyEnd])) keyEnd++;
        if (keyEnd == keyStart) return Fail(Code.InvalidKey, keyStart) is not null;
        var key = _text.Substring(keyStart, keyEnd - keyStart);
        _position = keyEnd;
        SkipSpacesAndComments();
        if (!Consume(':')) return Fail(Code.MissingColon, _position) is not null;
        SkipSpacesAndComments();
        if (_text.AsSpan(_position).StartsWith("import".AsSpan(), StringComparison.Ordinal)
            && (_position + 6 == _text.Length || !IsCatalogKeyCharacter(_text[_position + 6])))
        {
            _position += 6;
            SkipSpacesAndComments();
            if (ReadImportPath(out var pathOffset) is not { } path) return false;
            items.Add(new GesCatalogItem(GesCatalogItemKind.EntryImport, module, key, keyStart, null, path, pathOffset));
            return true;
        }
        var valueStart = _position;
        if (ReadEntryValue(0) is not { } value) return false;
        if (value.Kind != GesDocumentNodeKind.Map) return Fail(Code.UnexpectedToken, valueStart) is not null;
        items.Add(new GesCatalogItem(GesCatalogItemKind.Entry, module, key, keyStart, value, "", -1));
        return true;
    }

    // A non-empty quoted import path on the import's line.
    private string? ReadImportPath(out int pathOffset)
    {
        pathOffset = _position;
        if (_position == _text.Length || _text[_position] is not ('\'' or '"'))
        {
            Fail(Code.UnexpectedToken, _position);
            return null;
        }
        var path = GesTextLiteralReader.Read(_text, ref _position);
        if (path is null)
        {
            Fail(Code.UnclosedText, pathOffset);
            return null;
        }
        if (path.Length == 0)
        {
            Fail(Code.InvalidLiteral, pathOffset, _position);
            return null;
        }
        return path;
    }

    private static bool IsCatalogKeyCharacter(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-';

    // The end of a catalog name, keys joined by `.`, from `start`; -1 when no key starts there or a `.` ends it.
    private int CatalogNameEnd(int start)
    {
        var position = start;
        while (true)
        {
            var keyStart = position;
            while (position < _text.Length && IsCatalogKeyCharacter(_text[position])) position++;
            if (position == keyStart) return -1;
            if (position == _text.Length || _text[position] != '.') return position;
            position++;
        }
    }

    // Reads a handle, or with references a reference or pinned handle, at the `@` at `start`. A reference's identity is
    // its name. Returns the end, or -1 when no well-formed handle or reference that a delimiter follows is there.
    private int ScanHandle(int start, out bool reference, out string identity, out bool versioned, out int pin)
    {
        reference = false;
        if (!_references)
        {
            pin = -1;
            return GesHandleLiteral.Read(_text, start, out identity, out versioned);
        }
        var nameEnd = CatalogNameEnd(start + 1);
        if (nameEnd > 0 && nameEnd + 1 < _text.Length && _text[nameEnd] == ':' && GesHandleLiteral.IsIdStart(_text[nameEnd + 1])
            && GesText.IsTypeName(_text.Substring(start + 1, nameEnd - start - 1)))
            return GesHandleLiteral.Read(_text, start, true, out identity, out versioned, out pin);
        identity = "";
        versioned = false;
        pin = -1;
        if (nameEnd < 0) return -1;
        var end = nameEnd;
        if (end < _text.Length && _text[end] == '~') pin = end++;
        if (end < _text.Length && !GesHandleLiteral.IsDelimiter(_text, end)) return -1;
        reference = true;
        identity = _text.Substring(start + 1, nameEnd - start - 1);
        return end;
    }

    private GesDocumentNode? ReadDocument(out GesDocumentSchemaSyntax schema, out string schemaName)
    {
        schema = GesDocumentSchemaSyntax.None;
        schemaName = "";
        if (_text.Length > 0 && _text[0] == '\uFEFF') _position = 1;
        SkipWhitespace();
        if (IsSchemaHeader())
        {
            if (!ReadSchemaHeader(out schema, out schemaName)) return null;
            SkipSpacesAndComments();
            if (_position < _text.Length && !AtLineBreak) return Fail(Code.UnexpectedToken, _position);
            SkipWhitespace();
        }
        var record = schema == GesDocumentSchemaSyntax.Type;
        var keys = new List<string>();
        var keyOffsets = new List<int>();
        var handleKeys = new List<bool>();
        var values = new List<GesDocumentNode>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (_position < _text.Length)
        {
            var entryStart = _position;
            if (IsSchemaHeader()) return Fail(Code.MisplacedSchemaHeader, entryStart);
            // Catalog headers and module blocks belong only to catalog files.
            if (IsCatalogHeader()) return Fail(Code.InvalidSchemaHeader, entryStart);
            if (IsItemWord("module")) return Fail(Code.UnexpectedModule, entryStart, entryStart + 6);
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, entryStart);
            _items++;
            if (record && _text[entryStart] == '@') return Fail(Code.InvalidKey, entryStart);
            var key = ReadEntryKey(out var isHandle, out var identity);
            if (_error is not null) return null;
            if (key is null) return Fail(_text[entryStart] is '\'' or '"' ? Code.UnclosedText : Code.InvalidKey, entryStart);
            if (record && !GesText.IsFieldName(key)) return Fail(Code.InvalidKey, entryStart);
            if (!seen.Add(identity)) return Fail(Code.DuplicateKey, entryStart);
            SkipSpacesAndComments();
            if (!Consume(':')) return Fail(Code.MissingColon, _position);
            if (ReadEntryValue(0) is not { } value) return null;
            keys.Add(key);
            keyOffsets.Add(entryStart);
            handleKeys.Add(isHandle);
            values.Add(value);
            SkipSpacesAndComments();
            if (_position == _text.Length) break;
            if (!Consume(',') && !AtLineBreak) return Fail(Code.UnexpectedToken, _position);
            SkipWhitespace();
            if (_position < _text.Length && _text[_position] == ',') return Fail(Code.UnexpectedToken, _position);
        }
        return GesDocumentNode.Map(keys.ToArray(), values.ToArray(), keyOffsets.ToArray(), HandleKeyFlags(handleKeys));
    }

    private static bool[]? HandleKeyFlags(List<bool> flags) => flags.Contains(true) ? flags.ToArray() : null;

    // A Map key: a Text key, or a handle without a version or ref, or a reference without a pin. `identity` compares
    // keys; a handle's never equals a Text's or a reference's. A malformed handle records its error.
    private string? ReadEntryKey(out bool isHandle, out string identity)
    {
        SkipWhitespace();
        isHandle = _position < _text.Length && _text[_position] == '@';
        if (!isHandle)
        {
            var key = ReadKey();
            identity = "t" + key;
            return key;
        }
        var start = _position;
        var end = ScanHandle(start, out var reference, out identity, out var versioned, out var pin);
        if (end < 0 || versioned || pin >= 0)
        {
            Fail(Code.InvalidHandle, start, end < 0 ? start + 1 : end);
            return null;
        }
        identity = (reference ? "r" : "h") + identity;
        _position = end;
        return _text.Substring(start, end - start);
    }

    private GesDocumentNode? ReadHandle()
    {
        var start = _position;
        var end = ScanHandle(start, out var reference, out var identity, out _, out var pin);
        if (end < 0) return Fail(Code.InvalidHandle, start, start + 1);
        _position = end;
        if (reference) return GesDocumentNode.Reference(identity, pin >= 0);
        return GesDocumentNode.Handle(_text.Substring(start, (pin >= 0 ? pin : end) - start), pin >= 0);
    }

    private bool IsSchemaHeader() => StartsWord(":Schema");

    private bool ReadSchemaHeader(out GesDocumentSchemaSyntax schema, out string name)
    {
        schema = GesDocumentSchemaSyntax.None;
        name = "";
        _position += 7;
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidSchemaHeader, _position) is not null;
        SkipWhitespace();
        var start = _position;
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open) is not null;
        if (Consume(':'))
        {
            var type = ReadName();
            if (type is not null && GesLiteralForms.IsRecordTypeName(type))
            {
                schema = GesDocumentSchemaSyntax.Type;
                name = type;
            }
        }
        else if (_text[_position] is '\'' or '"')
        {
            var path = GesTextLiteralReader.Read(_text, ref _position);
            if (path is null) return Fail(Code.UnclosedText, start) is not null;
            if (path.Length > 0)
            {
                schema = GesDocumentSchemaSyntax.File;
                name = path;
            }
        }
        if (schema == GesDocumentSchemaSyntax.None) return Fail(Code.InvalidSchemaHeader, start) is not null;
        switch (Separator(')', open))
        {
            case 1: return true;
            case 0: Fail(Code.InvalidSchemaHeader, _position); return false;
            default: return false;
        }
    }

    // The value after a Map or root key: key-only entries are true; otherwise the value starts on the key's line.
    private GesDocumentNode? ReadEntryValue(int depth)
    {
        SkipSpacesAndComments();
        if (_position == _text.Length || AtLineBreak || _text[_position] is ',' or ']')
        {
            var implied = GesDocumentNode.BooleanValue(true);
            implied.Start = _position;
            return implied;
        }

        return ReadValue(depth);
    }

    private GesDocumentNode? ReadValue(int depth)
    {
        SkipWhitespace();
        var start = _position;
        var value = ReadValueAtPosition(depth);
        if (value is not null) value.Start = start;
        return value;
    }

    private GesDocumentNode? ReadValueAtPosition(int depth)
    {
        if (_position == _text.Length) return Fail(Code.UnexpectedToken, _position);
        var valueStart = _position;
        if (_text[_position] is '\'' or '"')
        {
            var text = GesTextLiteralReader.Read(_text, ref _position);
            return text is null ? Fail(Code.UnclosedText, valueStart) : GesDocumentNode.Text(text);
        }
        if (_text[_position] == '@') return ReadHandle();
        if (_text[_position] == ':')
        {
            if (StartsWord(":Vector")) return ReadSpatial(depth, point: false);
            if (StartsWord(":Point")) return ReadSpatial(depth, point: true);
        }
        if (_text[_position] == ':' && !_text.AsSpan(_position).StartsWith(":Dice[".AsSpan(), StringComparison.Ordinal) &&
            !_text.AsSpan(_position).StartsWith(":Bytes[".AsSpan(), StringComparison.Ordinal))
        {
            var saved = _position;
            var typed = ReadTyped(depth);
            if (typed is not null || _error is not null) return typed;
            _position = saved;
        }
        var dice = _text[_position] == ':' && _text.AsSpan(_position).StartsWith(":Dice".AsSpan(), StringComparison.Ordinal);
        var bytes = _text[_position] == ':' && _text.AsSpan(_position).StartsWith(":Bytes".AsSpan(), StringComparison.Ordinal);
        if (dice || bytes)
        {
            _position += dice ? 5 : 6;
            SkipWhitespace();
            if (_position == _text.Length || _text[_position] != '[') return Fail(Code.InvalidLiteral, valueStart);
        }
        if (_text[_position] == '[')
        {
            if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", MaximumDepth, _position);
            return dice ? ReadDice(valueStart) : bytes ? ReadBytes(valueStart) : ReadCollection(depth + 1);
        }

        var start = _position;
        _position = GesNumberLiteral.TokenEnd(_text, _position, brackets: true);
        EndTokenAtComment(start);
        if (_position == start) return Fail(Code.UnexpectedToken, _position);
        var token = _text.AsSpan(start, _position - start);
        if (token.SequenceEqual("true".AsSpan())) return GesDocumentNode.BooleanValue(true);
        if (token.SequenceEqual("false".AsSpan())) return GesDocumentNode.BooleanValue(false);
        if (token.SequenceEqual("nothing".AsSpan())) return GesDocumentNode.NothingValue();
        if (token[0] == '#')
        {
            var name = _text.Substring(start + 1, _position - start - 1);
            return GesText.IsTagName(name) ? GesDocumentNode.Tag(name) : Fail(Code.InvalidLiteral, start, _position);
        }
        var number = GesNumberLiteral.Read(_text, start, _position - start, percentage: token[token.Length - 1] == '%', allowGrouping: false);
        if (!number.IsValid) return TokenFailure(start, _position);
        // A number token that constructs nothing, such as `NaN` or `Infinity%`, is no value in a document.
        return number.Kind == GesNumberLiteralKind.Nothing ? Fail(Code.InvalidLiteral, start, _position) : GesDocumentNode.NumberValue(number);
    }

    private GesDocumentNode? ReadTyped(int depth)
    {
        var formStart = _position;
        if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", MaximumDepth, formStart);
        _position++;
        var name = ReadName();
        if (name is null || !GesText.IsTypeName(name)) return Fail(Code.InvalidLiteral, formStart);
        // Built-in names are case-sensitive; documents know no Program Record constructors.
        if (!GesLiteralForms.IsBuiltinName(name)) return Fail(Code.UnknownType, formStart);
        var builtin = name.ToLowerInvariant();
        SkipWhitespace();
        var open = _position;
        // `:Dice [...]` with whitespace is the Dice result literal, read after this form fails.
        if (!Consume('(')) return builtin is "dice" or "bytes" ? null : Fail(Code.InvalidLiteral, formStart);
        SkipWhitespace();
        if (builtin is "handler" or "message" && _position < _text.Length && GesText.IsAsciiUpper(_text[_position]))
            return ReadMessageForm(depth + 1, builtin == "handler", formStart, open);
        var labels = new List<string>();
        var values = new List<GesDocumentNode>();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
                var saved = _position;
                var label = ReadKey();
                SkipSpacesAndComments();
                if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                else if (!GesText.IsFieldName(label)) return Fail(Code.InvalidKey, saved);
                else if (labels.Contains(label)) return Fail(Code.DuplicateKey, saved);
                SkipWhitespace();
                if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
                GesDocumentNode? value;
                if (builtin == "series" && (label == "kind" || label == "_" && values.Count == 0) && _text[_position] is >= 'a' and <= 'z')
                {
                    var valueStart = _position;
                    var kind = ReadName();
                    if (kind is "fibonacci" or "factorial") value = GesDocumentNode.Text(kind);
                    else { _position = valueStart; value = ReadValue(depth + 1); }
                }
                else value = ReadValue(depth + 1);
                if (value is null) return null;
                labels.Add(label);
                values.Add(value);
                var separator = Separator(')', open);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
            if (_position == 0 || _text[_position - 1] != ')') return null;
        }
        if (GesLiteralForms.Positions(builtin, labels) is null) return Fail(Code.InvalidLiteral, formStart);
        return GesDocumentNode.Form(builtin, labels.ToArray(), values.ToArray(), formStart);
    }

    private GesDocumentNode? ReadMessageForm(int depth, bool handler, int formStart, int outerOpen)
    {
        var name = ReadName();
        if (name is null || !GesText.IsMessageName(name)) return Fail(Code.InvalidLiteral, formStart);
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidLiteral, formStart);
        SkipWhitespace();
        var labels = new List<string>();
        var values = new List<GesDocumentNode>();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
                if (handler)
                {
                    var labelStart = _position;
                    var label = ReadName();
                    if (label is null) return Fail(Code.InvalidLiteral, formStart);
                    if (label != "_" && !GesText.IsFieldName(label)) return Fail(Code.InvalidKey, labelStart);
                    if (label != "_" && labels.Contains(label)) return Fail(Code.DuplicateKey, labelStart);
                    labels.Add(label);
                }
                else
                {
                    var saved = _position;
                    var label = ReadKey();
                    SkipSpacesAndComments();
                    if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                    else if (!GesText.IsFieldName(label)) return Fail(Code.InvalidKey, saved);
                    else if (labels.Contains(label)) return Fail(Code.DuplicateKey, saved);
                    SkipWhitespace();
                    if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
                    if (ReadValue(depth) is not { } value) return null;
                    labels.Add(label);
                    values.Add(value);
                }
                var separator = Separator(')', open);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
        }
        SkipWhitespace();
        var result = handler ? GesDocumentNode.Handler(name, labels.ToArray()) : GesDocumentNode.Message(name, labels.ToArray(), values.ToArray(), formStart);
        if (!handler && StartsWord("with"))
        {
            _position += 4;
            var tags = new List<GesDocumentNode>();
            while (true)
            {
                if (ReadValue(depth) is not { } tag) return null;
                tags.Add(tag);
                var separator = Separator(')', outerOpen);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
            return GesDocumentNode.Form("message", ["_", "tags"], [result, GesDocumentNode.List(tags.ToArray())], formStart);
        }
        if (Consume(')')) return result;
        return _position == _text.Length ? Fail(Code.UnclosedBracket, outerOpen) : Fail(Code.UnexpectedToken, _position);
    }

    // Callers skip any permitted whitespace first; a type name follows its `:` directly, like the source type token.
    private string? ReadName()
    {
        var start = _position;
        while (_position < _text.Length && IsNameCharacter(_text[_position])) _position++;
        return _position == start ? null : _text.Substring(start, _position - start);
    }

    // Whether the text continues with `word` and no further name character.
    private bool StartsWord(string word)
        => _text.AsSpan(_position).StartsWith(word.AsSpan(), StringComparison.Ordinal)
            && (_position + word.Length == _text.Length || !IsNameCharacter(_text[_position + word.Length]));

    private static bool IsNameCharacter(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private GesDocumentNode? ReadSpatial(int depth, bool point)
    {
        var formStart = _position;
        _position += point ? 6 : 7;
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidLiteral, formStart);
        if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", MaximumDepth, formStart);
        var x = 0d;
        var y = 0d;
        var z = 0d;
        GesQuantityUnit? unit = null;
        bool? labeled = null;
        var previousIndex = -1;
        SkipWhitespace();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (Consume(')')) return GesDocumentNode.Spatial(point, x, y, z, GesQuantityUnit.UnitNone);
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
            _items++;
            var hasLabel = _text[_position] is 'x' or 'y' or 'z';
            if (labeled.HasValue && labeled != hasLabel) return Fail(Code.InvalidLiteral, formStart);
            labeled = hasLabel;
            var index = previousIndex + 1;
            var labelStart = _position;
            if (hasLabel)
            {
                index = _text[_position++] - 'x';
                SkipSpacesAndComments();
                if (!Consume(':')) return Fail(Code.InvalidLiteral, formStart);
                SkipWhitespace();
                if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
            }
            if (hasLabel && index == previousIndex) return Fail(Code.DuplicateKey, labelStart);
            if (index > 2 || index <= previousIndex || previousIndex >= 0 && index != previousIndex + 1) return Fail(Code.InvalidLiteral, formStart);
            previousIndex = index;
            var start = _position;
            _position = GesNumberLiteral.TokenEnd(_text, _position, brackets: false);
            EndTokenAtComment(start);
            if (_position == start) return Fail(Code.UnexpectedToken, _position);
            var component = GesNumberLiteral.Read(_text, start, _position - start, percentage: _text[_position - 1] == '%', allowGrouping: false);
            if (!component.IsValid) return TokenFailure(start, _position);
            if (component.Kind is not (GesNumberLiteralKind.Integer or GesNumberLiteralKind.Float or GesNumberLiteralKind.Percentage))
                return Fail(Code.InvalidLiteral, formStart);
            if (unit.HasValue && unit != component.Unit) return Fail(Code.InvalidLiteral, formStart);
            unit = component.Unit;
            var number = component.Kind == GesNumberLiteralKind.Integer ? component.IntegerValue : component.FloatValue;
            if (double.IsNaN(number)) return Fail(Code.InvalidLiteral, formStart);
            switch (index)
            {
                case 0: x = number; break;
                case 1: y = number; break;
                case 2: z = number; break;
            }
            var separator = Separator(')', open);
            if (separator < 0) return null;
            if (separator > 0) return GesDocumentNode.Spatial(point, x, y, z, unit.Value);
        }
        return null;
    }

    private GesDocumentNode? ReadDice(int formStart)
    {
        var open = _position;
        _position++;
        SkipWhitespace();
        if (Consume(']')) return GesDocumentNode.Dice([]);
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        var rolls = new List<int>();
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
            _items++;
            if (_text[_position] is < '0' or > '9') return Fail(Code.InvalidLiteral, formStart);
            if (ReadValue(0) is not { } roll) return null;
            if (roll.Kind != GesDocumentNodeKind.Number || roll.Number.Kind != GesNumberLiteralKind.Integer || roll.Number.Unit != GesQuantityUnit.UnitNone
                || roll.Number.IntegerValue is <= 0 or > int.MaxValue)
                return Fail(Code.InvalidLiteral, formStart);
            rolls.Add((int)roll.Number.IntegerValue);
            var separator = Separator(']', open);
            if (separator < 0) return null;
            if (separator > 0) return GesDocumentNode.Dice(rolls.ToArray());
        }
        return null;
    }

    // `:Bytes[...]` holds integer literals from 0 to 255.
    private GesDocumentNode? ReadBytes(int formStart)
    {
        var open = _position;
        _position++;
        SkipWhitespace();
        if (Consume(']')) return GesDocumentNode.BytesValue([]);
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        var bytes = new List<byte>();
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
            _items++;
            if (_text[_position] is < '0' or > '9') return Fail(Code.InvalidLiteral, formStart);
            if (ReadValue(0) is not { } item) return null;
            if (item.Kind != GesDocumentNodeKind.Number || item.Number.Kind != GesNumberLiteralKind.Integer || item.Number.Unit != GesQuantityUnit.UnitNone
                || item.Number.IntegerValue is < 0 or > 255)
                return Fail(Code.InvalidLiteral, formStart);
            bytes.Add((byte)item.Number.IntegerValue);
            var separator = Separator(']', open);
            if (separator < 0) return null;
            if (separator > 0) return GesDocumentNode.BytesValue(bytes.ToArray());
        }
        return null;
    }

    private GesDocumentNode? ReadCollection(int depth)
    {
        var open = _position;
        _position++;
        SkipWhitespace();
        if (Consume(']')) return GesDocumentNode.List([]);
        var contentStart = _position;
        if (Consume(':'))
        {
            SkipWhitespace();
            if (Consume(']')) return GesDocumentNode.Map([], []);
            // A leading typed literal, such as [:Dice[]], is a List item.
            _position = contentStart;
        }
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);

        var saved = _position;
        bool isMap;
        if (_text[_position] == '@')
        {
            var end = ScanHandle(_position, out _, out _, out _, out _);
            if (end >= 0) _position = end;
            SkipSpacesAndComments();
            isMap = end >= 0 && Consume(':');
        }
        else
        {
            var key = ReadKey();
            SkipSpacesAndComments();
            isMap = key is not null && Consume(':');
        }
        _position = saved;
        var keys = isMap ? new List<string>() : null;
        var keyOffsets = isMap ? new List<int>() : null;
        var handleKeys = isMap ? new List<bool>() : null;
        var seen = isMap ? new HashSet<string>(StringComparer.Ordinal) : null;
        var values = new List<GesDocumentNode>();
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
            _items++;
            if (isMap)
            {
                SkipWhitespace();
                var keyStart = _position;
                var key = ReadEntryKey(out var isHandle, out var identity);
                if (_error is not null) return null;
                if (key is null) return Fail(keyStart < _text.Length && _text[keyStart] is '\'' or '"' ? Code.UnclosedText : Code.InvalidKey, keyStart);
                if (!seen!.Add(identity)) return Fail(Code.DuplicateKey, keyStart);
                SkipSpacesAndComments();
                if (!Consume(':')) return Fail(Code.MissingColon, _position);
                if (ReadEntryValue(depth) is not { } entry) return null;
                keys!.Add(key);
                keyOffsets!.Add(keyStart);
                handleKeys!.Add(isHandle);
                values.Add(entry);
            }
            else
            {
                if (ReadValue(depth) is not { } item) return null;
                values.Add(item);
            }
            var separator = Separator(']', open);
            if (separator < 0) return null;
            if (separator > 0) return isMap ? GesDocumentNode.Map(keys!.ToArray(), values.ToArray(), keyOffsets!.ToArray(), HandleKeyFlags(handleKeys!)) : GesDocumentNode.List(values.ToArray());
        }
        return null;
    }

    private string? ReadKey()
    {
        SkipWhitespace();
        if (_position < _text.Length && _text[_position] is '\'' or '"') return GesTextLiteralReader.Read(_text, ref _position);
        var start = _position;
        if (_position == _text.Length || _text[_position] is < 'a' or > 'z') return null;
        _position++;
        while (_position < _text.Length && _text[_position] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9') _position++;
        return _text.Substring(start, _position - start);
    }

    // Ends one container entry: 1 when the container closes, 0 when another entry follows, -1 on failure. Line
    // breaks also end entries, and one trailing comma is accepted.
    private int Separator(char close, int open)
    {
        SkipSpacesAndComments();
        if (Consume(close)) return 1;
        if (_position == _text.Length) { Fail(Code.UnclosedBracket, open); return -1; }
        if (!Consume(',') && !AtLineBreak) { Fail(Code.UnexpectedToken, _position); return -1; }
        SkipWhitespace();
        if (_position == _text.Length) { Fail(Code.UnclosedBracket, open); return -1; }
        if (Consume(close)) return 1;
        if (_text[_position] == ',') { Fail(Code.UnexpectedToken, _position); return -1; }
        return 0;
    }

    // Classifies an unreadable value token.
    private GesDocumentNode? TokenFailure(int start, int end)
    {
        var first = _text[start];
        if (first is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_') return Fail(Code.UnquotedText, start, end);
        return Fail(HasUnknownUnit(start, end) ? Code.UnknownUnit : Code.InvalidLiteral, start, end);
    }

    // A numeric prefix followed by an ASCII letter, `°` or `/` is a number with an unknown unit suffix.
    private bool HasUnknownUnit(int start, int end)
    {
        var index = start;
        if (index < end && _text[index] is '+' or '-') index++;
        // An invalid hexadecimal number has no unit suffix.
        if (end - index >= 2 && _text[index] == '0' && _text[index + 1] == 'x') return false;
        var digits = SkipDigits(ref index, end);
        if (index < end && _text[index] == '.')
        {
            index++;
            digits |= SkipDigits(ref index, end);
        }
        if (!digits) return false;
        if (index < end && _text[index] is 'e' or 'E')
        {
            var exponent = index + 1;
            if (exponent < end && _text[exponent] is '+' or '-') exponent++;
            if (SkipDigits(ref exponent, end)) index = exponent;
        }
        return index < end && _text[index] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '°' or '/';
    }

    private bool SkipDigits(ref int index, int end)
    {
        if (index >= end || _text[index] is < '0' or > '9') return false;
        index++;
        while (index < end && _text[index] is >= '0' and <= '9' or '_') index++;
        return true;
    }

    // Records the first error. Without an explicit end, the range covers the `:Name` of a form, or the scalar at the
    // position, or nothing at a line break and at the end.
    private GesDocumentNode? Fail(string code, int position, int end = -1)
    {
        _error ??= Error(code, position, end);
        return null;
    }

    private GesDocumentDiagnostic Error(string code, int position, int end = -1)
    {
        if (end < 0)
        {
            end = position;
            if (position < _text.Length && _text[position] == ':' && position + 1 < _text.Length && GesText.IsAsciiLetter(_text[position + 1]))
            {
                end = position + 1;
                while (end < _text.Length && IsNameCharacter(_text[end])) end++;
            }
            else if (position < _text.Length && _text[position] is not ('\n' or '\r'))
                end = position + GesText.ScalarUtf16LengthAt(_text, position);
        }
        return new GesDocumentDiagnostic(code, position, end);
    }

    private GesDocumentNode? Limit(string name, int value, int position)
    {
        _error ??= new GesDocumentDiagnostic(Code.LimitExceeded, position, position, name, value);
        return null;
    }

    private bool Consume(char value)
    {
        if (_position == _text.Length || _text[_position] != value) return false;
        _position++;
        return true;
    }

    // A comment ends an unquoted token.
    private void EndTokenAtComment(int start)
    {
        var comment = _text.IndexOf("//", start, _position - start, StringComparison.Ordinal);
        if (comment >= 0) _position = comment;
    }

    private bool AtComment => _position + 1 < _text.Length && _text[_position] == '/' && _text[_position + 1] == '/';

    private bool AtLineBreak => _position < _text.Length && _text[_position] is '\n' or '\r';

    private void SkipWhitespace()
    {
        SkipSpacesAndComments();
        while (AtLineBreak)
        {
            _position++;
            SkipSpacesAndComments();
        }
    }

    // Trivia between a key or label and its colon or value: documents keep both on one line.
    private void SkipSpacesAndComments()
    {
        while (_position < _text.Length)
        {
            if (_text[_position] is ' ' or '\t') _position++;
            else if (AtComment) while (_position < _text.Length && _text[_position] is not ('\n' or '\r')) _position++;
            else return;
        }
    }

    private static bool IsWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';
}
