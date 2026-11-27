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
    Messages,
    TagSet,
    Network,
    Handle,
    Projection
}

internal enum GesSchemaRouteClass : byte
{
    Reliable,
    Latest,
    Bulk
}

// One route of a network: `client sends :Root reliable`, `client sends Move reliable` or
// `server publishes :Root latest at most every 200ms`.
internal sealed class GesSchemaRouteSyntax
{
    internal bool Server { get; init; }

    internal bool Publishes { get; init; }

    internal string Root { get; init; } = "";

    // The root is a message, written without a colon; otherwise a schema or message set.
    internal bool IsMessage { get; init; }

    internal int RootOffset { get; init; }

    internal GesSchemaRouteClass Class { get; init; }

    internal int ClassOffset { get; init; }

    // The interval of `at most every`, a literal or a constant; null without one.
    internal GesSchemaValueSyntax? Rate { get; init; }

    // `server owns @Target`: Root names the handle type, and the route has no class or rate.
    internal bool Owns { get; init; }

    // The projection of `owns @Unit as :UnitPublic`, which the other side reads, without `:`; empty without one.
    internal string Projection { get; init; } = "";

    internal int ProjectionOffset { get; init; }

    // `owns @Target sending state`: the owner sends the state its party reads with each handle.
    internal bool SendingState { get; init; }

    internal int Offset { get; init; }
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

    // The canonical spelling of the schema hash, with ASCII signs whatever the current culture.
    internal string Canonical => Significand.ToString(System.Globalization.CultureInfo.InvariantCulture) + "e" + Exponent.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool Equals(GesSchemaDecimal other) => Significand == other.Significand && Exponent == other.Exponent;

    public override bool Equals(object? obj) => obj is GesSchemaDecimal other && Equals(other);

    public override int GetHashCode() => (Significand.GetHashCode() * 31) ^ Exponent;
}

/// <summary>A literal, a constant, or a name of a let or parameter in a bound, step, delta range or let.</summary>
internal sealed class GesSchemaValueSyntax
{
    internal GesSchemaDecimal Value { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    internal bool IsPercent { get; init; }

    // The spelling of a literal, which formatting keeps.
    internal string Text { get; init; } = "";

    // The name of a constant, or a lowercase name of a let of the schema or a parameter of the file; empty for a literal.
    internal string Constant { get; init; } = "";

    internal string Name { get; init; } = "";

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
    // x, y, z, or the radius of a Disc.
    internal string Axis { get; init; } = "";

    internal GesSchemaConstraintsSyntax Constraints { get; init; } = new();

    internal int Offset { get; init; }
}

internal sealed class GesSchemaTypeSyntax
{
    internal GesSchemaTypeKind Kind { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    // The referenced schema's name, a handle type's name without `@`, or for a Quantity and a spatial type whether a unit was written.
    internal string Name { get; init; } = "";

    // A handle type written `@Name~`, with its version.
    internal bool Versioned { get; init; }

    // The projection of a handle type written `@Name(:P)`, without ':', and where it starts; empty otherwise.
    internal string Projection { get; init; } = "";

    internal int ProjectionOffset { get; init; }

    // The key handle type of a handle Map `:Map(@Name → T)`, empty for any other type, and where it starts.
    internal string Key { get; init; } = "";

    internal int KeyOffset { get; init; }

    internal string[]? Tags { get; init; }

    // The written maximum of Text and List, or the let that holds it, and where that name starts.
    internal long? Max { get; init; }

    internal string MaxName { get; init; } = "";

    internal int MaxOffset { get; init; }

    // The alternatives of a List's element.
    internal GesSchemaTypeSyntax[]? Element { get; init; }

    // The written `depth` of a List whose element is the schema that declares it, and where it starts; 0 without one.
    internal int Depth { get; init; }

    internal int DepthOffset { get; init; }

    internal GesSchemaConstraintsSyntax Constraints { get; init; } = new();

    internal GesSchemaAxisSyntax[]? Axes { get; init; }

    internal int Offset { get; init; }

    // The label of a union alternative, such as `bind` in `bind: :Loadout_UnitBind`, or empty, and where it starts.
    internal string Label { get; set; } = "";

    internal int LabelOffset { get; set; }
}

internal sealed class GesSchemaFieldSyntax
{
    internal string Name { get; init; } = "";

    internal bool Unlabeled { get; init; }

    internal GesSchemaTypeSyntax[] Alternatives { get; init; } = [];

    internal GesSchemaFieldMarker Marker { get; init; }

    internal GesSchemaDefaultSyntax? Default { get; init; }

    // The expression of a field written `computed by`, which is never encoded.
    internal GesSchemaExpressionSyntax? Computed { get; init; }

    // The index of the entry among all fields, lets and withs of its schema, which orders computed and taken fields.
    internal int Sequence { get; set; }

    internal int Offset { get; init; }
}

internal enum GesSchemaExpressionKind : byte
{
    Literal,
    Boolean,
    Constant,
    Name,
    Member,
    Negate,
    Not,
    Abs,
    Binary,
    Choice,
    Min,
    Max,
    Clamp,
    Default,
    HasValue,
    Sum,
    Count,
    Function,
    Construct,
    List,
    Select
}

internal enum GesSchemaBinaryOperator : byte
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Equal,
    NotEqual,
    And,
    Or,
    Power
}

// An expression of a computed field, a subset of GES expressions with their spellings. A Member reads Name from its one
// child; a Choice holds value and condition pairs and then the fallback; Sum holds its list and, with a variable in Name,
// the projection; Clamp holds the value and both bounds; a Function holds its arguments and the function's ASCII spelling in
// Name, such as `sqrt`, `round half up` or `atan2`. A Construct builds the spatial type in Name (`Vector`, `Point`, `Box`,
// `Disc` or `Poly`) from its arguments, each with its label in Labels or an empty one for a positional argument; a List holds
// the items of a list literal; Select holds its list and the projection, with its variable in Name.
internal sealed class GesSchemaExpressionSyntax
{
    internal GesSchemaExpressionKind Kind { get; init; }

    internal GesSchemaBinaryOperator Operator { get; init; }

    internal GesSchemaValueSyntax? Literal { get; init; }

    internal bool Boolean { get; init; }

    internal string Name { get; init; } = "";

    internal GesSchemaExpressionSyntax[] Children { get; init; } = [];

    // The label of each argument of a Construct, empty for a positional one.
    internal string[] Labels { get; init; } = [];

    // The depth of the tree below and including this node, which the reader limits.
    internal int Depth { get; init; } = 1;

    internal int Offset { get; init; }
}

// The value a field takes when a value or document leaves it out: `default` with a number, a constant, a let or a sibling field
// (both in Value), `true` or `false`, a tag, or a record literal such as `[:]` or `[efficiency: 90%]`.
internal sealed class GesSchemaDefaultSyntax
{
    internal GesSchemaValueSyntax? Value { get; init; }

    internal bool? Boolean { get; init; }

    internal string? Tag { get; init; }

    // The named fields of a record literal in written order, empty for `[:]`; null for every other default.
    internal GesSchemaRecordEntrySyntax[]? Record { get; init; }

    // Whether the default is `[]`, the empty List.
    internal bool EmptyList { get; init; }

    internal int Offset { get; init; }
}

// One `name: value` of a record literal; the value is again a number, a constant, `true`, `false`, a tag or a record literal.
internal sealed class GesSchemaRecordEntrySyntax
{
    internal string Name { get; init; } = "";

    internal GesSchemaDefaultSyntax Value { get; init; } = null!;

    internal int Offset { get; init; }
}

// A constant of a schema, `let name be value`, which its fields can name.
internal sealed class GesSchemaLetSyntax
{
    internal string Name { get; init; } = "";

    internal GesSchemaValueSyntax Value { get; init; } = null!;

    internal int Offset { get; init; }
}

// `with :Name all optional without (a, b)` inside a schema: the fields of another schema, copied where it stands.
internal sealed class GesSchemaWithSyntax
{
    internal string Name { get; init; } = "";

    internal bool AllOptional { get; init; }

    internal string[] Without { get; init; } = [];

    internal int[] WithoutOffsets { get; init; } = [];

    // The number of the schema's own fields declared before it.
    internal int Position { get; init; }

    // The index of the entry among all fields, lets and withs of its schema.
    internal int Sequence { get; init; }

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

    internal GesSchemaLetSyntax[] Lets { get; init; } = [];

    internal GesSchemaWithSyntax[] Withs { get; init; } = [];

    // The fields written `computed by`, in declared order; Fields holds the others.
    internal GesSchemaFieldSyntax[] Computed { get; init; } = [];

    // An abstract schema only lends its fields with `with`.
    internal bool Abstract { get; set; }

    // A packed schema is one machine word; its width is 8, 16, 32 or 64 when written as `packed(width)`, else 0.
    internal bool Packed { get; set; }

    internal int PackedWidth { get; set; }

    internal int PackedOffset { get; set; }

    internal string[] Tags { get; init; } = [];

    internal string[] Members { get; init; } = [];

    internal int[] MemberOffsets { get; init; } = [];

    // The routes of a network in declared order.
    internal GesSchemaRouteSyntax[] Routes { get; init; } = [];

    // The fields a projection takes from its source, which State names, in written order.
    internal GesSchemaTakenSyntax[] Taken { get; init; } = [];

    // A handle declaration: its state schema, the composite root without `@`, the id or part type, the version form and the
    // revision's type, each with where it starts.
    internal string State { get; init; } = "";

    internal int StateOffset { get; init; }

    // The handle type of a catalog file type, `:Catalog(@Type)`, without `@`; empty otherwise, when State names the state.
    internal string Catalog { get; init; } = "";

    internal int CatalogOffset { get; init; }

    internal string Root { get; init; } = "";

    internal int RootOffset { get; init; }

    internal GesSchemaTypeSyntax? Id { get; init; }

    internal GesHandleVersionKind Version { get; init; }

    internal GesSchemaTypeSyntax? Revision { get; init; }

    internal int VersionOffset { get; init; }

    // The group of a wire or model root, or null when the declaration has no such role.
    internal string? WireGroup { get; set; }

    internal string? ModelGroup { get; set; }
}

// A field a projection takes: its name and the path of field names from the source's root, one segment for a bare name.
internal sealed class GesSchemaTakenSyntax
{
    internal string Name { get; init; } = "";

    internal string[] Path { get; init; } = [];

    internal int Offset { get; init; }

    // Where each path segment starts.
    internal int[] PathOffsets { get; init; } = [];
}

internal sealed class GesSchemaDiagnostic
{
    internal const string Syntax = "schema.syntax";
    internal const string DuplicateName = "schema.duplicateName";
    internal const string UnknownName = "schema.unknownName";
    internal const string AmbiguousName = "schema.ambiguousName";
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
    internal const string InvalidRole = "schema.invalidRole";
    internal const string DuplicateRole = "schema.duplicateRole";
    internal const string InvalidDefault = "schema.invalidDefault";
    internal const string InvalidAbstract = "schema.invalidAbstract";
    internal const string PackedField = "schema.packedField";
    internal const string PackedTooWide = "schema.packedTooWide";
    internal const string InvalidExpression = "schema.invalidExpression";
    internal const string ComputedMayBeAbsent = "schema.computedMayBeAbsent";
    internal const string InvalidRoute = "schema.invalidRoute";
    internal const string DuplicateRoute = "schema.duplicateRoute";
    internal const string InvalidHandle = "schema.invalidHandle";
    internal const string InvalidProjection = "schema.invalidProjection";

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
