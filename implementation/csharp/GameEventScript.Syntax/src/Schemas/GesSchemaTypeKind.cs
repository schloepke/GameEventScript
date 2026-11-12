// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>The kinds of types a schema field can have.</summary>
public enum GesSchemaTypeKind : byte
{
    /// <summary>A Boolean.</summary>
    Boolean,

    /// <summary>A Number.</summary>
    Number,

    /// <summary>A Percentage.</summary>
    Percentage,

    /// <summary>A Number with a unit.</summary>
    Quantity,

    /// <summary>A Point.</summary>
    Point,

    /// <summary>A Vector.</summary>
    Vector,

    /// <summary>A Tag, of a closed set or any.</summary>
    Tag,

    /// <summary>A Text.</summary>
    Text,

    /// <summary>A List.</summary>
    List,

    /// <summary>A reference to a schema.</summary>
    Reference,

    /// <summary>A Map.</summary>
    Map,

    /// <summary>A Range.</summary>
    Range,

    /// <summary>A built-in Series.</summary>
    Series,

    /// <summary>A Dice value.</summary>
    Dice,

    /// <summary>A Message.</summary>
    Message,

    /// <summary>A Handler.</summary>
    Handler,

    /// <summary>Nothing.</summary>
    Nothing,

    /// <summary>A Bytes value.</summary>
    Bytes,

    /// <summary>Flags of a closed set, as a Map of the set members.</summary>
    Flags,

    /// <summary>An axis-aligned Box between two Points.</summary>
    Box,

    /// <summary>A polygon of Points in the plane z = 0.</summary>
    Poly
}

internal static class GesSchemaTypeNames
{
    // A name no declaration can take: the types below and the language's Record, which is no schema type.
    internal static bool IsBuiltIn(string name) => name == "Record" || Kind(name) is not null;

    // The built-in type of a name; any other name refers to a declaration.
    internal static GesSchemaTypeKind? Kind(string name) => name switch
    {
        "Boolean" => GesSchemaTypeKind.Boolean,
        "Number" => GesSchemaTypeKind.Number,
        "Percentage" => GesSchemaTypeKind.Percentage,
        "Quantity" => GesSchemaTypeKind.Quantity,
        "Point" => GesSchemaTypeKind.Point,
        "Vector" => GesSchemaTypeKind.Vector,
        "Box" => GesSchemaTypeKind.Box,
        "Poly" => GesSchemaTypeKind.Poly,
        "Tag" => GesSchemaTypeKind.Tag,
        "Text" => GesSchemaTypeKind.Text,
        "Bytes" => GesSchemaTypeKind.Bytes,
        "Flags" => GesSchemaTypeKind.Flags,
        "List" => GesSchemaTypeKind.List,
        "Map" => GesSchemaTypeKind.Map,
        "Range" => GesSchemaTypeKind.Range,
        "Series" => GesSchemaTypeKind.Series,
        "Dice" => GesSchemaTypeKind.Dice,
        "Message" => GesSchemaTypeKind.Message,
        "Handler" => GesSchemaTypeKind.Handler,
        "Nothing" => GesSchemaTypeKind.Nothing,
        _ => null
    };
}
