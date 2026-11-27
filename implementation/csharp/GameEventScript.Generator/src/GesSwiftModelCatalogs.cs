// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// The Swift type of a model catalog (specs/Schemas.md#model-catalogs): a final class that reads a GesCatalog of its handle type,
// converts every entry's state to the model once and keeps the untyped catalog's order, so that lookups search the typed
// entries by UTF-16 id like GesCatalog does. It is the resolver of its type by delegating to the untyped catalog. Like
// GesCatalog, whose values are not Sendable, it is not Sendable.
internal static partial class GesSwiftModelEmitter
{
    // `set` is the throwing expression of the schema set that declares the handle type.
    internal static void Catalog(GesSchemaPlanner.Code code, GesModelCatalog catalog, string access, string set)
    {
        var handle = catalog.Handle;
        var name = catalog.Name;
        var state = handle.State;
        var handleType = GesModelPlanner.HandleName(handle.Name);
        var dependencies = catalog.Dependencies.ConvertAll(dependency => (Name: dependency, Label: Label(dependency), Use: Member(dependency), Type: dependency + "Catalog"));
        var parameters = string.Concat(dependencies.ConvertAll(dependency => $", {dependency.Label}: {dependency.Type}? = nil"));
        var passed = dependencies.Count == 0 ? "" : ", dependencies: dependencies(" + string.Join(", ", dependencies.ConvertAll(dependency => $"{dependency.Label}: {dependency.Use}")) + ")";
        code.Line($"/// The catalog of `@{handle.Name}`: the entries of `:{state}` read from GES catalog documents, sorted by id in UTF-16");
        code.Line("/// order. It is the handle resolver of its type: register it with a host for `catalog.type`.");
        code.Open($"{access}final class {name}: GesHandleResolver {{");
        code.Line("/// One entry of the catalog: its id, its handle, its state and where it is declared.");
        code.Open($"{access}struct Entry {{");
        code.Line("/// The id, such as `drone.scout`.");
        code.Line($"{access}let id: String");
        code.Line("/// The handle of the entry, without a version.");
        code.Line($"{access}let handle: {handleType}");
        code.Line("/// The state.");
        code.Line($"{access}let value: {state}");
        code.Line("/// Where the entry is declared.");
        code.Line($"{access}let origin: GesSourceLocation");
        code.Close();
        code.Blank();
        code.Line("/// The untyped catalog, whose type is the handle type a host registers this catalog for.");
        code.Line($"{access}let catalog: GesCatalog");
        code.Line("/// Every entry, sorted by id.");
        code.Line($"{access}let entries: [Entry]");
        code.Line("/// The handle of every entry, sorted by id.");
        code.Line($"{access}let index: [{handleType}]");
        foreach (var projection in catalog.Projections) code.Line($"private let {GesSchemaPlanner.Camel(projection.Name)}Index: [{handleType}]");
        code.Blank();
        code.Line("/// The roots a catalog read from handles was read from, each with the version it was read at; empty for named roots.");
        code.Line($"{access}var sources: [GesHandle] {{ catalog.sources }}");
        code.Blank();
        code.Open("private init(_ catalog: GesCatalog) throws {");
        code.Line("var entries: [Entry] = []");
        code.Line("entries.reserveCapacity(catalog.entries.count)");
        code.Open("for entry in catalog.entries {");
        code.Line($"guard let handle = {handleType}(matching: entry.handle) else {{ throw GesApiError.invalidArgument(\"The entry \\(entry.id) has no @{handle.Name} handle.\") }}");
        code.Line($"entries.append(Entry(id: entry.id, handle: handle, value: try {state}(value: entry.value), origin: entry.origin))");
        code.Close();
        code.Line("self.catalog = catalog");
        code.Line("self.entries = entries");
        code.Line("self.index = entries.map(\\.handle)");
        // The handle of every entry with the version of each projection, in the order of the entries.
        foreach (var projection in catalog.Projections)
        {
            var field = GesSchemaPlanner.Camel(projection.Name) + "Index";
            code.Line($"var {field}: [{handleType}] = []");
            code.Open($"for handle in try catalog.index({set}.schema({GesSchemaPlanner.Literal(projection.Lookup, true)})) {{");
            code.Line($"guard let typed = {handleType}(matching: handle) else {{ throw GesApiError.invalidArgument(\"The handle \\(handle) is no @{handle.Name} handle.\") }}");
            code.Line($"{field}.append(typed)");
            code.Close();
            code.Line($"self.{field} = {field}");
        }

        code.Close();
        code.Blank();
        code.Line("/// The entry with an id, or nil when the catalog has none with that id.");
        code.Open($"{access}subscript(_ id: String) -> Entry? {{");
        code.Line("let index = lowerBound(id)");
        code.Line("return index < entries.count && entries[index].id.utf16.elementsEqual(id.utf16) ? entries[index] : nil");
        code.Close();
        code.Blank();
        code.Line("/// Returns the entries of a module: those whose id starts with the module name and `.`, sorted by id.");
        code.Line("/// - Parameter name: The module name, such as `drone`.");
        code.Line("/// - Returns: The module's entries; empty when it has none.");
        code.Open($"{access}func module(_ name: String) -> [Entry] {{");
        code.Line("let prefix = name + \".\"");
        code.Line("let start = lowerBound(prefix)");
        code.Line("var end = start");
        code.Line("while end < entries.count && entries[end].id.utf16.starts(with: prefix.utf16) { end += 1 }");
        code.Line("return Array(entries[start..<end])");
        code.Close();
        foreach (var projection in catalog.Projections)
        {
            var field = GesSchemaPlanner.Camel(projection.Name) + "Index";
            code.Blank();
            code.Line($"/// The handle of every entry with the version of the projection `:{projection.Name}`, sorted by id: the content hash of the");
            code.Line("/// projected state for a type with content hashes, the entry's version otherwise.");
            code.Open($"{access}func index(_ projection: {projection.Name}.Type) -> [{handleType}] {{");
            code.Line(field);
            code.Close();
            code.Blank();
            code.Line($"/// Returns the handles of a module's entries with the version of the projection `:{projection.Name}`, sorted by id.");
            code.Line("/// - Parameters:");
            code.Line("///   - name: The module name, such as `drone`.");
            code.Line($"///   - projection: `{projection.Name}.self`.");
            code.Line("/// - Returns: The module's projected handles; empty when it has none.");
            code.Open($"{access}func module(_ name: String, _ projection: {projection.Name}.Type) -> [{handleType}] {{");
            code.Line("let prefix = name + \".\"");
            code.Line("let start = lowerBound(prefix)");
            code.Line("var end = start");
            code.Line("while end < entries.count && entries[end].id.utf16.starts(with: prefix.utf16) { end += 1 }");
            code.Line($"return Array({field}[start..<end])");
            code.Close();
        }
        code.Blank();
        ReadDocumentation(code, dependencies.ConvertAll(dependency => (dependency.Name, dependency.Label)), files: true);
        code.Open($"{access}static func read(roots: [GesCatalogRoot], files: some GesCatalogFiles{parameters}) throws -> {name} {{");
        code.Line($"try {name}(GesCatalog.read(try {set}.handleType({GesSchemaPlanner.Literal(handle.Name, true)}), roots: roots, files: files{passed}))");
        code.Close();
        code.Blank();
        ReadDocumentation(code, dependencies.ConvertAll(dependency => (dependency.Name, dependency.Label)), files: false);
        code.Open($"{access}static func read(roots: [GesHandle], resolver: some GesHandleResolver{parameters}) throws -> {name} {{");
        code.Line($"try {name}(GesCatalog.read(try {set}.handleType({GesSchemaPlanner.Literal(handle.Name, true)}), roots: roots, resolver: resolver{passed}))");
        code.Close();
        code.Blank();
        code.Line("/// Answers a read of an entry's state: its state, with its content hash for a type that declares one, or missing.");
        code.Line("/// - Parameter call: The call holding the handle and receiving the answer.");
        code.Open($"{access}func resolve(_ call: GesHandleResolveCall) throws {{");
        code.Line("try catalog.resolve(call)");
        code.Close();
        if (dependencies.Count > 0)
        {
            code.Blank();
            code.Open($"private static func dependencies({string.Join(", ", dependencies.ConvertAll(dependency => $"{dependency.Label}: {dependency.Type}?"))}) -> [GesCatalog] {{");
            code.Line("var dependencies: [GesCatalog] = []");
            foreach (var dependency in dependencies) code.Line($"if let {dependency.Use} {{ dependencies.append({dependency.Use}.catalog) }}");
            code.Line("return dependencies");
            code.Close();
        }

        code.Blank();
        code.Open("private func lowerBound(_ id: String) -> Int {");
        code.Line("var low = 0");
        code.Line("var high = entries.count");
        code.Open("while low < high {");
        code.Line("let middle = (low + high) / 2");
        code.Open("if entries[middle].id.utf16.lexicographicallyPrecedes(id.utf16) {");
        code.Line("low = middle + 1");
        code.Close("} else {");
        code.Line("    high = middle");
        code.Line("}");
        code.Close();
        code.Line("return low");
        code.Close();
        code.Close();
    }

    private static void ReadDocumentation(GesSchemaPlanner.Code code, List<(string Name, string Label)> dependencies, bool files)
    {
        code.Line(files ? "/// Reads the catalog from named roots, whose files a source supplies." : "/// Reads the catalog from root handles, whose files a resolver answers.");
        code.Line("///");
        code.Line("/// - Parameters:");
        code.Line("///   - roots: The roots, read in this order.");
        code.Line(files ? "///   - files: The source of every file's text." : "///   - resolver: The resolver that answers the root files and their imports.");
        foreach (var dependency in dependencies)
            code.Line($"///   - {dependency.Label.Trim('`')}: The catalog of `@{dependency.Name}`, whose entries this catalog's entries may name or pin; none when nil.");
        code.Line("/// - Returns: The catalog.");
        code.Line("/// - Throws: `GesApiError.invalidArgument` when two roots share a name or a root is no catalog file of the type; `GesCatalogError`");
        if (files)
        {
            code.Line("///   listing every error when the catalog has errors; `GesSchemaError` when the embedded schema sources no longer parse.");
            return;
        }

        code.Line("///   listing every error when the catalog has errors; `GesHandleError` when the resolver answers a file wrongly; `GesSchemaError`");
        code.Line("///   when the embedded schema sources no longer parse.");
    }
}
