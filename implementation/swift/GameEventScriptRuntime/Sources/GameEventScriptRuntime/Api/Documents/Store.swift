// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// A named root of a store and the root-relative path of its root file (specs/Documents.md#reading-a-store).
public struct GesStoreRoot: Sendable, Hashable {
    /// The root's name, which the host's file source and every origin carry.
    public let name: String
    /// The path of the root file, relative to the root.
    public let path: String

    /// Creates a root.
    /// - Parameters:
    ///   - name: The root's name, which the host's file source and every origin carry.
    ///   - path: The path of the root file, relative to the root.
    public init(name: String, path: String) {
        self.name = name
        self.path = path
    }
}

/// The files of a store's roots. GES reads no files itself: it asks for the text of each file a store reaches.
public protocol GesStoreFiles {
    /// Returns the text of a file of a root.
    /// - Parameters:
    ///   - root: The root's name.
    ///   - path: The root-relative path, segments joined by `/`, without `.` or `..` segments.
    /// - Returns: The file's text, or nil when the root has no such file (`store.importMissing`).
    func read(root: String, path: String) -> String?
}

/// One object of a store: its id, its handle, its checked state and where it is declared.
public struct GesStoreEntry {
    /// The entry's id: its module name, a `.` and its key, or only its key at the root.
    public let id: String
    /// The entry's handle, which carries the content hash of `value` for a `:ContentHash` type.
    public let handle: GesHandle
    /// The state: a Record of the state type with the schema's defaults applied, as a resolver's state.
    public let value: GesValue
    /// The position of the entry's key: `moduleName` is the root, `sourceName` the root-relative path of the file, with
    /// line and column.
    public let origin: GesSourceLocation
}

/// One error of reading a store (specs/Documents.md#reading-a-store).
public struct GesStoreDiagnostic: Sendable, CustomStringConvertible {
    /// The stable code, a `store.*` or `document.*` code.
    public let code: String
    /// A message for people, which is not part of the contract.
    public let message: String
    /// The position: `moduleName` is the root, `sourceName` the root-relative path, and line and column, which a missing
    /// root file has none of.
    public let location: GesSourceLocation
    /// The field path of a schema error, such as `weapons[].range`; otherwise empty.
    public let fieldPath: String
    /// The second position of an error, the first key of a `store.duplicateId`; otherwise nil.
    public let other: GesSourceLocation?
    /// The suggested id of a `store.unknownReference`, or nil when no id is close.
    public let suggestion: String?

    /// The position, the code and the message.
    public var description: String { "\(Self.text(location)): \(code): \(message)" }

    // A location as text: `root@path`, then the line and column when known.
    static func text(_ location: GesSourceLocation) -> String {
        let name = location.moduleName.allSatisfy(\.isWhitespace) ? location.sourceName : "\(location.moduleName)@\(location.sourceName)"
        guard let line = location.line else { return name }
        guard let column = location.column else { return "\(name) (line \(line))" }
        return "\(name) (line \(line), col \(column))"
    }
}

/// A store could not be read; `diagnostics` holds every error found.
public struct GesStoreError: Error, CustomStringConvertible {
    /// The code of a file the host does not have.
    public static let importMissing = "store.importMissing"
    /// The code of an import path that steps above its root.
    public static let importOutsideRoot = "store.importOutsideRoot"
    /// The code of an import of a file on its own import chain.
    public static let importCycle = "store.importCycle"
    /// The code of an imported file of the wrong kind.
    public static let importKind = "store.importKind"
    /// The code of an imported file of another handle or state type.
    public static let importType = "store.importType"
    /// The code of an id longer than the id's `max`.
    public static let invalidId = "store.invalidId"
    /// The code of a repeated id.
    public static let duplicateId = "store.duplicateId"
    /// The code of a reference or handle that names no entry of a known store.
    public static let unknownReference = "store.unknownReference"
    /// The code of a one-segment reference that names both a module entry and a root entry.
    public static let ambiguousReference = "store.ambiguousReference"
    /// The code of a pin to a type without content hashes or without a known store.
    public static let invalidPin = "store.invalidPin"
    /// The code of pinned references that form a cycle.
    public static let dependencyCycle = "store.dependencyCycle"

    /// Every error, ordered by file in reading order and by position within a file; never empty.
    public let diagnostics: [GesStoreDiagnostic]

    /// A message for people: the first error and the number of further ones; it is not part of the contract.
    public var message: String { diagnostics.count == 1 ? diagnostics[0].description : "\(diagnostics[0]) (and \(diagnostics.count - 1) more errors)" }

    /// The message.
    public var description: String { message }
}

/// The entries of one handle type read from store documents (specs/Documents.md#stores): every object under a stable
/// Text id, with references between entries replaced by handles and every state checked against the state schema. A
/// store is the handle resolver of its type (specs/Documents.md#stores-as-resolvers).
public final class GesStore: GesHandleResolver {
    /// The handle type of every entry.
    public let type: GesHandleType
    /// The root handles a store read from handles was read from, each with the version it was read at, in the given
    /// order; empty for a store read from named roots.
    public let sources: [GesHandle]
    /// Every entry, sorted by id in ordinal UTF-16 order.
    public let entries: [GesStoreEntry]

    private init(_ type: GesHandleType, _ entries: [GesStoreEntry], _ sources: [GesHandle]) {
        self.type = type
        self.entries = entries
        self.sources = sources
    }

    /// The entry with an id, or nil when the store has none with that id.
    public subscript(_ id: String) -> GesStoreEntry? {
        let index = lowerBound(id)
        return index < entries.count && entries[index].id.utf16.elementsEqual(id.utf16) ? entries[index] : nil
    }

    /// Returns the entries of a module: those whose id starts with the module name and `.`, sorted by id.
    /// - Parameter name: The module name, such as `drone`.
    /// - Returns: The module's entries; empty when it has none.
    public func module(_ name: String) -> [GesStoreEntry] {
        let prefix = name + "."
        let start = lowerBound(prefix)
        var end = start
        while end < entries.count && entries[end].id.utf16.starts(with: prefix.utf16) { end += 1 }
        return Array(entries[start..<end])
    }

    /// Returns the handles of every entry through a projection (specs/Schemas.md#projections), sorted by id, each carrying
    /// the projection and its version: the content hash of the projected state for a `:ContentHash` type, the entry's
    /// version otherwise. The list is computed on each call.
    /// - Parameter projection: A projection of the store's state, one of ``GesHandleType/projections``.
    /// - Returns: The projected handles, as a script receives them as `@Name(:P)~`.
    /// - Throws: `GesApiError.invalidArgument` when `projection` is no projection of the store's state.
    public func index(_ projection: GesSchema) throws -> [GesHandle] {
        if !projection.declaration.narrows(type.resolved.state) {
            throw GesApiError.invalidArgument("':\(projection.name)' is no projection of '@\(type.name)'.")
        }
        let hashed = type.version == .contentHash
        var result: [GesHandle] = []
        result.reserveCapacity(entries.count)
        for entry in entries {
            var version = entry.handle.version
            if hashed {
                let hash = try GesSchemaValues.projectedHash(type, projection.declaration, try GesSchemaValues.project(projection.declaration, entry.value))
                version = .bytes(hash.bytes)
            }
            result.append(entry.handle.withView(version, projection.name))
        }
        return result
    }

    /// Reads a store of a handle type from its roots.
    /// - Parameters:
    ///   - type: The handle type, whose id is Text and which is not composite.
    ///   - roots: The roots, read in this order.
    ///   - files: The source of every file's text.
    ///   - dependencies: The stores of other types whose handles entries may name or pin.
    /// - Returns: The store.
    /// - Throws: `GesApiError.invalidArgument` when the type is composite or has no Text id, two roots share a name, or
    ///   two stores share a type; `GesStoreError` listing every error when the store has errors.
    public static func read(_ type: GesHandleType, roots: [GesStoreRoot], files: some GesStoreFiles, dependencies: [GesStore] = []) throws -> GesStore {
        let (maxIdLength, others) = try Self.dependencies(type, dependencies)
        var names = Set<[UInt8]>()
        for root in roots where !names.insert(Array(root.name.utf8)).inserted {
            throw GesApiError.invalidArgument("Two roots are named '\(root.name)'.")
        }
        var reading = Reading(type: type, maxIdLength: maxIdLength, fetch: { root, path in (files.read(root: root, path: path), false) }, dependencies: others)
        return try reading.run(roots, nil)
    }

    /// Reads a store of a handle type from versioned sources (specs/Documents.md#reading-from-handles): each root is a
    /// handle of a store file type of `type` whose part is the path of the root file, and every file is requested from
    /// the resolver as the composite handle of the same root with the file's root-relative path.
    /// - Parameters:
    ///   - type: The handle type, whose id is Text and which is not composite.
    ///   - roots: The root handles, read in this order; a root without a version is pinned by the version of its first
    ///     answer.
    ///   - resolver: The resolver of the store file type, which answers each file with its document text.
    ///   - dependencies: The stores of other types whose handles entries may name or pin.
    /// - Returns: The store, whose `sources` are the roots with the versions they were read at.
    /// - Throws: `GesApiError.invalidArgument` when the type is composite or has no Text id, a root is no handle of a
    ///   store file type of `type` with a Text part, two roots are equal, or two stores share a type; `GesHandleError`
    ///   with `runtime.handleResolveFailed` when the resolver gave no answer or several, passed an invalid version, or
    ///   failed to turn a ref into a version, and with `runtime.handleVersionMismatch` when it passed another version than
    ///   the root's; `GesStoreError` listing every error when the store has errors. Errors the resolver throws
    ///   propagate unchanged.
    public static func read(_ type: GesHandleType, roots: [GesHandle], resolver: some GesHandleResolver, dependencies: [GesStore] = []) throws -> GesStore {
        let (maxIdLength, others) = try Self.dependencies(type, dependencies)
        var fileType: GesHandleType?
        let sources = HandleSources(resolver, roots.count)
        for root in roots {
            if !root.isComposite || root.part.valueKind != .text {
                throw GesApiError.invalidArgument("\(root) is no handle of a store file of @\(type.name) with a Text part.")
            }
            if fileType == nil || fileType!.name != root.typeName {
                fileType = type.fileType(root.typeName)
                if fileType == nil { throw GesApiError.invalidArgument("@\(root.typeName) is no store file type of @\(type.name).") }
            }
            if !sources.add(root, fileType!) { throw GesApiError.invalidArgument("The root \(root) is given twice.") }
        }
        let names = roots.map { GesStoreRoot(name: $0.description, path: $0.part.asText) }
        var reading = Reading(type: type, maxIdLength: maxIdLength, fetch: { root, path in try sources.read(root, path) }, dependencies: others)
        return try reading.run(names, sources)
    }

    /// Answers a handle of a known id with its entry's state and, for a `:ContentHash` type, its content hash; any other as
    /// missing.
    /// - Parameter call: The call holding the handle.
    /// - Throws: `GesApiError.invalidOperation` outside the resolver invocation.
    public func resolve(_ call: GesHandleResolveCall) throws {
        let handle = call.handle
        guard !handle.isComposite, handle.id.valueKind == .text, let entry = self[handle.id.asText] else {
            try call.setMissing()
            return
        }
        if type.version == .contentHash {
            try call.setState(entry.value, version: entry.handle.version)
        } else {
            try call.setState(entry.value)
        }
    }

    // The maximum id length of a store's type and the dependencies by type name, after checking both.
    private static func dependencies(_ type: GesHandleType, _ dependencies: [GesStore]) throws -> (Int64, [String: GesStore]) {
        if type.root != nil { throw GesApiError.invalidArgument("@\(type.name) is composite; a store needs a type of its own.") }
        guard let id = type.id, id.isText else { throw GesApiError.invalidArgument("@\(type.name) has no Text id.") }
        var others: [String: GesStore] = [:]
        for dependency in dependencies {
            if dependency.type.name == type.name || others[dependency.type.name] != nil {
                throw GesApiError.invalidArgument("More than one store of @\(dependency.type.name).")
            }
            others[dependency.type.name] = dependency
        }
        return (id.maxLength, others)
    }

    private func lowerBound(_ id: String) -> Int {
        var low = 0
        var high = entries.count
        while low < high {
            let middle = (low + high) >> 1
            if Self.ordinalLess(entries[middle].id, id) { low = middle + 1 } else { high = middle }
        }
        return low
    }

    static func ordinalLess(_ left: String, _ right: String) -> Bool { left.utf16.lexicographicallyPrecedes(right.utf16) }

    /// A root-relative import path (specs/Documents.md#import-paths), or nil when it steps above the root.
    static func combine(_ from: String, _ path: String) -> String? {
        var segments: [Substring.UnicodeScalarView] = []
        let pathScalars = path.unicodeScalars
        if pathScalars.first != "/" {
            let fromScalars = from.unicodeScalars
            if let directory = fromScalars.lastIndex(of: "/"), directory > fromScalars.startIndex {
                segments.append(contentsOf: fromScalars[..<directory].split(separator: "/", omittingEmptySubsequences: false))
            }
        }
        for segment in pathScalars.split(separator: "/", omittingEmptySubsequences: false) {
            if segment.isEmpty || segment.elementsEqual(".".unicodeScalars) { continue }
            if !segment.elementsEqual("..".unicodeScalars) {
                segments.append(segment)
                continue
            }
            if segments.isEmpty { return nil }
            segments.removeLast()
        }
        var result = String.UnicodeScalarView()
        for (index, segment) in segments.enumerated() {
            if index > 0 { result.append("/") }
            result.append(contentsOf: segment)
        }
        return String(result)
    }

    // A file a store reached, numbered in reading order; a missing root file has no text.
    private final class SourceFile {
        let index: Int
        let root: String
        let path: String
        let bytes: [UInt8]?

        init(_ index: Int, _ root: String, _ path: String, _ text: String?) {
            self.index = index
            self.root = root
            self.path = path
            bytes = text.map { Array($0.utf8) }
        }

        func at(_ offset: Int) -> GesSourceLocation {
            guard let bytes, offset >= 0 else { return GesSourceLocation(sourceName: path, moduleName: root) }
            let (line, column) = GesDocumentSyntax.location(bytes, offset)
            return GesSourceLocation(sourceName: path, line: line, column: column, moduleName: root)
        }
    }

    // An entry before its check: the key in its store file and the value, written there or in an imported state document.
    private final class Pending {
        let keyFile: SourceFile
        let item: GesStoreItem
        let valueFile: SourceFile
        let value: GesDocumentNode
        // The offset of the value: the entry's value in its store file, or the start of an imported state document.
        let valueStart: Int
        let id: String
        // The resolved references and handles of the value, in reading order.
        var links: [Link] = []
        var broken = false
        // 0 unvisited, 1 on the current pin chain, 2 checked, 3 failed or skipped.
        var state = 0
        var entry: GesStoreEntry?

        init(_ keyFile: SourceFile, _ item: GesStoreItem, _ valueFile: SourceFile, _ value: GesDocumentNode, _ valueStart: Int) {
            self.keyFile = keyFile
            self.item = item
            self.valueFile = valueFile
            self.value = value
            self.valueStart = valueStart
            id = item.module.isEmpty ? item.key : item.module + "." + item.key
        }
    }

    // The place of a link in a value: the node's number in depth-first reading order and the index of a Map key, or -1
    // when the node itself is replaced.
    private struct Site: Hashable {
        let node: Int
        let key: Int
    }

    // A reference or complete handle with its target: an own entry or one of a dependency.
    private struct Link {
        let site: Site
        let offset: Int
        let pinned: Bool
        let own: Pending?
        let other: GesStoreEntry?
    }

    // The text of a root's file, or nil when the root has none; `state` is true for a state answer instead of a document.
    private typealias Fetch = (_ root: String, _ path: String) throws -> (text: String?, state: Bool)

    // The roots of a store read from handles, by the text form of each root handle, with the version each is read at.
    private final class HandleSources {
        private final class Source {
            let handle: GesHandle
            let fileType: GesHandleType
            var prepared = false
            var unknownRef = false
            var version = GesValue.nothing

            init(_ handle: GesHandle, _ fileType: GesHandleType) {
                self.handle = handle
                self.fileType = fileType
            }
        }

        private let resolver: any GesHandleResolver
        private var ordered: [Source] = []
        private var byName: [[UInt8]: Source] = [:]
        private let call = GesHandleResolveCall()

        init(_ resolver: any GesHandleResolver, _ capacity: Int) {
            self.resolver = resolver
            ordered.reserveCapacity(capacity)
        }

        func add(_ root: GesHandle, _ fileType: GesHandleType) -> Bool {
            let source = Source(root, fileType)
            let name = Array(root.description.utf8)
            if byName[name] != nil { return false }
            byName[name] = source
            ordered.append(source)
            return true
        }

        // The roots with the versions they were read at, in the given order.
        func read() throws -> [GesHandle] {
            try ordered.map { $0.version.isNothing ? $0.handle : try $0.handle.withVersion($0.version) }
        }

        func read(_ root: String, _ path: String) throws -> (text: String?, state: Bool) {
            let source = byName[Array(root.utf8)]!
            let fileType = source.fileType
            if !source.prepared {
                source.prepared = true
                source.version = source.handle.version
                if !source.handle.ref.isNothing {
                    let version = try resolver.resolveRef(source.handle)
                    if version.isNothing {
                        source.unknownRef = true
                    } else if !GesHandleStates.isVersionOf(fileType.version, version) {
                        throw GesHandleError(
                            code: GesDiagnosticCodes.runtimeHandleResolveFailed,
                            symbol: root,
                            message: "The resolver of @\(fileType.name) returned no valid version."
                        )
                    } else {
                        source.version = version
                    }
                }
            }
            if source.unknownRef || Int64(path.utf8.count) > fileType.part!.maxLength { return (nil, false) }
            let handle = try GesHandle(
                typeName: fileType.name,
                root: try GesHandle(typeName: fileType.root!.name, id: source.handle.id, version: source.version),
                part: .text(path)
            )
            let answer: GesHandleResolveCall.AnswerKind
            let text: String?
            let passed: GesValue
            call.begin(handle, fileType, nil, 0)
            defer { call.end() }
            try resolver.resolve(call)
            answer = call.answer
            text = call.text
            passed = call.version
            let symbol = handle.description
            if answer == .none || answer == .several {
                throw GesHandleError(code: GesDiagnosticCodes.runtimeHandleResolveFailed, symbol: symbol, message: "The resolver of @\(fileType.name) must give exactly one answer.")
            }
            if !passed.isNothing {
                if !GesHandleStates.isVersionOf(fileType.version, passed) {
                    throw GesHandleError(
                        code: GesDiagnosticCodes.runtimeHandleResolveFailed,
                        symbol: symbol,
                        message: "The resolver of @\(fileType.name) passed no valid version of its type."
                    )
                }
                if source.version.isNothing {
                    source.version = passed
                } else if !GesHandleStates.versionEquals(source.version, passed) {
                    throw GesHandleError(
                        code: GesDiagnosticCodes.runtimeHandleVersionMismatch,
                        symbol: symbol,
                        message: "The resolver of @\(fileType.name) delivered another version of \(root)."
                    )
                }
            }
            switch answer {
            case .document: return (text, false)
            case .state: return (nil, true)
            default: return (nil, false)
            }
        }
    }

    private struct Reading {
        let type: GesHandleType
        let maxIdLength: Int64
        let fetch: Fetch
        let dependencies: [String: GesStore]
        private var diagnostics: [(file: SourceFile, diagnostic: GesStoreDiagnostic)] = []
        private var sourceFiles: [SourceFile] = []
        private var visited = Set<[UInt8]>()
        private var chain = Set<[UInt8]>()
        private var pendingEntries: [Pending] = []
        private var ids: [String: Pending] = [:]
        private var sortedIds: [String] = []

        init(type: GesHandleType, maxIdLength: Int64, fetch: @escaping Fetch, dependencies: [String: GesStore]) {
            self.type = type
            self.maxIdLength = maxIdLength
            self.fetch = fetch
            self.dependencies = dependencies
        }

        mutating func run(_ roots: [GesStoreRoot], _ sources: HandleSources?) throws -> GesStore {
            for root in roots {
                guard let path = combine("", root.path) else {
                    let file = addFile(root.name, root.path, nil)
                    report(file, -1, GesStoreError.importOutsideRoot, "The root file path '\(root.path)' steps above the root.")
                    continue
                }
                try readStoreFile(root.name, path, nil, -1)
            }
            if diagnostics.isEmpty { checkIds() }
            if diagnostics.isEmpty { try checkEntries() }
            if !diagnostics.isEmpty { throw GesStoreError(diagnostics: sorted()) }
            let entries = pendingEntries.map { $0.entry! }.sorted { ordinalLess($0.id, $1.id) }
            return GesStore(type, entries, try sources?.read() ?? [])
        }

        // Step 1: a store file and, at each import, the imported file.
        private mutating func readStoreFile(_ root: String, _ path: String, _ importer: SourceFile?, _ importOffset: Int) throws {
            let key = Array((root + "\n" + path).utf8)
            if chain.contains(key) {
                report(importer!, importOffset, GesStoreError.importCycle, "'\(path)' imports itself through its import chain.")
                return
            }
            if !visited.insert(key).inserted { return }
            let (fetched, state) = try fetch(root, path)
            if state {
                report(importer ?? addFile(root, path, nil), importer == nil ? -1 : importOffset, GesStoreError.importKind, "'\(path)' is an object's state, not a store file.")
                return
            }
            guard let text = fetched else {
                report(importer ?? addFile(root, path, nil), importer == nil ? -1 : importOffset, GesStoreError.importMissing, "The root '\(root)' has no file '\(path)'.")
                return
            }
            let file = addFile(root, path, text)
            let syntax = GesDocumentReader.readStore(text)
            guard syntax.isStore else {
                report(importer ?? file, importer == nil ? syntax.headerOffset : importOffset, GesStoreError.importKind, "'\(path)' is no store file.")
                return
            }
            if let diagnostic = syntax.diagnostic {
                reportDocument(file, diagnostic)
                return
            }
            if syntax.typeName != type.name {
                let message = "'\(path)' is a store of @\(syntax.typeName), not of @\(type.name)."
                report(importer ?? file, importer == nil ? syntax.headerOffset : importOffset, GesStoreError.importType, message)
                return
            }
            chain.insert(key)
            for item in syntax.items {
                switch item.kind {
                case .import:
                    if let imported = imported(file, item) { try readStoreFile(root, imported, file, item.pathOffset) }
                case .entry:
                    pendingEntries.append(Pending(file, item, file, item.value!, item.valueOffset))
                case .entryImport:
                    try readStateDocument(file, item)
                }
            }
            chain.remove(key)
        }

        // The state document of `key: import 'path'`.
        private mutating func readStateDocument(_ file: SourceFile, _ item: GesStoreItem) throws {
            guard let path = imported(file, item) else { return }
            let (fetched, state) = try fetch(file.root, path)
            if state {
                report(file, item.pathOffset, GesStoreError.importKind, "'\(path)' is an object's state, not a state document.")
                return
            }
            guard let text = fetched else {
                report(file, item.pathOffset, GesStoreError.importMissing, "The root '\(file.root)' has no file '\(path)'.")
                return
            }
            let syntax = GesDocumentReader.read(text, references: true)
            if GesDocumentReader.readStore(text).isStore {
                report(file, item.pathOffset, GesStoreError.importKind, "'\(path)' is a store file, not a state document.")
                return
            }
            let document = addFile(file.root, path, text)
            if let diagnostic = syntax.diagnostic {
                reportDocument(document, diagnostic)
                return
            }
            guard case .type(let name) = syntax.schema else {
                report(file, item.pathOffset, GesStoreError.importKind, "'\(path)' names no state type in a :Schema header.")
                return
            }
            if name != type.stateName {
                report(file, item.pathOffset, GesStoreError.importType, "'\(path)' is a :\(name) document, not a :\(type.stateName) state.")
                return
            }
            pendingEntries.append(Pending(file, item, document, syntax.root!, 0))
        }

        private mutating func imported(_ file: SourceFile, _ item: GesStoreItem) -> String? {
            if let path = combine(file.path, item.path) { return path }
            report(file, item.pathOffset, GesStoreError.importOutsideRoot, "The import path '\(item.path)' steps above the root.")
            return nil
        }

        // Step 2: every id fits the type's id and occurs once.
        private mutating func checkIds() {
            for pending in pendingEntries {
                if Int64(pending.id.utf8.count) > maxIdLength {
                    report(pending.keyFile, pending.item.keyOffset, GesStoreError.invalidId, "The id '\(pending.id)' is longer than \(maxIdLength) bytes.")
                    continue
                }
                if let first = ids[pending.id] {
                    let firstLocation = first.keyFile.at(first.item.keyOffset)
                    let diagnostic = GesStoreDiagnostic(
                        code: GesStoreError.duplicateId,
                        message: "The id '\(pending.id)' is already declared at \(GesStoreDiagnostic.text(firstLocation)).",
                        location: pending.keyFile.at(pending.item.keyOffset),
                        fieldPath: "",
                        other: firstLocation,
                        suggestion: nil
                    )
                    diagnostics.append((pending.keyFile, diagnostic))
                    continue
                }
                ids[pending.id] = pending
            }
        }

        // Step 3: references resolve, pinned entries are checked before the entries that pin them, every state is checked.
        private mutating func checkEntries() throws {
            sortedIds = ids.keys.sorted(by: ordinalLess)
            for pending in pendingEntries {
                var counter = 0
                collect(pending, pending.value, &counter)
            }
            for pending in pendingEntries { try visit(pending) }
        }

        // Records the links of a value and reports the references that name nothing. Nodes are numbered depth-first in
        // reading order, as `replace` numbers them.
        private mutating func collect(_ pending: Pending, _ node: GesDocumentNode, _ counter: inout Int) {
            let number = counter
            counter += 1
            switch node {
            case .reference(let name, let pinned, let offset):
                resolve(pending, Site(node: number, key: -1), name, offset, pinned)
            case .handle(let text, let pinned, let offset):
                resolveHandle(pending, Site(node: number, key: -1), text, offset, pinned)
            case .list(let items, _):
                for item in items { collect(pending, item, &counter) }
            case .map(let keys, let values, let keyOffsets, _, let handleKeys):
                for index in values.indices {
                    if !handleKeys.isEmpty && handleKeys[index] {
                        let label = keys[index]
                        let site = Site(node: number, key: index)
                        if !label.utf8.contains(58) {
                            resolve(pending, site, String(label.unicodeScalars.dropFirst()), keyOffsets[index], false)
                        } else {
                            resolveHandle(pending, site, label, keyOffsets[index], false)
                        }
                    }
                    collect(pending, values[index], &counter)
                }
            case .form(_, _, let arguments, _), .message(_, _, let arguments, _):
                for argument in arguments { collect(pending, argument, &counter) }
            default:
                break
            }
        }

        private mutating func resolve(_ pending: Pending, _ site: Site, _ name: String, _ offset: Int, _ pinned: Bool) {
            let target: Pending?
            if name.utf8.contains(46) || pending.item.module.isEmpty {
                target = ids[name]
            } else {
                let inModule = ids[pending.item.module + "." + name]
                let atRoot = ids[name]
                if let inModule, let atRoot {
                    fail(pending, offset, GesStoreError.ambiguousReference, "@\(name) names both \(inModule.id) and the root entry \(atRoot.id).")
                    return
                }
                target = inModule ?? atRoot
            }
            guard let target else {
                failUnknown(pending, offset, "@" + name, name, pending.item.module, sortedIds)
                return
            }
            if pinned && type.version != .contentHash {
                fail(pending, offset, GesStoreError.invalidPin, "@\(type.name) has no content hash to pin @\(name) to.")
                return
            }
            pending.links.append(Link(site: site, offset: offset, pinned: pinned, own: target, other: nil))
        }

        // A complete handle names an entry of its type's store when the store is known.
        private mutating func resolveHandle(_ pending: Pending, _ site: Site, _ text: String, _ offset: Int, _ pinned: Bool) {
            guard let handle = try? GesHandle(parsing: text), !handle.isComposite else {
                if pinned { fail(pending, offset, GesStoreError.invalidPin, "\(text) names no store entry to pin to.") }
                return
            }
            let id = handle.id.valueKind == .text ? handle.id.asText : nil
            if handle.typeName == type.name {
                guard let id, let target = ids[id] else {
                    failUnknown(pending, offset, text, id ?? "", "", sortedIds)
                    return
                }
                if pinned && type.version != .contentHash {
                    fail(pending, offset, GesStoreError.invalidPin, "@\(type.name) has no content hash to pin \(text) to.")
                    return
                }
                if pinned { pending.links.append(Link(site: site, offset: offset, pinned: true, own: target, other: nil)) }
                return
            }
            guard let store = dependencies[handle.typeName] else {
                if pinned { fail(pending, offset, GesStoreError.invalidPin, "No store of @\(handle.typeName) is known to pin \(text) to.") }
                return
            }
            guard let id, let entry = store[id] else {
                failUnknown(pending, offset, text, id ?? "", "", store.entries.map(\.id))
                return
            }
            if pinned && store.type.version != .contentHash {
                fail(pending, offset, GesStoreError.invalidPin, "@\(handle.typeName) has no content hash to pin \(text) to.")
                return
            }
            if pinned { pending.links.append(Link(site: site, offset: offset, pinned: true, own: nil, other: entry)) }
        }

        private mutating func visit(_ pending: Pending) throws {
            if pending.state != 0 { return }
            pending.state = 1
            for link in pending.links {
                guard link.pinned, let target = link.own else { continue }
                if target.state == 1 {
                    report(pending.valueFile, link.offset, GesStoreError.dependencyCycle, "Pinning @\(target.id) closes a cycle of pinned references.")
                    pending.broken = true
                    break
                }
                try visit(target)
                if target.state == 3 {
                    pending.broken = true
                    break
                }
            }
            pending.state = try !pending.broken && check(pending) ? 2 : 3
        }

        // Replaces the links by handles, builds the state and checks it against the state schema.
        private mutating func check(_ pending: Pending) throws -> Bool {
            var replacements: [Site: String] = [:]
            for link in pending.links {
                let handle: GesHandle
                if let other = link.other {
                    handle = other.handle
                } else if link.pinned {
                    handle = link.own!.entry!.handle
                } else {
                    handle = try GesHandle(typeName: type.name, id: .text(link.own!.id))
                }
                replacements[link.site] = handle.description
            }
            var counter = 0
            let root = Self.replace(pending.value, replacements, &counter)
            let file = pending.valueFile
            let syntax = GesDocumentSyntax(bytes: file.bytes!, root: root)
            let declaration = type.resolved.state!
            do {
                let value = try GesDocumentReading.entries(syntax, root, recordType: type.stateName)
                let (failure, chosen) = try GesSchemaDocumentCheck.choosing(syntax, declaration, nil, rootStart: pending.valueStart)
                if let failure {
                    reportError(file, failure)
                    return false
                }
                let state = try GesSchemaValues.normalizeState(declaration, GesSchemaValues.documentShapes(declaration, value, chosen: chosen), "", type.declarations)
                let id = GesValue.text(pending.id)
                let handle: GesHandle
                if type.version == .contentHash {
                    handle = try GesHandle(typeName: type.name, id: id, version: .bytes(try GesSchemaValues.contentHash(type, state: state).bytes))
                } else {
                    handle = try GesHandle(typeName: type.name, id: id)
                }
                pending.entry = GesStoreEntry(id: pending.id, handle: handle, value: state, origin: pending.keyFile.at(pending.item.keyOffset))
                return true
            } catch let error as GesDocumentError {
                reportError(file, error)
                return false
            } catch let error as GesWireError {
                let message = error.detail.isEmpty ? "\(error.code) at '\(error.path)'" : "\(error.code) at '\(error.path)': \(error.detail)"
                diagnostics.append((file, GesStoreDiagnostic(code: error.code, message: message, location: file.at(pending.valueStart), fieldPath: error.path, other: nil, suggestion: nil)))
                return false
            }
        }

        // The value with every linked node and key replaced by its handle text.
        private static func replace(_ node: GesDocumentNode, _ replacements: [Site: String], _ counter: inout Int) -> GesDocumentNode {
            let number = counter
            counter += 1
            switch node {
            case .reference(_, _, let offset), .handle(_, _, let offset):
                guard let text = replacements[Site(node: number, key: -1)] else { return node }
                return .handle(text, pinned: false, offset: offset)
            case .list(let items, let offsets):
                return .list(items.map { replace($0, replacements, &counter) }, offsets: offsets)
            case .map(var keys, let values, let keyOffsets, let valueOffsets, let handleKeys):
                var children: [GesDocumentNode] = []
                children.reserveCapacity(values.count)
                for index in values.indices {
                    if let key = replacements[Site(node: number, key: index)] { keys[index] = key }
                    children.append(replace(values[index], replacements, &counter))
                }
                return .map(keys: keys, values: children, keyOffsets: keyOffsets, valueOffsets: valueOffsets, handleKeys: handleKeys)
            case .form(let type, let labels, let arguments, let offset):
                return .form(type: type, labels: labels, arguments: arguments.map { replace($0, replacements, &counter) }, offset: offset)
            case .message(let name, let labels, let arguments, let offset):
                return .message(name: name, labels: labels, arguments: arguments.map { replace($0, replacements, &counter) }, offset: offset)
            default:
                return node
            }
        }

        // An unknown reference with the suggestion of specs/Documents.md#references.
        private mutating func failUnknown(_ pending: Pending, _ offset: Int, _ written: String, _ id: String, _ module: String, _ ids: [String]) {
            let suggestion = Self.suggest(id, !id.utf8.contains(46) && !module.isEmpty ? module + "." + id : nil, ids)
            let message = "\(written) names no entry." + (suggestion.map { " Did you mean \(Self.written(written, $0))?" } ?? "")
            pending.broken = true
            let diagnostic = GesStoreDiagnostic(
                code: GesStoreError.unknownReference,
                message: message,
                location: pending.valueFile.at(offset),
                fieldPath: "",
                other: nil,
                suggestion: suggestion
            )
            diagnostics.append((pending.valueFile, diagnostic))
        }

        // A suggested id in the form of the written reference: with the type when it named one.
        private static func written(_ written: String, _ id: String) -> String {
            let scalars = written.unicodeScalars
            guard let colon = scalars.firstIndex(of: ":") else { return "@" + id }
            return String(scalars[...colon]) + id
        }

        private static func suggest(_ id: String, _ qualified: String?, _ ids: [String]) -> String? {
            let last = lastSegment(id)
            for candidate in ids where lastSegment(candidate).elementsEqual(last) { return candidate }
            var best: String?
            var bestDistance = 4
            for candidate in ids {
                var distance = Self.distance(id, candidate)
                if let qualified { distance = min(distance, Self.distance(qualified, candidate)) }
                if distance >= bestDistance { continue }
                best = candidate
                bestDistance = distance
            }
            return best
        }

        private static func lastSegment(_ id: String) -> [UInt16] {
            let units = Array(id.utf16)
            guard let dot = units.lastIndex(of: 46) else { return units }
            return Array(units[(dot + 1)...])
        }

        // The Levenshtein distance of two ids, in UTF-16 code units.
        private static func distance(_ left: String, _ right: String) -> Int {
            let left = Array(left.utf16)
            let right = Array(right.utf16)
            var previous = Array(0...right.count)
            var current = [Int](repeating: 0, count: right.count + 1)
            if left.isEmpty { return right.count }
            for i in 1...left.count {
                current[0] = i
                if !right.isEmpty {
                    for j in 1...right.count {
                        let substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1)
                        current[j] = min(substitution, min(previous[j], current[j - 1]) + 1)
                    }
                }
                swap(&previous, &current)
            }
            return previous[right.count]
        }

        private mutating func fail(_ pending: Pending, _ offset: Int, _ code: String, _ message: String) {
            pending.broken = true
            report(pending.valueFile, offset, code, message)
        }

        private mutating func addFile(_ root: String, _ path: String, _ text: String?) -> SourceFile {
            let file = SourceFile(sourceFiles.count, root, path, text)
            sourceFiles.append(file)
            return file
        }

        private mutating func report(_ file: SourceFile, _ offset: Int, _ code: String, _ message: String) {
            diagnostics.append((file, GesStoreDiagnostic(code: code, message: message, location: file.at(offset), fieldPath: "", other: nil, suggestion: nil)))
        }

        private mutating func reportDocument(_ file: SourceFile, _ diagnostic: GesDocumentDiagnostic) {
            let (line, column) = GesDocumentSyntax.location(file.bytes!, diagnostic.start)
            let limit = diagnostic.limitName == nil ? nil : diagnostic.limitValue
            let error = GesDocumentError(code: diagnostic.code, line: line, column: column, limitName: diagnostic.limitName, limit: limit)
            diagnostics.append((file, GesStoreDiagnostic(code: diagnostic.code, message: error.message, location: file.at(diagnostic.start), fieldPath: "", other: nil, suggestion: nil)))
        }

        // A document error, which carries its line and column; it sorts by them within its file.
        private mutating func reportError(_ file: SourceFile, _ error: GesDocumentError) {
            let location = GesSourceLocation(sourceName: file.path, line: error.line, column: error.column, moduleName: file.root)
            diagnostics.append((file, GesStoreDiagnostic(code: error.code, message: error.message, location: location, fieldPath: error.path, other: nil, suggestion: nil)))
        }

        private func sorted() -> [GesStoreDiagnostic] {
            diagnostics.indices.sorted { left, right in
                let a = diagnostics[left]
                let b = diagnostics[right]
                if a.file.index != b.file.index { return a.file.index < b.file.index }
                let lineA = a.diagnostic.location.line ?? 0
                let lineB = b.diagnostic.location.line ?? 0
                if lineA != lineB { return lineA < lineB }
                let columnA = a.diagnostic.location.column ?? 0
                let columnB = b.diagnostic.location.column ?? 0
                if columnA != columnB { return columnA < columnB }
                return left < right
            }.map { diagnostics[$0].diagnostic }
        }
    }
}
