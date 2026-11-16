// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

/// <summary>GES document text is invalid or exceeds a fixed literal parsing limit.</summary>
public sealed class GesDocumentException : Exception
{
    /// <summary>Creates a document error at a source position.</summary>
    /// <param name="code">The stable error code, for example <c>document.duplicateKey</c>.</param>
    /// <param name="line">The 1-based line of the offending position.</param>
    /// <param name="column">The 1-based column of the offending position, in Unicode scalars.</param>
    /// <param name="limitName">The stable name of the exceeded limit for <c>document.limitExceeded</c>; otherwise <see langword="null"/>.</param>
    /// <param name="limit">The exceeded limit value for <c>document.limitExceeded</c>; otherwise <see langword="null"/>.</param>
    /// <param name="path">The field path that a schema check or a model reports, such as <c>units[].name</c>; otherwise empty.</param>
    /// <param name="message">A message that replaces the default one, which is not part of the contract.</param>
    public GesDocumentException(string code, int line, int column, string? limitName = null, int? limit = null, string path = "", string? message = null)
        : base(message ?? (limitName is null
            ? $"Invalid GES document ({code}) at line {line}, column {column}{(string.IsNullOrEmpty(path) ? "" : $", field '{path}'")}."
            : $"GES document exceeds {limitName} ({limit}) at line {line}, column {column}."))
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Line = line;
        Column = column;
        LimitName = limitName;
        Limit = limit;
        Path = path ?? "";
    }

    /// <summary>The stable error code, such as <c>document.unquotedText</c>; see the GES document specification.</summary>
    public string Code { get; }

    /// <summary>The 1-based line of the offending position.</summary>
    public int Line { get; }

    /// <summary>The 1-based column of the offending position, counted in Unicode scalars.</summary>
    public int Column { get; }

    /// <summary>The stable name of the exceeded limit for <c>document.limitExceeded</c>, such as <c>MaxLiteralDepth</c>.</summary>
    public string? LimitName { get; }

    /// <summary>The exceeded limit value for <c>document.limitExceeded</c>.</summary>
    public int? Limit { get; }

    /// <summary>The field path of a schema check or model error, such as <c>units[].name</c>; empty for a reading error.</summary>
    public string Path { get; }
}
