// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Syntax;

internal enum GesDocumentNodeKind : byte
{
    Text,
    Boolean,
    Nothing,
    Tag,
    Number,
    List,
    Map,
    Dice,
    Bytes,
    Vector,
    Point,
    // An explicit form such as `:Range(1, 5)`, constructed by Runtime.
    Form,
    // The message of `:Message(Done(v: 1))` before its tags.
    Message,
    Handler,
    // A handle in its text form, built by Runtime.
    Handle,
    // A store reference such as `@hull.light`, which a store replaces by a handle (specs/Documents.md#references).
    Reference
}

/// <summary>
/// A value of a read document. Scalars carry their recognized value; Runtime builds the values of forms, messages
/// and handlers.
/// </summary>
internal sealed class GesDocumentNode
{
    private static readonly GesDocumentNode[] NoChildren = [];

    private GesDocumentNode(GesDocumentNodeKind kind) => Kind = kind;

    internal GesDocumentNodeKind Kind { get; }

    // The Text value, the tag name, the lowercase form type, the message name, the handle text or the reference name.
    internal string Name { get; private init; } = "";

    internal bool Boolean { get; private init; }

    internal GesNumberLiteral Number { get; private init; }

    // Map keys, or the argument labels of a form, message or handler; `_` for an unlabeled one.
    internal string[] Labels { get; private init; } = [];

    internal GesDocumentNode[] Children { get; private init; } = NoChildren;

    internal int[] Rolls { get; private init; } = [];

    internal byte[] Bytes { get; private init; } = [];

    internal double X { get; private init; }

    internal double Y { get; private init; }

    internal double Z { get; private init; }

    internal GesQuantityUnit Unit { get; private init; }

    // The UTF-16 offset where this value starts, and of each key of a Map; schema checks report them.
    internal int Start { get; set; }

    internal int[] KeyOffsets { get; private init; } = [];

    // Whether each Map key is a handle in its text form rather than a Text; empty when no key is.
    internal bool[] HandleKeys { get; private init; } = [];

    // The UTF-16 offset of a form's `:`, where construction errors point.
    internal int Offset { get; private init; }

    // Whether a reference or complete handle of a store ends with `~`, which pins it to the named entry's hash.
    internal bool Pinned { get; private init; }

    internal static GesDocumentNode Text(string value) => new(GesDocumentNodeKind.Text) { Name = value };

    internal static GesDocumentNode BooleanValue(bool value) => new(GesDocumentNodeKind.Boolean) { Boolean = value };

    internal static GesDocumentNode NothingValue() => new(GesDocumentNodeKind.Nothing);

    internal static GesDocumentNode Tag(string name) => new(GesDocumentNodeKind.Tag) { Name = name };

    internal static GesDocumentNode NumberValue(GesNumberLiteral number) => new(GesDocumentNodeKind.Number) { Number = number };

    internal static GesDocumentNode List(GesDocumentNode[] items) => new(GesDocumentNodeKind.List) { Children = items };

    internal static GesDocumentNode Map(string[] keys, GesDocumentNode[] values, int[]? keyOffsets = null, bool[]? handleKeys = null)
        => new(GesDocumentNodeKind.Map) { KeyOffsets = keyOffsets ?? [], Labels = keys, Children = values, HandleKeys = handleKeys ?? [] };

    internal static GesDocumentNode Handle(string text, bool pinned = false) => new(GesDocumentNodeKind.Handle) { Name = text, Pinned = pinned };

    internal static GesDocumentNode Reference(string name, bool pinned) => new(GesDocumentNodeKind.Reference) { Name = name, Pinned = pinned };

    // Whether a Map key is a handle or a reference; its label holds the text with the `@`.
    internal bool IsHandleKey(int index) => HandleKeys.Length > 0 && HandleKeys[index];

    // Whether a Map key is a reference: a handle key whose label has no type, as `@scout` has none.
    internal bool IsReferenceKey(int index) => IsHandleKey(index) && Labels[index].IndexOf(':') < 0;

    // A copy with other labels and children, which a store uses to replace references.
    internal GesDocumentNode With(string[] labels, GesDocumentNode[] children) => new(Kind)
    {
        Name = Name,
        Boolean = Boolean,
        Number = Number,
        Labels = labels,
        Children = children,
        Rolls = Rolls,
        Bytes = Bytes,
        X = X,
        Y = Y,
        Z = Z,
        Unit = Unit,
        Start = Start,
        KeyOffsets = KeyOffsets,
        HandleKeys = HandleKeys,
        Offset = Offset,
        Pinned = Pinned
    };

    internal static GesDocumentNode Dice(int[] rolls) => new(GesDocumentNodeKind.Dice) { Rolls = rolls };

    internal static GesDocumentNode BytesValue(byte[] bytes) => new(GesDocumentNodeKind.Bytes) { Bytes = bytes };

    internal static GesDocumentNode Spatial(bool point, double x, double y, double z, GesQuantityUnit unit)
        => new(point ? GesDocumentNodeKind.Point : GesDocumentNodeKind.Vector) { X = x, Y = y, Z = z, Unit = unit };

    internal static GesDocumentNode Form(string type, string[] labels, GesDocumentNode[] arguments, int offset)
        => new(GesDocumentNodeKind.Form) { Name = type, Labels = labels, Children = arguments, Offset = offset };

    internal static GesDocumentNode Message(string name, string[] labels, GesDocumentNode[] arguments, int offset)
        => new(GesDocumentNodeKind.Message) { Name = name, Labels = labels, Children = arguments, Offset = offset };

    internal static GesDocumentNode Handler(string name, string[] labels) => new(GesDocumentNodeKind.Handler) { Name = name, Labels = labels };
}

internal enum GesDocumentSchemaSyntax : byte
{
    None,
    // `:Schema(:DriveUnit)`: the root is a Record of that type.
    Type,
    // `:Schema('DriveUnit.gess')`: the root is a Map carrying the path.
    File
}

/// <summary>
/// The error of an unreadable document: its stable code and its UTF-16 range, which starts at the position the code
/// reports. A limit error also names the limit and its value.
/// </summary>
internal sealed record GesDocumentDiagnostic(string Code, int Start, int End, string? LimitName = null, int LimitValue = 0)
{
    internal const string InvalidKey = "document.invalidKey";
    internal const string MissingColon = "document.missingColon";
    internal const string DuplicateKey = "document.duplicateKey";
    internal const string UnquotedText = "document.unquotedText";
    internal const string UnknownUnit = "document.unknownUnit";
    internal const string InvalidLiteral = "document.invalidLiteral";
    internal const string UnknownType = "document.unknownType";
    internal const string UnexpectedToken = "document.unexpectedToken";
    internal const string UnclosedBracket = "document.unclosedBracket";
    internal const string UnclosedText = "document.unclosedText";
    internal const string InvalidSchemaHeader = "document.invalidSchemaHeader";
    internal const string MisplacedSchemaHeader = "document.misplacedSchemaHeader";
    internal const string LimitExceeded = "document.limitExceeded";
    internal const string InvalidHandle = "document.invalidHandle";
    internal const string UnexpectedModule = "document.unexpectedModule";

    // The English explanation of an error code for highlighters and tools; it is not part of the contract.
    internal static string Explain(string code, string? limitName, int limitValue) => code switch
    {
        InvalidKey => "An entry must start with a key; a record field must be a field name.",
        MissingColon => "A key must be followed by ':' on its line.",
        DuplicateKey => "This key is already used in its container.",
        UnquotedText => "Text values must be quoted.",
        UnknownUnit => "The number has an unknown unit.",
        InvalidLiteral => "This is not a valid literal.",
        UnknownType => "Documents know only the built-in types.",
        UnexpectedToken => "An entry, separator or closing bracket is expected here.",
        UnclosedBracket => "This bracket is never closed.",
        UnclosedText => "This Text is never closed.",
        InvalidSchemaHeader => "A schema header names one record type or a schema file; a store header one handle type.",
        MisplacedSchemaHeader => "The schema header must come before the first entry.",
        InvalidHandle => "This is not a handle, or a handle key has a version.",
        UnexpectedModule => "Module blocks belong only at the root of a store file.",
        LimitExceeded => $"The document exceeds {limitName ?? "a limit"} ({limitValue}).",
        _ => "This is not a valid document."
    };
}

/// <summary>
/// A read document: the Map root of its entries and its schema reference, or the first error that stops reading.
/// Construction of explicit forms and the Range materialization total are left to Runtime.
/// </summary>
internal sealed class GesDocumentSyntax(string text, GesDocumentNode? root, GesDocumentSchemaSyntax schema, string schemaName, GesDocumentDiagnostic? diagnostic)
{
    internal string Text { get; } = text;

    // The entries as a Map node, absent when reading failed.
    internal GesDocumentNode? Root { get; } = root;

    internal GesDocumentSchemaSyntax Schema { get; } = schema;

    // The record type name or the file path of the schema.
    internal string SchemaName { get; } = schemaName;

    internal GesDocumentDiagnostic? Diagnostic { get; } = diagnostic;

    /// <summary>The 1-based line and column of a UTF-16 offset, in Unicode scalars; CRLF is one line break and a
    /// leading byte order mark is not counted.</summary>
    internal static (int Line, int Column) Location(string text, int offset)
    {
        var line = 1;
        var column = 1;
        var index = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        while (index < offset && index < text.Length)
        {
            var c = text[index++];
            if (c is '\n' or '\r')
            {
                if (c == '\r' && index < text.Length && text[index] == '\n' && index < offset) index++;
                line++;
                column = 1;
            }
            else
            {
                if (char.IsHighSurrogate(c) && index < text.Length && char.IsLowSurrogate(text[index])) index++;
                column++;
            }
        }
        return (line, column);
    }
}

internal enum GesStoreItemKind : byte
{
    // A root `import 'path'` of a further store file.
    Import,
    // `key: value` at the root or in a module block.
    Entry,
    // `key: import 'path'`, whose state comes from a state document.
    EntryImport
}

/// <summary>One item of a store file: an entry with its module, key and value, or an import with its path.</summary>
internal sealed class GesStoreItem(GesStoreItemKind kind, string module, string key, int keyOffset, GesDocumentNode? value, string path, int pathOffset)
{
    internal GesStoreItemKind Kind { get; } = kind;

    // The module name of the block holding the entry, empty at the root.
    internal string Module { get; } = module;

    internal string Key { get; } = key;

    internal int KeyOffset { get; } = keyOffset;

    // The entry's Map value, which may contain references.
    internal GesDocumentNode? Value { get; } = value;

    // The import path as written, and the offset of its opening quote.
    internal string Path { get; } = path;

    internal int PathOffset { get; } = pathOffset;
}

/// <summary>
/// A read store file (specs/Documents.md#store-files): its handle type and items, or the first error. A text
/// without a <c>:Store</c> header is no store file and has no error.
/// </summary>
internal sealed class GesStoreSyntax(string text, bool isStore, string typeName, int headerOffset, GesStoreItem[] items, GesDocumentDiagnostic? diagnostic)
{
    internal string Text { get; } = text;

    internal bool IsStore { get; } = isStore;

    // The handle type name of the header, without `@`.
    internal string TypeName { get; } = typeName;

    // The offset of the header's `:`.
    internal int HeaderOffset { get; } = headerOffset;

    internal GesStoreItem[] Items { get; } = items;

    internal GesDocumentDiagnostic? Diagnostic { get; } = diagnostic;
}
