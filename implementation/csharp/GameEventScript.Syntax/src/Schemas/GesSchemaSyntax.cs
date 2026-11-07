// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Syntax;

internal enum GesSchemaFieldMarker : byte
{
    None,
    Optional,
    Key
}

internal enum GesSchemaDeclarationKind : byte
{
    Constant,
    Parameter,
    Schema,
    Message,
    Messages
}

/// <summary>An exact decimal in lowest terms: no trailing zero in the significand, and zero is 0e0.</summary>
internal readonly struct GesSchemaDecimal : IEquatable<GesSchemaDecimal>
{
    internal GesSchemaDecimal(long significand, int exponent)
    {
        if (significand == 0) exponent = 0;
        while (significand != 0 && significand % 10 == 0)
        {
            significand /= 10;
            exponent++;
        }

        Significand = significand;
        Exponent = exponent;
    }

    internal long Significand { get; }

    internal int Exponent { get; }

    // The canonical spelling of the schema hash.
    internal string Canonical => $"{Significand}e{Exponent}";

    public bool Equals(GesSchemaDecimal other) => Significand == other.Significand && Exponent == other.Exponent;

    public override bool Equals(object? obj) => obj is GesSchemaDecimal other && Equals(other);

    public override int GetHashCode() => (Significand.GetHashCode() * 31) ^ Exponent;
}

/// <summary>A literal, a constant or a parameter in a bound, step or delta range.</summary>
internal sealed class GesSchemaValueSyntax
{
    internal GesSchemaDecimal Value { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    internal bool IsPercent { get; init; }

    // The spelling of a literal, which formatting keeps.
    internal string Text { get; init; } = "";

    // The name of a constant or parameter; empty for a literal.
    internal string Constant { get; init; } = "";

    internal string Parameter { get; init; } = "";

    internal int Offset { get; init; }
}

internal sealed class GesSchemaConstraintsSyntax
{
    internal bool Integer { get; init; }

    internal bool Wrapping { get; init; }

    internal GesSchemaValueSyntax? Lower { get; init; }

    internal GesSchemaValueSyntax? Upper { get; init; }

    internal GesSchemaValueSyntax? Step { get; init; }

    internal GesSchemaValueSyntax? Delta { get; init; }
}

internal sealed class GesSchemaAxisSyntax
{
    internal char Axis { get; init; }

    internal GesSchemaConstraintsSyntax Constraints { get; init; } = new();

    internal int Offset { get; init; }
}

internal sealed class GesSchemaTypeSyntax
{
    internal GesSchemaTypeKind Kind { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    // The referenced schema's name, or for a Quantity and a spatial type whether a unit was written.
    internal string Name { get; init; } = "";

    internal string[]? Tags { get; init; }

    // The written maximum of Text and List.
    internal long? Max { get; init; }

    // The alternatives of a List's element.
    internal GesSchemaTypeSyntax[]? Element { get; init; }

    internal GesSchemaConstraintsSyntax Constraints { get; init; } = new();

    internal GesSchemaAxisSyntax[]? Axes { get; init; }

    internal int Offset { get; init; }
}

internal sealed class GesSchemaFieldSyntax
{
    internal string Name { get; init; } = "";

    internal bool Unlabeled { get; init; }

    internal GesSchemaTypeSyntax[] Alternatives { get; init; } = [];

    internal GesSchemaFieldMarker Marker { get; init; }

    internal int Offset { get; init; }
}

internal sealed class GesSchemaDeclarationSyntax
{
    internal GesSchemaDeclarationKind Kind { get; init; }

    internal string Name { get; init; } = "";

    internal int Offset { get; init; }

    internal GesSchemaValueSyntax? Value { get; init; }

    // The kind and unit of a parameter.
    internal GesSchemaTypeSyntax? Type { get; init; }

    internal GesSchemaFieldSyntax[] Fields { get; init; } = [];

    internal string[] Tags { get; init; } = [];

    internal string[] Members { get; init; } = [];

    internal int[] MemberOffsets { get; init; } = [];
}

internal sealed class GesSchemaDiagnostic
{
    internal const string Syntax = "schema.syntax";
    internal const string DuplicateName = "schema.duplicateName";
    internal const string UnknownName = "schema.unknownName";
    internal const string Cycle = "schema.cycle";
    internal const string UnitMismatch = "schema.unitMismatch";
    internal const string InvalidRange = "schema.invalidRange";
    internal const string InvalidStep = "schema.invalidStep";
    internal const string InvalidConstraint = "schema.invalidConstraint";
    internal const string NotEncodable = "schema.notEncodable";
    internal const string InvalidKey = "schema.invalidKey";
    internal const string InvalidUnion = "schema.invalidUnion";
    internal const string InvalidBound = "schema.invalidBound";
    internal const string TooWide = "schema.tooWide";
    internal const string TooLarge = "schema.tooLarge";

    internal GesSchemaDiagnostic(string code, string text, int offset, string message)
    {
        Code = code;
        Offset = offset;
        Message = message;
        (Line, Column) = GesDocumentSyntax.Location(text, offset);
    }

    internal string Code { get; }

    internal int Offset { get; }

    internal int Line { get; }

    internal int Column { get; }

    internal string Message { get; }
}

internal sealed class GesSchemaSourceSyntax
{
    internal GesSchemaSourceSyntax(string text, GesSchemaDeclarationSyntax[] declarations, GesSchemaDiagnostic? error)
    {
        Text = text;
        Declarations = declarations;
        Error = error;
    }

    internal string Text { get; }

    internal GesSchemaDeclarationSyntax[] Declarations { get; }

    internal GesSchemaDiagnostic? Error { get; }
}
