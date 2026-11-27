// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

// Static handle type names as the binder keeps them: `@Name`, `@Name~`, `@Name(:P)`, `@Name(:P)~` and unions joined with
// `|` (specs/Language.md#handles, #projections).
internal static class GesHandleTypeNames
{
    // Whether a declared or static type names a handle type or a union, which is only of handle types.
    internal static bool IsHandleType(string? typeName)
        => !string.IsNullOrEmpty(typeName) && (typeName![0] == '@' || typeName.IndexOf('|') >= 0);

    // The name without `@` and projection and whether `~` requires a version.
    internal static (string Name, bool Versioned) Split(string alternative)
    {
        var versioned = alternative.Length > 0 && alternative[alternative.Length - 1] == '~';
        var start = alternative.Length > 0 && alternative[0] == '@' ? 1 : 0;
        var end = alternative.IndexOf('(');
        if (end < 0) end = alternative.Length - (versioned ? 1 : 0);
        return (alternative.Substring(start, end - start), versioned);
    }

    // The projection an alternative reads through, without `:`, or "" for the full view.
    internal static string Projection(string alternative)
    {
        var open = alternative.IndexOf('(');
        if (open < 0) return "";
        var close = alternative.IndexOf(')', open);
        var start = open + 1 < alternative.Length && alternative[open + 1] == ':' ? open + 2 : open + 1;
        return close < 0 ? alternative.Substring(start) : alternative.Substring(start, close - start);
    }

    // Whether any alternative reads through a projection.
    internal static bool HasProjection(string typeName) => typeName.IndexOf('(') >= 0;

    // An alternative as written in source: a handle type as is, any other type with its `:` and its capitalized name,
    // such as `:Number` for the binder's `number`.
    internal static string SourceName(string alternative)
        => alternative.Length == 0 || alternative[0] == '@' ? alternative : ":" + char.ToUpperInvariant(alternative[0]) + alternative.Substring(1);

    // The alternatives' names without `@`, in source order.
    internal static string[] Names(string typeName)
    {
        var alternatives = typeName.Split('|');
        var names = new string[alternatives.Length];
        for (var index = 0; index < alternatives.Length; index++) names[index] = Split(alternatives[index]).Name;
        return names;
    }

    // The root type all alternatives share, or null when one is no declared composite or the roots differ.
    internal static GesHandleType? CommonRoot(string typeName, IReadOnlyDictionary<string, GesHandleType> handleTypes)
    {
        GesHandleType? common = null;
        foreach (var alternative in typeName.Split('|'))
        {
            if (!handleTypes.TryGetValue(Split(alternative).Name, out var type) || type.Root is not { } root) return null;
            if (common is not null && !string.Equals(common.Name, root.Name, StringComparison.Ordinal)) return null;
            common = root;
        }

        return common;
    }
}
