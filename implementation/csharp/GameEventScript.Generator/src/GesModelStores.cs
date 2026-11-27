// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// A typed store of a handle type whose state is a model root, whose id is Text and that is not composite
// (specs/Schemas.md#model-stores): the type `<Name>Store` and the handle types of the other stores its state reaches,
// which it takes as optional dependencies, ordered by name.
internal sealed class GesModelStore
{
    internal string Name = "";
    internal GesModelHandle Handle = null!;
    internal List<string> Dependencies = [];

    // The projections of the state that the generation declares, by name, each with the name its schema set looks it up by.
    internal List<(string Name, string Lookup)> Projections = [];
}

internal static partial class GesModelPlanner
{
    // The stores of a layout, in the order of their handle types, which is by name.
    internal static List<GesModelStore> Stores(GesModelLayout layout, HashSet<string> topLevel)
    {
        var typed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in layout.Handles)
            if (!handle.IsComposite && handle.Id.IsText && layout.Roots.Contains(handle.State)
                && layout.Types.Exists(type => type.Name == handle.State && type.Kind == GesSchemaKind.Schema))
                typed.Add(handle.Name);
        var stores = new List<GesModelStore>();
        foreach (var handle in layout.Handles)
        {
            if (!typed.Contains(handle.Name)) continue;
            var name = handle.Name + "Store";
            if (topLevel.Contains(name))
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The store type '{name}' of @{handle.Name} takes the name of a generated type.");
            // The store declares its entry type Entry, which would hide a state type of that name inside it.
            if (handle.State == "Entry")
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The store type '{name}' declares the type Entry, which hides the state type :Entry.");
            var reached = new SortedSet<string>(StringComparer.Ordinal);
            Reach(layout, layout.Type(handle.State), new HashSet<string>(StringComparer.Ordinal), reached);
            reached.Remove(handle.Name);
            reached.IntersectWith(typed);
            foreach (var dependency in reached)
                if (GesSchemaPlanner.Camel(dependency) is "roots" or "files" or "resolver")
                    throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The store type '{name}' takes the store of @{dependency} as the parameter '{GesSchemaPlanner.Camel(dependency)}', which its reading already names.");
            stores.Add(new GesModelStore { Name = name, Handle = handle, Dependencies = [.. reached], Projections = handle.Projections.ConvertAll(projection => (projection, layout.Type(projection).Lookup)) });
        }

        return stores;
    }

    // The handle types a type reaches through its fields, List elements, union alternatives and Map keys, at any depth.
    private static void Reach(GesModelLayout layout, GesModelType type, HashSet<string> visited, ISet<string> handles)
    {
        if (!visited.Add(type.Name)) return;
        foreach (var field in type.Fields) Reach(layout, field.Value, visited, handles);
    }

    private static void Reach(GesModelLayout layout, GesModelValue value, HashSet<string> visited, ISet<string> handles)
    {
        if (value.Kind is GesModelKind.Handle or GesModelKind.HandleMap) handles.Add(value.Handle);
        if (value.Element is { } element) Reach(layout, element.Value, visited, handles);
        foreach (var alternative in value.Alternatives) Reach(layout, alternative.Value, visited, handles);
        if (value.Kind == GesModelKind.Schema && value.Schema.Length > 0) Reach(layout, layout.Type(value.Schema), visited, handles);
    }
}
