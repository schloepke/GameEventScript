// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

/// <summary>
/// A schema source or a binding that breaks a rule. Callers branch on <see cref="Code"/>, a stable <c>schema.*</c> code;
/// the message is informative text.
/// </summary>
public sealed class GesSchemaException : Exception
{
    internal GesSchemaException(string code, string sourceName, int line, int column, string message) : base(message)
    {
        Code = code;
        SourceName = sourceName;
        Line = line;
        Column = column;
    }

    /// <summary>Gets the stable <c>schema.*</c> code.</summary>
    public string Code { get; }

    /// <summary>Gets the name of the source the error is in.</summary>
    public string SourceName { get; }

    /// <summary>Gets the one-based line, or 0 when the error has no position.</summary>
    public int Line { get; }

    /// <summary>Gets the one-based Unicode-scalar column, or 0 when the error has no position.</summary>
    public int Column { get; }
}

/// <summary>
/// A value that cannot be encoded, or data that is not a valid encoding. The error names the field path and holds no value
/// from the data, so it can be logged safely.
/// </summary>
public sealed class GesWireException : Exception
{
    /// <summary>The code <c>wire.truncated</c>: the data ends before the record does.</summary>
    public const string Truncated = "wire.truncated";

    /// <summary>The code <c>wire.invalidIndex</c>: an index is at least the size of its range, tag set, union or message set.</summary>
    public const string InvalidIndex = "wire.invalidIndex";

    /// <summary>The code <c>wire.invalidCount</c>: a Text, Tag, List or Dice count exceeds its <c>max</c>.</summary>
    public const string InvalidCount = "wire.invalidCount";

    /// <summary>The code <c>wire.invalidText</c>: Text is not valid UTF-8.</summary>
    public const string InvalidText = "wire.invalidText";

    /// <summary>The code <c>wire.invalidTag</c>: a <c>:Tag max n</c> holds or receives no valid tag name.</summary>
    public const string InvalidTag = "wire.invalidTag";

    /// <summary>The code <c>wire.invalidNumber</c>: data holds NaN or negative zero, or an infinity with a range.</summary>
    public const string InvalidNumber = "wire.invalidNumber";

    /// <summary>The code <c>wire.invalidPadding</c>: a padding bit is not zero.</summary>
    public const string InvalidPadding = "wire.invalidPadding";

    /// <summary>The code <c>wire.limitExceeded</c>: the record exceeds the caller's byte limit.</summary>
    public const string LimitExceeded = "wire.limitExceeded";

    /// <summary>The code <c>wire.outOfRange</c>: a value is outside its range, not finite in a quantized field, or more than 2^50 steps from zero.</summary>
    public const string OutOfRange = "wire.outOfRange";

    /// <summary>The code <c>wire.notRepresentable</c>: an integer field receives a fractional value, or a value does not fit its stored form.</summary>
    public const string NotRepresentable = "wire.notRepresentable";

    /// <summary>The code <c>wire.missingField</c>: a field that is neither <c>optional</c> nor has a <c>:Nothing</c> alternative has no value.</summary>
    public const string MissingField = "wire.missingField";

    /// <summary>The code <c>wire.nothingValue</c>: a field, argument or list element is <c>nothing</c> without allowing it.</summary>
    public const string NothingValue = "wire.nothingValue";

    /// <summary>The code <c>wire.typeMismatch</c>: a value has another kind or unit than its field.</summary>
    public const string TypeMismatch = "wire.typeMismatch";

    /// <summary>The code <c>wire.unknownTag</c>: a tag is not in its set.</summary>
    public const string UnknownTag = "wire.unknownTag";

    /// <summary>The code <c>wire.tooLong</c>: a Text, Tag, List or Dice exceeds its <c>max</c>.</summary>
    public const string TooLong = "wire.tooLong";

    /// <summary>The code <c>wire.bufferTooSmall</c>: the caller's buffer is too small.</summary>
    public const string BufferTooSmall = "wire.bufferTooSmall";

    /// <summary>The code <c>wire.fieldOrder</c>: a typed writer or reader uses a field out of order.</summary>
    public const string FieldOrder = "wire.fieldOrder";

    /// <summary>The code <c>wire.keyMismatch</c>: a record and its baseline have different keys.</summary>
    public const string KeyMismatch = "wire.keyMismatch";

    /// <summary>The code <c>wire.invalidDelta</c>: a delta is not canonical, leaves its range, or its key differs from the baseline's.</summary>
    public const string InvalidDelta = "wire.invalidDelta";

    /// <summary>The code <c>wire.overlappingBuffers</c>: the target buffer of a delta overlaps an input.</summary>
    public const string OverlappingBuffers = "wire.overlappingBuffers";

    /// <summary>Creates an error with a stable code and a field path.</summary>
    /// <param name="code">The stable <c>wire.*</c> code, one of the constants of this type.</param>
    /// <param name="path">The path of the field; empty for the record as a whole.</param>
    public GesWireException(string code, string path) : base($"{code} at '{path}'")
    {
        Code = code;
        Path = path;
    }

    /// <summary>Gets the stable <c>wire.*</c> code.</summary>
    public string Code { get; }

    /// <summary>Gets the path of the field, such as <c>position.x</c>; empty for the record as a whole.</summary>
    public string Path { get; }
}
