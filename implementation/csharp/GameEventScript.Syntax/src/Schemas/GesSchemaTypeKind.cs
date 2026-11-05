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
    Nothing
}
