// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// The C# type of a model catalog (specs/Schemas.md#model-catalogs): a sealed class that reads a GesCatalog of its handle type,
// converts every entry's state to the model once and keeps the untyped catalog's order, so that lookups search the typed
// entries by ordinal id like GesCatalog does. It is the resolver of its type by delegating to the untyped catalog.
internal static partial class GesCSharpModelEmitter
{
    // `set` is the expression of the schema set that declares the handle type; `qualifier` qualifies model types.
    internal static void Catalog(GesSchemaPlanner.Code code, GesModelCatalog catalog, string access, string qualifier, string set)
    {
        var handle = catalog.Handle;
        var name = catalog.Name;
        var state = qualifier + handle.State;
        var handleType = qualifier + GesModelPlanner.HandleName(handle.Name);
        var dependencies = catalog.Dependencies.ConvertAll(dependency => (Name: dependency, Parameter: Parameter(dependency), Type: qualifier + dependency + "Catalog"));
        var parameters = string.Concat(dependencies.ConvertAll(dependency => $", {dependency.Type}? {dependency.Parameter} = null"));
        var passed = dependencies.Count == 0 ? "null" : "__Dependencies(" + string.Join(", ", dependencies.ConvertAll(dependency => dependency.Parameter)) + ")";
        code.Line($"/// <summary>The catalog of <c>@{handle.Name}</c>: the entries of <c>:{handle.State}</c> read from GES catalog documents, sorted by id in");
        code.Line("/// ordinal order. It is the handle resolver of its type: register it with a host for the type of <see cref=\"Catalog\"/>.</summary>");
        code.Line($"{access} sealed class {name} : IGesHandleResolver");
        code.Open("{");
        code.Line("private readonly Entry[] _entries;");
        code.Line($"private readonly {handleType}[] _index;");
        foreach (var projection in catalog.Projections) code.Line($"private readonly {handleType}[] _{GesSchemaPlanner.Camel(projection.Name)}Index;");
        code.Blank();
        code.Line($"private {name}(GesCatalog catalog)");
        code.Open("{");
        code.Line("Catalog = catalog;");
        code.Line("var entries = catalog.Entries;");
        code.Line("_entries = new Entry[entries.Count];");
        code.Line($"_index = new {handleType}[entries.Count];");
        code.Line("for (var index = 0; index < _entries.Length; index++)");
        code.Open("{");
        code.Line("var entry = entries[index];");
        code.Line($"var handle = {handleType}.FromHandle(entry.Handle);");
        code.Line($"_entries[index] = new Entry(entry.Id, handle, {state}.FromValue(entry.Value), entry.Origin);");
        code.Line("_index[index] = handle;");
        code.Close();
        foreach (var projection in catalog.Projections)
        {
            var field = "_" + GesSchemaPlanner.Camel(projection.Name) + "Index";
            var local = GesSchemaPlanner.Camel(projection.Name);
            code.Blank();
            code.Line($"var {local} = catalog.Index({set}.Schema({GesSchemaPlanner.Literal(projection.Lookup, false)}));");
            code.Line($"{field} = new {handleType}[{local}.Count];");
            code.Line($"for (var index = 0; index < {field}.Length; index++) {field}[index] = {handleType}.FromHandle({local}[index]);");
        }

        code.Close();
        code.Blank();
        code.Line("/// <summary>Gets the untyped catalog, whose type is the handle type a host registers this catalog for.</summary>");
        code.Line("public GesCatalog Catalog { get; }");
        code.Blank();
        code.Line("/// <summary>Gets the roots a catalog read from handles was read from, each with the version it was read at; empty for named roots.</summary>");
        code.Line("public IReadOnlyList<GesHandle> Sources => Catalog.Sources;");
        code.Blank();
        code.Line("/// <summary>Gets every entry, sorted by id.</summary>");
        code.Line("public IReadOnlyList<Entry> Entries => _entries;");
        code.Blank();
        code.Line("/// <summary>Gets the handle of every entry, sorted by id.</summary>");
        code.Line($"public IReadOnlyList<{handleType}> Index => _index;");
        code.Blank();
        code.Line("/// <summary>Gets the entry with an id.</summary>");
        code.Line("/// <param name=\"id\">The id, such as <c>drone.scout</c>.</param>");
        code.Line("/// <returns>The entry, or null when the catalog has none with that id.</returns>");
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
        foreach (var projection in catalog.Projections)
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
        code.Line($"public static {name} Read(IReadOnlyList<GesCatalogRoot> roots, IGesCatalogFiles files{parameters})");
        code.Line($"    => new {name}(GesCatalog.Read(__HandleType, roots, files, {passed}));");
        code.Blank();
        ReadDocumentation(code, dependencies.ConvertAll(dependency => (dependency.Name, dependency.Parameter)), files: false);
        code.Line($"public static {name} Read(IReadOnlyList<GesHandle> roots, IGesHandleResolver resolver{parameters})");
        code.Line($"    => new {name}(GesCatalog.Read(__HandleType, roots, resolver, {passed}));");
        code.Blank();
        code.Line("/// <summary>Answers a read of an entry's state: its state, with its content hash for a type that declares one, or missing.</summary>");
        code.Line("/// <param name=\"call\">The call holding the handle and receiving the answer.</param>");
        code.Line("public void Resolve(GesHandleResolveCall call) => Catalog.Resolve(call);");
        code.Blank();
        code.Line($"private static GesHandleType __HandleType => {set}.HandleType({GesSchemaPlanner.Literal(handle.Name, false)});");
        if (dependencies.Count > 0)
        {
            code.Blank();
            code.Line($"private static IReadOnlyList<GesCatalog> __Dependencies({string.Join(", ", dependencies.ConvertAll(dependency => $"{dependency.Type}? {dependency.Parameter}"))})");
            code.Open("{");
            code.Line("var dependencies = new List<GesCatalog>();");
            foreach (var dependency in dependencies) code.Line($"if ({dependency.Parameter} is not null) dependencies.Add({dependency.Parameter}.Catalog);");
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
        code.Line($"/// <summary>One entry of the catalog: its id, its handle, its state and where it is declared.</summary>");
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
        code.Line(files ? "/// <summary>Reads the catalog from named roots, whose files a source supplies.</summary>" : "/// <summary>Reads the catalog from root handles, whose files a resolver answers.</summary>");
        code.Line("/// <param name=\"roots\">The roots, read in this order.</param>");
        code.Line(files ? "/// <param name=\"files\">The source of every file's text.</param>" : "/// <param name=\"resolver\">The resolver that answers the root files and their imports.</param>");
        foreach (var dependency in dependencies)
            code.Line($"/// <param name=\"{dependency.Parameter.TrimStart('@')}\">The catalog of <c>@{dependency.Name}</c>, whose entries this catalog's entries may name or pin; none when null.</param>");
        code.Line("/// <returns>The catalog.</returns>");
        code.Line("/// <exception cref=\"ArgumentNullException\">Thrown when the roots, a root or the " + (files ? "files" : "resolver") + " are null.</exception>");
        code.Line("/// <exception cref=\"ArgumentException\">Thrown when two roots share a name or a root is no catalog file of the type.</exception>");
        code.Line("/// <exception cref=\"GesCatalogException\">Thrown when the catalog has errors; the exception lists every one.</exception>");
        if (!files) code.Line("/// <exception cref=\"GesHandleException\">Thrown when the resolver answers a file wrongly.</exception>");
    }
}
