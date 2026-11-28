// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// The C# type of a model store (specs/Schemas.md#model-stores): a sealed class that reads a GesStore of its handle type,
// converts every entry's state to the model once and keeps the untyped store's order, so that lookups search the typed
// entries by ordinal id like GesStore does. It is the resolver of its type by delegating to the untyped store.
internal static partial class GesCSharpModelEmitter
{
    // `set` is the expression of the schema set that declares the handle type; `qualifier` qualifies model types.
    internal static void Store(GesSchemaPlanner.Code code, GesModelStore store, string access, string qualifier, string set)
    {
        var handle = store.Handle;
        var name = store.Name;
        var state = qualifier + handle.State;
        var handleType = qualifier + GesModelPlanner.HandleName(handle.Name);
        var dependencies = store.Dependencies.ConvertAll(dependency => (Name: dependency, Parameter: Parameter(dependency), Type: qualifier + dependency + "Store"));
        var parameters = string.Concat(dependencies.ConvertAll(dependency => $", {dependency.Type}? {dependency.Parameter} = null"));
        var passed = dependencies.Count == 0 ? "null" : "__Dependencies(" + string.Join(", ", dependencies.ConvertAll(dependency => dependency.Parameter)) + ")";
        code.Line($"/// <summary>The store of <c>@{handle.Name}</c>: the entries of <c>:{handle.State}</c> read from GES store documents, sorted by id in");
        code.Line("/// ordinal order. It is the handle resolver of its type: register it with a host for the type of <see cref=\"Store\"/>.</summary>");
        code.Line($"{access} sealed class {name} : IGesHandleResolver");
        code.Open("{");
        code.Line("private readonly Entry[] _entries;");
        code.Line($"private readonly {handleType}[] _index;");
        foreach (var projection in store.Projections) code.Line($"private readonly {handleType}[] _{GesSchemaPlanner.Camel(projection.Name)}Index;");
        code.Blank();
        code.Line($"private {name}(GesStore store)");
        code.Open("{");
        code.Line("Store = store;");
        code.Line("var entries = store.Entries;");
        code.Line("_entries = new Entry[entries.Count];");
        code.Line($"_index = new {handleType}[entries.Count];");
        code.Line("for (var index = 0; index < _entries.Length; index++)");
        code.Open("{");
        code.Line("var entry = entries[index];");
        code.Line($"var handle = {handleType}.FromHandle(entry.Handle);");
        code.Line($"_entries[index] = new Entry(entry.Id, handle, {state}.FromValue(entry.Value), entry.Origin);");
        code.Line("_index[index] = handle;");
        code.Close();
        foreach (var projection in store.Projections)
        {
            var field = "_" + GesSchemaPlanner.Camel(projection.Name) + "Index";
            var local = GesSchemaPlanner.Camel(projection.Name);
            code.Blank();
            code.Line($"var {local} = store.Index({set}.Schema({GesSchemaPlanner.Literal(projection.Lookup, false)}));");
            code.Line($"{field} = new {handleType}[{local}.Count];");
            code.Line($"for (var index = 0; index < {field}.Length; index++) {field}[index] = {handleType}.FromHandle({local}[index]);");
        }

        code.Close();
        code.Blank();
        code.Line("/// <summary>Gets the untyped store, whose type is the handle type a host registers this store for.</summary>");
        code.Line("public GesStore Store { get; }");
        code.Blank();
        code.Line("/// <summary>Gets the roots a store read from handles was read from, each with the version it was read at; empty for named roots.</summary>");
        code.Line("public IReadOnlyList<GesHandle> Sources => Store.Sources;");
        code.Blank();
        code.Line("/// <summary>Gets every entry, sorted by id.</summary>");
        code.Line("public IReadOnlyList<Entry> Entries => _entries;");
        code.Blank();
        code.Line("/// <summary>Gets the handle of every entry, sorted by id.</summary>");
        code.Line($"public IReadOnlyList<{handleType}> Index => _index;");
        code.Blank();
        code.Line("/// <summary>Gets the entry with an id.</summary>");
        code.Line("/// <param name=\"id\">The id, such as <c>drone.scout</c>.</param>");
        code.Line("/// <returns>The entry, or null when the store has none with that id.</returns>");
        code.Line("/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"id\"/> is null.</exception>");
        code.Line("public Entry? this[string id]");
        code.Open("{");
        code.Line("get");
        code.Open("{");
        code.Line("if (id is null) throw new ArgumentNullException(nameof(id));");
        code.Line("var index = LowerBound(id);");
        code.Line("return index < _entries.Length && string.Equals(_entries[index].Id, id, StringComparison.Ordinal) ? _entries[index] : null;");
        code.Close();
        code.Close();
        code.Blank();
        code.Line("/// <summary>Returns the entries of a module: those whose id starts with the module name and <c>.</c>, sorted by id.</summary>");
        code.Line("/// <param name=\"name\">The module name, such as <c>drone</c>.</param>");
        code.Line("/// <returns>The module's entries; empty when it has none.</returns>");
        code.Line("/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"name\"/> is null.</exception>");
        code.Line("public IReadOnlyList<Entry> Module(string name)");
        code.Open("{");
        code.Line("if (name is null) throw new ArgumentNullException(nameof(name));");
        code.Line("var prefix = name + \".\";");
        code.Line("var start = LowerBound(prefix);");
        code.Line("var end = start;");
        code.Line("while (end < _entries.Length && _entries[end].Id.StartsWith(prefix, StringComparison.Ordinal)) end++;");
        code.Line("var result = new Entry[end - start];");
        code.Line("Array.Copy(_entries, start, result, 0, result.Length);");
        code.Line("return result;");
        code.Close();
        code.Blank();
        foreach (var projection in store.Projections)
        {
            var field = "_" + GesSchemaPlanner.Camel(projection.Name) + "Index";
            code.Line($"/// <summary>Gets the handle of every entry with the version of the projection <c>:{projection.Name}</c>, sorted by id: the content hash of");
            code.Line("/// the projected state for a type with content hashes, the entry's version otherwise.</summary>");
            code.Line($"public IReadOnlyList<{handleType}> {projection.Name}Index => {field};");
            code.Blank();
            code.Line($"/// <summary>Returns the handles of a module's entries with the version of the projection <c>:{projection.Name}</c>, sorted by id.</summary>");
            code.Line("/// <param name=\"name\">The module name, such as <c>drone</c>.</param>");
            code.Line("/// <returns>The module's projected handles; empty when it has none.</returns>");
            code.Line("/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"name\"/> is null.</exception>");
            code.Line($"public IReadOnlyList<{handleType}> {projection.Name}Module(string name)");
            code.Open("{");
            code.Line("if (name is null) throw new ArgumentNullException(nameof(name));");
            code.Line("var prefix = name + \".\";");
            code.Line("var start = LowerBound(prefix);");
            code.Line("var end = start;");
            code.Line("while (end < _entries.Length && _entries[end].Id.StartsWith(prefix, StringComparison.Ordinal)) end++;");
            code.Line($"var result = new {handleType}[end - start];");
            code.Line($"Array.Copy({field}, start, result, 0, result.Length);");
            code.Line("return result;");
            code.Close();
            code.Blank();
        }

        ReadDocumentation(code, dependencies.ConvertAll(dependency => (dependency.Name, dependency.Parameter)), files: true);
        code.Line($"public static {name} Read(IReadOnlyList<GesStoreRoot> roots, IGesStoreFiles files{parameters})");
        code.Line($"    => new {name}(GesStore.Read(__HandleType, roots, files, {passed}));");
        code.Blank();
        ReadDocumentation(code, dependencies.ConvertAll(dependency => (dependency.Name, dependency.Parameter)), files: false);
        code.Line($"public static {name} Read(IReadOnlyList<GesStoreSourceRoot> roots, IGesStoreSource source{parameters})");
        code.Line($"    => new {name}(GesStore.Read(__HandleType, roots, source, {passed}));");
        code.Blank();
        code.Line("/// <summary>Answers a read of an entry's state: its state, with its content hash for a type that declares one, or missing.</summary>");
        code.Line("/// <param name=\"call\">The call holding the handle and receiving the answer.</param>");
        code.Line("public void Resolve(GesHandleResolveCall call) => Store.Resolve(call);");
        code.Blank();
        code.Line($"private static GesHandleType __HandleType => {set}.HandleType({GesSchemaPlanner.Literal(handle.Name, false)});");
        if (dependencies.Count > 0)
        {
            code.Blank();
            code.Line($"private static IReadOnlyList<GesStore> __Dependencies({string.Join(", ", dependencies.ConvertAll(dependency => $"{dependency.Type}? {dependency.Parameter}"))})");
            code.Open("{");
            code.Line("var dependencies = new List<GesStore>();");
            foreach (var dependency in dependencies) code.Line($"if ({dependency.Parameter} is not null) dependencies.Add({dependency.Parameter}.Store);");
            code.Line("return dependencies;");
            code.Close();
        }

        code.Blank();
        code.Line("private int LowerBound(string id)");
        code.Open("{");
        code.Line("int low = 0, high = _entries.Length;");
        code.Line("while (low < high)");
        code.Open("{");
        code.Line("var middle = (low + high) >> 1;");
        code.Line("if (string.CompareOrdinal(_entries[middle].Id, id) < 0) low = middle + 1;");
        code.Line("else high = middle;");
        code.Close();
        code.Line("return low;");
        code.Close();
        code.Blank();
        code.Line($"/// <summary>One entry of the store: its id, its handle, its state and where it is declared.</summary>");
        code.Line("public sealed class Entry");
        code.Open("{");
        code.Line($"internal Entry(string id, {handleType} handle, {state} value, GesSourceLocation origin)");
        code.Open("{");
        code.Line("Id = id;");
        code.Line("Handle = handle;");
        code.Line("Value = value;");
        code.Line("Origin = origin;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Gets the id, such as <c>drone.scout</c>.</summary>");
        code.Line("public string Id { get; }");
        code.Blank();
        code.Line("/// <summary>Gets the handle of the entry, without a version.</summary>");
        code.Line($"public {handleType} Handle {{ get; }}");
        code.Blank();
        code.Line("/// <summary>Gets the state.</summary>");
        code.Line($"public {state} Value {{ get; }}");
        code.Blank();
        code.Line("/// <summary>Gets where the entry is declared.</summary>");
        code.Line("public GesSourceLocation Origin { get; }");
        code.Close();
        code.Close();
    }

    private static void ReadDocumentation(GesSchemaPlanner.Code code, List<(string Name, string Parameter)> dependencies, bool files)
    {
        code.Line(files ? "/// <summary>Reads the store from named roots, whose files a source supplies.</summary>" : "/// <summary>Reads the store from versioned sources: root handles and the paths of their root files, whose files a source answers.</summary>");
        code.Line("/// <param name=\"roots\">The roots, read in this order.</param>");
        code.Line(files ? "/// <param name=\"files\">The source of every file's text.</param>" : "/// <param name=\"source\">The source that answers every file by root handle and path.</param>");
        foreach (var dependency in dependencies)
            code.Line($"/// <param name=\"{dependency.Parameter.TrimStart('@')}\">The store of <c>@{dependency.Name}</c>, whose entries this store's entries may name or pin; none when null.</param>");
        code.Line("/// <returns>The store.</returns>");
        code.Line("/// <exception cref=\"ArgumentNullException\">Thrown when the roots, a root or the " + (files ? "files" : "source") + " are null.</exception>");
        code.Line((files ? "/// <exception cref=\"ArgumentException\">Thrown when two roots share a name.</exception>" : "/// <exception cref=\"ArgumentException\">Thrown when a root handle carries a part or two roots have equal handles.</exception>"));
        code.Line("/// <exception cref=\"GesStoreException\">Thrown when the store has errors; the exception lists every one.</exception>");
        if (!files) code.Line("/// <exception cref=\"GesHandleException\">Thrown when the source answers an invalid or another version of a root.</exception>");
    }
}
