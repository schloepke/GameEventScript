// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace GameEventScript.SyntaxHighlighter;

/// <summary>The bundled source or assembler grammar used for presentation.</summary>
public enum GameEventScriptSyntaxLanguage
{
    /// <summary>Game Event Script source, including incomplete editor input.</summary>
    Ges,
    /// <summary>GESA assembler with embedded GES source regions.</summary>
    Gesa
}

/// <summary>Theme-independent categories derived from TextMate scopes; these are not compiler tokens.</summary>
public enum GameEventScriptSyntaxKind
{
    /// <summary>Text without a more specific category, including whitespace.</summary>
    Plain,
    /// <summary>A keyword, word operator or boolean literal.</summary>
    Keyword,
    /// <summary>A built-in function or collection selector.</summary>
    Builtin,
    /// <summary>A variable or parameter identifier.</summary>
    Identifier,
    /// <summary>A message or handler name.</summary>
    Message,
    /// <summary>A source type name.</summary>
    Type,
    /// <summary>A tag name.</summary>
    Tag,
    /// <summary>A named constant.</summary>
    Constant,
    /// <summary>A numeric literal, including units or dice syntax.</summary>
    Number,
    /// <summary>A string literal, delimiter or escape.</summary>
    String,
    /// <summary>A comment.</summary>
    Comment,
    /// <summary>Punctuation such as parentheses and separators.</summary>
    Symbol,
    /// <summary>A declared function or predicate name.</summary>
    Function,
    /// <summary>A module name.</summary>
    Module,
    /// <summary>An assembler label or symbol reference.</summary>
    Label,
    /// <summary>An assembler register.</summary>
    Register
}

/// <summary>An immutable UTF-16 range with its category and outer-to-inner TextMate scope stack.</summary>
public sealed class GameEventScriptHighlightSpan
{
    /// <summary>Zero-based UTF-16 offset in the supplied document or line.</summary>
    public int Start { get; }
    /// <summary>Range length in UTF-16 code units.</summary>
    public int Length { get; }
    /// <summary>The most specific recognized category.</summary>
    public GameEventScriptSyntaxKind Kind { get; }
    /// <summary>Immutable outer-to-inner TextMate scopes, including the root language scope.</summary>
    public IReadOnlyList<string> Scopes { get; }

    internal GameEventScriptHighlightSpan(int start, int length, string[] scopes)
    {
        Start = start;
        Length = length;
        Scopes = Array.AsReadOnly(scopes);
        Kind = ScopeKinds.Classify(scopes);
    }
}

/// <summary>An immutable, equatable line-boundary state. It is not a serializable editor document or compiler state.</summary>
public sealed class GameEventScriptHighlightState : IEquatable<GameEventScriptHighlightState>
{
    /// <summary>The language this state belongs to; states cannot be reused for another language.</summary>
    public GameEventScriptSyntaxLanguage Language { get; }
    internal int[] Frames { get; }

    internal GameEventScriptHighlightState(GameEventScriptSyntaxLanguage language, IEnumerable<int> frames)
    {
        Language = language;
        Frames = frames.ToArray();
    }

    /// <summary>Compares grammar context for incremental retokenization convergence.</summary>
    public bool Equals(GameEventScriptHighlightState? other) => other is not null && Language == other.Language && Frames.SequenceEqual(other.Frames);
    /// <summary>Compares grammar context for incremental retokenization convergence.</summary>
    public override bool Equals(object? obj) => obj is GameEventScriptHighlightState other && Equals(other);
    /// <summary>Returns a hash consistent with grammar-state equality.</summary>
    public override int GetHashCode()
    {
        var hash = (int)Language;
        foreach (var frame in Frames) hash = unchecked(hash * 31 + frame);
        return hash;
    }
}

/// <summary>A highlighting result. Failed resource bounds return no spans or usable continuation state.</summary>
public sealed class GameEventScriptHighlightResult
{
    /// <summary>Ordered, nonoverlapping ranges covering the complete input when successful.</summary>
    public IReadOnlyList<GameEventScriptHighlightSpan> Spans { get; }
    /// <summary>Whether all input was highlighted within the resource limits.</summary>
    public bool IsComplete { get; }
    /// <summary>Context after the final line, or null when incomplete. Do not cache an incomplete result.</summary>
    public GameEventScriptHighlightState? NextState { get; }

    internal GameEventScriptHighlightResult(List<GameEventScriptHighlightSpan> spans, GameEventScriptHighlightState? state)
    {
        Spans = spans.AsReadOnly();
        NextState = state;
        IsComplete = state is not null;
    }
}

internal static class ScopeKinds
{
    internal static GameEventScriptSyntaxKind Classify(IReadOnlyList<string> scopes)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            var scope = scopes[i];
            bool Is(string prefix) => scope.StartsWith(prefix, StringComparison.Ordinal);
            if (Is("comment")) return GameEventScriptSyntaxKind.Comment;
            if (Is("string") || Is("constant.character.escape")) return GameEventScriptSyntaxKind.String;
            if (Is("constant.numeric") || Is("constant.language.numeric")) return GameEventScriptSyntaxKind.Number;
            if (Is("variable.other.constant")) return GameEventScriptSyntaxKind.Constant;
            if (Is("variable.other.register")) return GameEventScriptSyntaxKind.Register;
            if (Is("variable.other.label") || Is("constant.other.symbol")) return GameEventScriptSyntaxKind.Label;
            if (Is("entity.name.type.class.message") || Is("entity.name.function.handler")) return GameEventScriptSyntaxKind.Message;
            if (Is("support.type") || Is("entity.name.type")) return GameEventScriptSyntaxKind.Type;
            if (Is("entity.name.namespace")) return GameEventScriptSyntaxKind.Module;
            if (Is("entity.name.function")) return GameEventScriptSyntaxKind.Function;
            if (Is("entity.other.attribute-name.tag")) return GameEventScriptSyntaxKind.Tag;
            if (Is("support") || Is("entity")) return GameEventScriptSyntaxKind.Builtin;
            if (Is("keyword") || Is("constant.language.boolean")) return GameEventScriptSyntaxKind.Keyword;
            if (Is("variable")) return GameEventScriptSyntaxKind.Identifier;
            if (Is("punctuation")) return GameEventScriptSyntaxKind.Symbol;
        }
        return GameEventScriptSyntaxKind.Plain;
    }
}
