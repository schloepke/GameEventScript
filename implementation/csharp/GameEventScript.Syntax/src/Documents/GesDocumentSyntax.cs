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
    Vector,
    Point,
    // An explicit form such as `:Range(1, 5)`, constructed by Runtime.
    Form,
    // The message of `:Message(Done(v: 1))` before its tags.
    Message,
    Handler
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

    // The Text value, the tag name, the lowercase form type or the message name.
    internal string Name { get; private init; } = "";

    internal bool Boolean { get; private init; }

    internal GesNumberLiteral Number { get; private init; }

    // Map keys, or the argument labels of a form, message or handler; `_` for an unlabeled one.
    internal string[] Labels { get; private init; } = [];

    internal GesDocumentNode[] Children { get; private init; } = NoChildren;

    internal int[] Rolls { get; private init; } = [];

    internal double X { get; private init; }

    internal double Y { get; private init; }

    internal double Z { get; private init; }

    internal GesQuantityUnit Unit { get; private init; }

    // The UTF-16 offset of a form's `:`, where construction errors point.
    internal int Offset { get; private init; }

    internal static GesDocumentNode Text(string value) => new(GesDocumentNodeKind.Text) { Name = value };

    internal static GesDocumentNode BooleanValue(bool value) => new(GesDocumentNodeKind.Boolean) { Boolean = value };

    internal static GesDocumentNode NothingValue() => new(GesDocumentNodeKind.Nothing);

    internal static GesDocumentNode Tag(string name) => new(GesDocumentNodeKind.Tag) { Name = name };

    internal static GesDocumentNode NumberValue(GesNumberLiteral number) => new(GesDocumentNodeKind.Number) { Number = number };

    internal static GesDocumentNode List(GesDocumentNode[] items) => new(GesDocumentNodeKind.List) { Children = items };

    internal static GesDocumentNode Map(string[] keys, GesDocumentNode[] values) => new(GesDocumentNodeKind.Map) { Labels = keys, Children = values };

    internal static GesDocumentNode Dice(int[] rolls) => new(GesDocumentNodeKind.Dice) { Rolls = rolls };

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
        InvalidSchemaHeader => "A schema header names one record type or a schema file.",
        MisplacedSchemaHeader => "The schema header must come before the first entry.",
        _ => $"The document exceeds {limitName ?? "a limit"} ({limitValue})."
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
