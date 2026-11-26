// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

// Static handle type names as the binder keeps them: `@Name`, `@Name~` and unions joined with `|` (specs/Language.md#handles).
internal static class GesHandleTypeNames
{
    // Whether a declared or static type names a handle type or a union, which is only of handle types.
    internal static bool IsHandleType(string? typeName)
        => !string.IsNullOrEmpty(typeName) && (typeName![0] == '@' || typeName.IndexOf('|') >= 0);

    // The name without `@` and whether `~` requires a version.
    internal static (string Name, bool Versioned) Split(string alternative)
    {
        var versioned = alternative.Length > 0 && alternative[alternative.Length - 1] == '~';
        var start = alternative.Length > 0 && alternative[0] == '@' ? 1 : 0;
        return (alternative.Substring(start, alternative.Length - start - (versioned ? 1 : 0)), versioned);
    }

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
