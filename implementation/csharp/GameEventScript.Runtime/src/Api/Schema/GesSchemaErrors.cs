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
    internal const string Truncated = "wire.truncated";
    internal const string InvalidIndex = "wire.invalidIndex";
    internal const string InvalidCount = "wire.invalidCount";
    internal const string InvalidText = "wire.invalidText";
    internal const string InvalidTag = "wire.invalidTag";
    internal const string InvalidNumber = "wire.invalidNumber";
    internal const string InvalidPadding = "wire.invalidPadding";
    internal const string LimitExceeded = "wire.limitExceeded";
    internal const string OutOfRange = "wire.outOfRange";
    internal const string NotRepresentable = "wire.notRepresentable";
    internal const string MissingField = "wire.missingField";
    internal const string TypeMismatch = "wire.typeMismatch";
    internal const string UnknownTag = "wire.unknownTag";
    internal const string TooLong = "wire.tooLong";
    internal const string BufferTooSmall = "wire.bufferTooSmall";
    internal const string FieldOrder = "wire.fieldOrder";

    internal GesWireException(string code, string path) : base($"{code} at '{path}'")
    {
        Code = code;
        Path = path;
    }

    /// <summary>Gets the stable <c>wire.*</c> code.</summary>
    public string Code { get; }

    /// <summary>Gets the path of the field, such as <c>position.x</c>; empty for the record as a whole.</summary>
    public string Path { get; }
}
