// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using GameEventScript.Runtime.Values;

namespace GameEventScript.CSharpBridge;

/// <summary>Provides C# dictionary factories for portable immutable values.</summary>
public static class GameEventScriptCSharpValue
{
    /// <summary>Creates an immutable map from dictionary entries using the portable factory's canonical Unicode scalar key order.</summary>
    /// <param name="entries">The entries to snapshot. Null or an empty dictionary creates an empty map.</param>
    /// <returns>A portable map independent of subsequent changes to the dictionary.</returns>
    public static GesValue GesMap(IReadOnlyDictionary<string, GesValue>? entries)
    {
        var (keys, values) = CopyEntries(entries);
        return GesValue.GesMap(keys, values);
    }

    /// <summary>Creates an immutable record from dictionary fields using the portable factory's canonical Unicode scalar key order.</summary>
    /// <param name="typeName">The record type name.</param>
    /// <param name="fields">The fields to snapshot. Null or an empty dictionary creates a record with no fields.</param>
    /// <returns>A portable record independent of subsequent changes to the dictionary.</returns>
    public static GesValue GesRecord(string typeName, IReadOnlyDictionary<string, GesValue>? fields)
    {
        var (keys, values) = CopyEntries(fields);
        return GesValue.GesRecord(typeName, keys, values);
    }

    private static (string[] Keys, GesValue[] Values) CopyEntries(IReadOnlyDictionary<string, GesValue>? entries)
    {
        if (entries is null || entries.Count == 0) return ([], []);
        var keys = new string[entries.Count];
        var values = new GesValue[entries.Count];
        var index = 0;
        foreach (var entry in entries)
        {
            keys[index] = entry.Key;
            values[index] = entry.Value;
            index++;
        }

        return (keys, values);
    }
}
