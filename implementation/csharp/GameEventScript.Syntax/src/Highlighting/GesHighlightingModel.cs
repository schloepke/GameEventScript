// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Syntax;

/// <summary>The language a highlighter reads.</summary>
public enum GesSyntaxLanguage
{
    /// <summary>Game Event Script source, including incomplete editor input.</summary>
    Ges,
    /// <summary>GESA assembler with embedded GES source regions.</summary>
    Gesa
}

/// <summary>Theme-independent categories the highlighter derives from the syntax tree; these are not compiler tokens.</summary>
public enum GesSyntaxKind
{
    /// <summary>Text without a more specific category, including whitespace and line breaks.</summary>
    Plain,
    /// <summary>A keyword, contextual keyword, operator or boolean literal.</summary>
    Keyword,
    /// <summary>A built-in function, collection selector, extension name, unit or inline-assembly opcode.</summary>
    Builtin,
    /// <summary>A variable, parameter, field, member or label identifier.</summary>
    Identifier,
    /// <summary>A message or handler name.</summary>
    Message,
    /// <summary>A source type or record name.</summary>
    Type,
    /// <summary>A tag name.</summary>
    Tag,
    /// <summary>A named constant.</summary>
    Constant,
    /// <summary>A numeric literal, including units, dice syntax and math constants.</summary>
    Number,
    /// <summary>A string literal, including an unterminated one.</summary>
    String,
    /// <summary>A comment.</summary>
    Comment,
    /// <summary>Punctuation such as parentheses, brackets and separators.</summary>
    Symbol,
    /// <summary>A function or predicate name, where declared or called.</summary>
    Function,
    /// <summary>A module name.</summary>
    Module,
    /// <summary>An assembler label or symbol reference.</summary>
    Label,
    /// <summary>An assembler register.</summary>
    Register
}

/// <summary>A diagnostic of the highlighted source: a syntax error or a violated single-source rule.</summary>
/// <remarks>Rules that need other sources, catalogs or a host are checked only by the Compiler.</remarks>
public sealed class GesHighlightDiagnostic
{
    /// <summary>The stable diagnostic code, such as <c>parse.syntax</c> or <c>validate.duplicateVariable</c>.</summary>
    public string Code { get; }
    /// <summary>A message for people; its wording is not stable.</summary>
    public string Message { get; }
    /// <summary>Zero-based UTF-16 offset of the range in the highlighted text.</summary>
    public int Start { get; }
    /// <summary>Range length in UTF-16 code units; zero for a position, such as the end of an unfinished source.</summary>
    public int Length { get; }

    internal GesHighlightDiagnostic(string code, string message, int start, int length)
    {
        Code = code;
        Message = message;
        Start = start;
        Length = length;
    }
}

/// <summary>An immutable UTF-16 range with its category and the diagnostics that cover it.</summary>
public sealed class GesHighlightSpan
{
    /// <summary>Zero-based UTF-16 offset in the highlighted text.</summary>
    public int Start { get; }
    /// <summary>Range length in UTF-16 code units; always positive.</summary>
    public int Length { get; }
    /// <summary>The category of the range.</summary>
    public GesSyntaxKind Kind { get; }
    /// <summary>The diagnostics whose range overlaps this span, in source order; a diagnostic without length belongs to the span at its position.</summary>
    public IReadOnlyList<GesHighlightDiagnostic> Diagnostics { get; }

    internal GesHighlightSpan(int start, int length, GesSyntaxKind kind, IReadOnlyList<GesHighlightDiagnostic> diagnostics)
    {
        Start = start;
        Length = length;
        Kind = kind;
        Diagnostics = diagnostics;
    }
}

/// <summary>A highlighting result. An input above the length bound returns an incomplete result without spans or diagnostics.</summary>
public sealed class GesHighlightResult
{
    /// <summary>Ordered, nonoverlapping ranges covering the complete input when complete; empty for empty input.</summary>
    public IReadOnlyList<GesHighlightSpan> Spans { get; }
    /// <summary>All diagnostics of the input in source order; GESA input has none.</summary>
    public IReadOnlyList<GesHighlightDiagnostic> Diagnostics { get; }
    /// <summary>Whether the input was within the length bound and has been highlighted.</summary>
    public bool IsComplete { get; }

    internal GesHighlightResult(IReadOnlyList<GesHighlightSpan> spans, IReadOnlyList<GesHighlightDiagnostic> diagnostics, bool isComplete)
    {
        Spans = spans;
        Diagnostics = diagnostics;
        IsComplete = isComplete;
    }

    internal static readonly GesHighlightResult Incomplete = new(Array.Empty<GesHighlightSpan>(), Array.Empty<GesHighlightDiagnostic>(), false);
}
