// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The Swift type of a model catalog (specs/Schemas.md#model-catalogs): a final class that reads a GesCatalog of its handle type,
// converts every entry's state to the model once and keeps the untyped catalog's order, so that lookups search the typed
// entries by UTF-16 id like GesCatalog does. It is the resolver of its type by delegating to the untyped catalog. Like
// GesCatalog, whose values are not Sendable, it is not Sendable.
extension GesSwiftModelEmitter {
    // `set` is the throwing expression of the schema set that declares the handle type.
    static func catalog(_ code: inout Code, _ catalog: GesModelCatalog, _ access: String, _ set: String) {
        let handle = catalog.handle
        let name = catalog.name
        let state = handle.state
        let handleType = GesModelPlanner.handleName(handle.name)
        let dependencies = catalog.dependencies.map { (name: $0, label: label($0), use: member($0), type: $0 + "Catalog") }
        let parameters = dependencies.map { ", \($0.label): \($0.type)? = nil" }.joined()
        let passed = dependencies.isEmpty ? "" : ", dependencies: dependencies(" + dependencies.map { "\($0.label): \($0.use)" }.joined(separator: ", ") + ")"
        let literal = GesSchemaPlanner.literal(handle.name, swift: true)
        code.line("/// The catalog of `@\(handle.name)`: the entries of `:\(state)` read from GES catalog documents, sorted by id in UTF-16")
        code.line("/// order. It is the handle resolver of its type: register it with a host for `catalog.type`.")
        code.open("\(access)final class \(name): GesHandleResolver {")
        code.line("/// One entry of the catalog: its id, its handle, its state and where it is declared.")
        code.open("\(access)struct Entry {")
        code.line("/// The id, such as `drone.scout`.")
        code.line("\(access)let id: String")
        code.line("/// The handle of the entry, without a version.")
        code.line("\(access)let handle: \(handleType)")
        code.line("/// The state.")
        code.line("\(access)let value: \(state)")
        code.line("/// Where the entry is declared.")
        code.line("\(access)let origin: GesSourceLocation")
        code.close()
        code.blank()
        code.line("/// The untyped catalog, whose type is the handle type a host registers this catalog for.")
        code.line("\(access)let catalog: GesCatalog")
        code.line("/// Every entry, sorted by id.")
        code.line("\(access)let entries: [Entry]")
        code.line("/// The handle of every entry, sorted by id.")
        code.line("\(access)let index: [\(handleType)]")
        for projection in catalog.projections { code.line("private let \(GesSchemaPlanner.camel(projection.name))Index: [\(handleType)]") }
        code.blank()
        code.line("/// The roots a catalog read from handles was read from, each with the version it was read at; empty for named roots.")
        code.line("\(access)var sources: [GesHandle] { catalog.sources }")
        code.blank()
        code.open("private init(_ catalog: GesCatalog) throws {")
        code.line("var entries: [Entry] = []")
        code.line("entries.reserveCapacity(catalog.entries.count)")
        code.open("for entry in catalog.entries {")
        code.line("guard let handle = \(handleType)(matching: entry.handle) else { throw GesApiError.invalidArgument(\"The entry \\(entry.id) has no @\(handle.name) handle.\") }")
        code.line("entries.append(Entry(id: entry.id, handle: handle, value: try \(state)(value: entry.value), origin: entry.origin))")
        code.close()
        code.line("self.catalog = catalog")
        code.line("self.entries = entries")
        code.line("self.index = entries.map(\\.handle)")
        // The handle of every entry with the version of each projection, in the order of the entries.
        for projection in catalog.projections {
            let field = GesSchemaPlanner.camel(projection.name) + "Index"
            code.line("var \(field): [\(handleType)] = []")
            code.open("for handle in try catalog.index(\(set).schema(\(GesSchemaPlanner.literal(projection.lookup, swift: true)))) {")
            code.line("guard let typed = \(handleType)(matching: handle) else { throw GesApiError.invalidArgument(\"The handle \\(handle) is no @\(handle.name) handle.\") }")
            code.line("\(field).append(typed)")
            code.close()
            code.line("self.\(field) = \(field)")
        }
        code.close()
        code.blank()
        code.line("/// The entry with an id, or nil when the catalog has none with that id.")
        code.open("\(access)subscript(_ id: String) -> Entry? {")
        code.line("let index = lowerBound(id)")
        code.line("return index < entries.count && entries[index].id.utf16.elementsEqual(id.utf16) ? entries[index] : nil")
        code.close()
        code.blank()
        code.line("/// Returns the entries of a module: those whose id starts with the module name and `.`, sorted by id.")
        code.line("/// - Parameter name: The module name, such as `drone`.")
        code.line("/// - Returns: The module's entries; empty when it has none.")
        code.open("\(access)func module(_ name: String) -> [Entry] {")
        code.line("let prefix = name + \".\"")
        code.line("let start = lowerBound(prefix)")
        code.line("var end = start")
        code.line("while end < entries.count && entries[end].id.utf16.starts(with: prefix.utf16) { end += 1 }")
        code.line("return Array(entries[start..<end])")
        code.close()
        for projection in catalog.projections {
            let field = GesSchemaPlanner.camel(projection.name) + "Index"
            code.blank()
            code.line("/// The handle of every entry with the version of the projection `:\(projection.name)`, sorted by id: the content hash of the")
            code.line("/// projected state for a type with content hashes, the entry's version otherwise.")
            code.open("\(access)func index(_ projection: \(projection.name).Type) -> [\(handleType)] {")
            code.line(field)
            code.close()
            code.blank()
            code.line("/// Returns the handles of a module's entries with the version of the projection `:\(projection.name)`, sorted by id.")
            code.line("/// - Parameters:")
            code.line("///   - name: The module name, such as `drone`.")
            code.line("///   - projection: `\(projection.name).self`.")
            code.line("/// - Returns: The module's projected handles; empty when it has none.")
            code.open("\(access)func module(_ name: String, _ projection: \(projection.name).Type) -> [\(handleType)] {")
            code.line("let prefix = name + \".\"")
            code.line("let start = lowerBound(prefix)")
            code.line("var end = start")
            code.line("while end < entries.count && entries[end].id.utf16.starts(with: prefix.utf16) { end += 1 }")
            code.line("return Array(\(field)[start..<end])")
            code.close()
        }
        code.blank()
        catalogReadDocumentation(&code, dependencies.map { ($0.name, $0.label) }, files: true)
        code.open("\(access)static func read(roots: [GesCatalogRoot], files: some GesCatalogFiles\(parameters)) throws -> \(name) {")
        code.line("try \(name)(GesCatalog.read(try \(set).handleType(\(literal)), roots: roots, files: files\(passed)))")
        code.close()
        code.blank()
        catalogReadDocumentation(&code, dependencies.map { ($0.name, $0.label) }, files: false)
        code.open("\(access)static func read(roots: [GesHandle], resolver: some GesHandleResolver\(parameters)) throws -> \(name) {")
        code.line("try \(name)(GesCatalog.read(try \(set).handleType(\(literal)), roots: roots, resolver: resolver\(passed)))")
        code.close()
        code.blank()
        code.line("/// Answers a read of an entry's state: its state, with its content hash for a type that declares one, or missing.")
        code.line("/// - Parameter call: The call holding the handle and receiving the answer.")
        code.open("\(access)func resolve(_ call: GesHandleResolveCall) throws {")
        code.line("try catalog.resolve(call)")
        code.close()
        if !dependencies.isEmpty {
            code.blank()
            code.open("private static func dependencies(\(dependencies.map { "\($0.label): \($0.type)?" }.joined(separator: ", "))) -> [GesCatalog] {")
            code.line("var dependencies: [GesCatalog] = []")
            for dependency in dependencies { code.line("if let \(dependency.use) { dependencies.append(\(dependency.use).catalog) }") }
            code.line("return dependencies")
            code.close()
        }
        code.blank()
        code.open("private func lowerBound(_ id: String) -> Int {")
        code.line("var low = 0")
        code.line("var high = entries.count")
        code.open("while low < high {")
        code.line("let middle = (low + high) / 2")
        code.open("if entries[middle].id.utf16.lexicographicallyPrecedes(id.utf16) {")
        code.line("low = middle + 1")
        code.close("} else {")
        code.line("    high = middle")
        code.line("}")
        code.close()
        code.line("return low")
        code.close()
        code.close()
    }

    private static func catalogReadDocumentation(_ code: inout Code, _ dependencies: [(name: String, label: String)], files: Bool) {
        code.line(files ? "/// Reads the catalog from named roots, whose files a source supplies." : "/// Reads the catalog from root handles, whose files a resolver answers.")
        code.line("///")
        code.line("/// - Parameters:")
        code.line("///   - roots: The roots, read in this order.")
        code.line(files ? "///   - files: The source of every file's text." : "///   - resolver: The resolver that answers the root files and their imports.")
        for dependency in dependencies {
            let label = dependency.label.filter { $0 != "`" }
            code.line("///   - \(label): The catalog of `@\(dependency.name)`, whose entries this catalog's entries may name or pin; none when nil.")
        }
        code.line("/// - Returns: The catalog.")
        code.line("/// - Throws: `GesApiError.invalidArgument` when two roots share a name or a root is no catalog file of the type; `GesCatalogError`")
        if files {
            code.line("///   listing every error when the catalog has errors; `GesSchemaError` when the embedded schema sources no longer parse.")
        } else {
            code.line("///   listing every error when the catalog has errors; `GesHandleError` when the resolver answers a file wrongly; `GesSchemaError`")
            code.line("///   when the embedded schema sources no longer parse.")
        }
    }
}
