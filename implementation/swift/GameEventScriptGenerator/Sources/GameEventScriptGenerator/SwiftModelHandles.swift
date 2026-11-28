// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// The Swift model type of a handle type: a struct `<Name>Handle` that holds the id, or the root and part of a type with a part,
// and the version where the type has one: an attached type's is its root's, a keyed type holds its root without a version and
// its own. Equality, hashing and order use the identity only, a Text id by its Unicode scalars rather
// than by String's canonical equivalence. Like the C# struct, the public initializers throw GesApiError for an id or part
// outside the declared range or length and for a runtime handle of another form, and none creates a default handle; the
// generated code builds handles it already checked through internal unchecked initializers. It converts to and from
// GesHandle, whose canonical text form it writes and reads as Codable. Swift's synthesized equality and hashing of a
// Dictionary already compare and hash entries independent of order, so models need no counterpart of the C# MapEquals and
// MapHash helpers.
extension GesSwiftModelEmitter {
    // Also the wire code's handle type, which needs only the Runtime; `handles` holds the root of a type with a part.
    static func handle(_ code: inout Code, _ handles: [GesModelHandle], _ handle: GesModelHandle, _ access: String) {
        let name = GesModelPlanner.handleName(handle.name)
        let typeName = GesSchemaPlanner.literal(handle.name, swift: true)
        let withPart = handle.hasPart
        let keyed = handle.isKeyed
        // A version of its own: every type except one attached to a root with a part, which carries its root's.
        let own = !withPart || keyed
        let root = withPart ? handles.first { $0.name == handle.root }! : nil
        let rootName = root.map { GesModelPlanner.handleName($0.name) } ?? ""
        let version = versionType(handle.version)
        let describe =
            switch handle.version {
            case .revision: ", with its revision"
            case .gesHash: ", with its content hash"
            case .sha1: ", with its SHA-1 version"
            case .sha256: ", with its SHA-256 version"
            case .none: ""
            }
        let key = handle.key
        let keyType = key.isText ? "String" : "Int64"
        let keyName = withPart ? "part" : "id"
        let of = handle.state.isEmpty ? "an object without state outside the host" : "an object of `:\(handle.state)` outside the host"
        let rooted = !withPart ? "" : keyed ? ", keyed by a `@" + handle.root! + "`" : ", a part of a `@" + handle.root! + "`"
        code.line("/// A handle `@\(handle.name)`: the identity of \(of)\(rooted)\(describe).")
        code.line("/// Equality, hashing and order use the identity and ignore the version\(key.isText ? "; a Text compares by Unicode scalars" : "").")
        code.open("\(access)struct \(name): Hashable, Comparable, Sendable, Codable, CustomStringConvertible {")
        if withPart {
            code.line(keyed ? "/// The root, without a version." : "/// The root, whose version is the handle's.")
            code.line("\(access)let root: \(rootName)")
            code.blank()
            code.line(keyed ? "/// The part, which with the root's id names the object." : "/// The part of the root's object.")
            code.line("\(access)let part: \(keyType)")
            if let version {
                code.blank()
                if keyed {
                    code.line("/// The version of its own, or nil for none.")
                    code.line("\(access)let version: \(version)?")
                } else {
                    code.line("/// The version: the root's, or nil for none.")
                    code.line("\(access)var version: \(version)? { root.version }")
                }
            }
        } else {
            code.line("/// The id.")
            code.line("\(access)let id: \(keyType)")
            if let version {
                code.blank()
                code.line("/// The version, or nil for none.")
                code.line("\(access)let version: \(version)?")
            }
        }

        code.blank()
        code.line("/// Creates a handle.")
        code.line("///")
        code.line("/// - Parameters:")
        if withPart { code.line(keyed ? "///   - root: The root, whose version the handle drops." : "///   - root: The root, whose version is the handle's.") }
        code.line("///   - \(keyName): The \(keyName), \(keyDescription(key)).")
        if version != nil && own { code.line("///   - version: The version, or nil for none.") }
        code.line("/// - Throws: `GesApiError.invalidArgument` when the \(keyName) is out of its declared \(key.isText ? "length" : "range").")
        let keyedVersion = keyed && version != nil
        let parameters =
            if withPart { "root: \(rootName), part: \(keyType)" + (keyedVersion ? ", version: \(version!)? = nil" : "") } else if let version { "id: \(keyType), version: \(version)? = nil" } else { "id: \(keyType)" }
        code.open("\(access)init(\(parameters)) throws(GesApiError) {")
        let bound = GesSchemaPlanner.literal("The \(keyName) of a @\(handle.name) is out of its declared \(key.isText ? "length" : "range").", swift: true)
        code.line(
            key.isText
                ? "guard \(keyName).utf8.count <= \(number(key.maxLength)) else { throw .invalidArgument(\(bound)) }"
                : "guard \(keyName) >= \(number(key.minimum)) && \(keyName) <= \(number(key.maximum)) else { throw .invalidArgument(\(bound)) }"
        )
        let keyedRoot = keyed && root!.version != .none ? "root.withoutVersion()" : "root"
        code.line(withPart ? "self.init(uncheckedRoot: \(keyedRoot), part: part\(keyedVersion ? ", version: version" : ""))" : version == nil ? "self.init(uncheckedId: id)" : "self.init(uncheckedId: id, version: version)")
        code.close()
        code.blank()
        code.line("// A handle whose \(keyName) the generated code already checked.")
        let uncheckedParameters =
            if withPart {
                "uncheckedRoot root: \(rootName), part: \(keyType)" + (keyedVersion ? ", version: \(version!)? = nil" : "")
            } else if let version { "uncheckedId id: \(keyType), version: \(version)? = nil" } else { "uncheckedId id: \(keyType)" }
        code.open("init(\(uncheckedParameters)) {")
        if withPart { code.line("self.root = root") }
        code.line("self.\(keyName) = \(keyName)")
        if version != nil && own { code.line("self.version = version") }
        code.close()
        if version != nil {
            code.blank()
            code.line("/// Returns the handle with the same identity and no version.")
            code.line("\(access)func withoutVersion() -> \(name) { \(name)(\(!withPart ? "uncheckedId: id" : keyed ? "uncheckedRoot: root, part: part" : "uncheckedRoot: root.withoutVersion(), part: part")) }")
        }

        let equal = key.isText ? "left.\(keyName).utf8.elementsEqual(right.\(keyName).utf8)" : "left.\(keyName) == right.\(keyName)"
        let less = key.isText ? "left.\(keyName).utf8.lexicographicallyPrecedes(right.\(keyName).utf8)" : "left.\(keyName) < right.\(keyName)"
        code.blank()
        code.line("/// Compares the identity: whether both handles name the same object.")
        code.line("\(access)static func == (left: \(name), right: \(name)) -> Bool { \(withPart ? "left.root == right.root && " : "")\(equal) }")
        code.blank()
        code.line("/// Hashes the identity.")
        code.open("\(access)func hash(into hasher: inout Hasher) {")
        if withPart { code.line("hasher.combine(root)") }
        if key.isText {
            code.line("for byte in \(keyName).utf8 { hasher.combine(byte) }")
            code.line("// No UTF-8 byte is 0xFF, so it ends the text.")
            code.line("hasher.combine(0xFF as UInt8)")
        } else {
            code.line("hasher.combine(\(keyName))")
        }
        code.close()
        code.blank()
        code.line(withPart ? "/// Orders by identity: the root, then the part, a Text by Unicode scalars." : "/// Orders by identity: the id, a Text by Unicode scalars.")
        code.line(
            withPart
                ? "\(access)static func < (left: \(name), right: \(name)) -> Bool { left.root != right.root ? left.root < right.root : \(less) }"
                : "\(access)static func < (left: \(name), right: \(name)) -> Bool { \(less) }"
        )
        code.blank()
        code.line("/// The text form, such as `\(example(handle, root))`.")
        code.line("\(access)var description: String { toHandle().description }")
        code.blank()
        code.line("// Cannot throw: the type name is valid and the id, part and version have valid kinds.")
        code.line("/// Converts to a runtime handle.")
        if withPart && keyed {
            code.line(
                "\(access)func toHandle() -> GesHandle { try! GesHandle(typeName: \(typeName), keyedBy: root.toHandle(), part: \(keyValue(key, "part"))\(version == nil ? "" : ", version: " + versionValue(handle.version))) }"
            )
        } else if withPart {
            code.line("\(access)func toHandle() -> GesHandle { try! GesHandle(typeName: \(typeName), attachedTo: root.toHandle(), part: \(keyValue(key, "part"))) }")
        } else if version == nil {
            code.line("\(access)func toHandle() -> GesHandle { try! GesHandle(typeName: \(typeName), id: \(keyValue(key, "id"))) }")
        } else {
            code.line("\(access)func toHandle() -> GesHandle { try! GesHandle(typeName: \(typeName), id: \(keyValue(key, "id")), version: \(versionValue(handle.version))) }")
        }
        code.blank()
        code.line("/// Converts a runtime handle of this type.")
        code.line("///")
        code.line("/// - Throws: `GesApiError.invalidArgument` for a handle of another type or out of its declared form.")
        code.open("\(access)init(_ handle: GesHandle) throws(GesApiError) {")
        code.line("guard let value = \(name)(matching: handle) else { throw .invalidArgument(\(GesSchemaPlanner.literal("The handle is no @" + handle.name + ".", swift: true))) }")
        code.line("self = value")
        code.close()
        code.blank()
        code.line("// A runtime handle of this type, or nil for a handle of another type or out of its declared form.")
        code.open("init?(matching handle: GesHandle) {")
        code.line("guard handle.typeName == \(typeName), \(withPart ? "" : "!")handle.hasPart\(withPart ? ", \(keyed ? "" : "!")handle.isKeyed" : "") else { return nil }")
        if let root {
            let rootHandle =
                root.version == .none || keyed
                ? "GesHandle(typeName: \(GesSchemaPlanner.literal(root.name, swift: true)), id: handle.id)"
                : "GesHandle(typeName: \(GesSchemaPlanner.literal(root.name, swift: true)), id: handle.id, version: handle.version)"
            code.line("guard let rootHandle = try? \(rootHandle), let root = \(rootName)(matching: rootHandle) else { return nil }")
            keyRead(&code, key, "handle.part", "part")
            if keyedVersion {
                versionRead(&code, handle.version)
                code.line("self.init(uncheckedRoot: root, part: part, version: version)")
            } else {
                code.line("self.init(uncheckedRoot: root, part: part)")
            }
        } else {
            keyRead(&code, key, "handle.id", "id")
            if version == nil {
                code.line("self.init(uncheckedId: id)")
            } else {
                versionRead(&code, handle.version)
                code.line("self.init(uncheckedId: id, version: version)")
            }
        }

        code.close()
        code.blank()
        code.line("/// Reads the text form.")
        code.line("///")
        code.line("/// - Throws: `DecodingError.dataCorrupted` for text that is no handle `@\(handle.name)` of the declared form.")
        code.open("\(access)init(from decoder: any Decoder) throws {")
        code.line("let container = try decoder.singleValueContainer()")
        code.line("let text = try container.decode(String.self)")
        code.line(
            "guard let handle = try? GesHandle(parsing: text), let value = \(name)(matching: handle) else { throw DecodingError.dataCorruptedError(in: container, debugDescription: \(GesSchemaPlanner.literal("The text is no @" + handle.name + ".", swift: true))) }"
        )
        code.line("self = value")
        code.close()
        code.blank()
        code.line("/// Writes the text form.")
        code.open("\(access)func encode(to encoder: any Encoder) throws {")
        code.line("var container = encoder.singleValueContainer()")
        code.line("try container.encode(description)")
        code.close()
        code.blank()
        code.line("// A GES handle value of this type; a field without a version drops it, one with a version requires it.")
        code.open("static func read(_ value: GesValue, versioned: Bool) -> \(name)? {")
        code.line("guard let handle = value.handleValue, let result = \(name)(matching: handle) else { return nil }")
        if version == nil {
            code.line("return result")
        } else {
            code.line("if !versioned { return result.withoutVersion() }")
            code.line("return handle.hasVersion ? result : nil")
        }

        code.close()
        code.blank()
        code.line("// The GES value of the handle; a field without a version drops it.")
        code.line("func toValue(versioned: Bool) -> GesValue { .handle(\(version == nil ? "toHandle()" : "(versioned ? self : withoutVersion()).toHandle()")) }")
        code.close()
    }

    private static func versionType(_ kind: GesHandleVersionKind) -> String? {
        switch kind {
        case .revision: "Int64"
        case .sha1: "GesHash20"
        case .sha256, .gesHash: "GesHash32"
        case .none: nil
        }
    }

    private static func versionValue(_ kind: GesHandleVersionKind) -> String {
        kind == .revision ? "version.map { GesValue.integer($0) } ?? .nothing" : "version.map { GesValue.bytes($0.bytes) } ?? .nothing"
    }

    private static func versionRead(_ code: inout Code, _ kind: GesHandleVersionKind) {
        let type = versionType(kind)!
        code.line("let version: \(type)?")
        if kind == .revision {
            code.line("if let revision = handle.version.integerValue { version = revision }")
        } else {
            code.line("if let bytes = handle.version.bytesValue, bytes.count == \(type).length { version = \(type)(bytes: bytes) }")
        }
        code.line("else if handle.version.isNothing { version = nil }")
        code.line("else { return nil }")
    }

    private static func keyValue(_ key: GesHandleKey, _ member: String) -> String { key.isText ? ".text(\(member))" : ".integer(\(member))" }

    private static func keyRead(_ code: inout Code, _ key: GesHandleKey, _ expression: String, _ name: String) {
        code.line(
            key.isText
                ? "guard \(expression).valueKind == .text, let \(name) = \(expression).textValue, \(name).utf8.count <= \(number(key.maxLength)) else { return nil }"
                : "guard let \(name) = \(expression).integerValue, \(name) >= \(number(key.minimum)) && \(name) <= \(number(key.maximum)) else { return nil }"
        )
    }

    private static func keyDescription(_ key: GesHandleKey) -> String {
        key.isText ? "a Text of at most \(number(key.maxLength)) UTF-8 bytes" : "an integer between \(number(key.minimum)) and \(number(key.maximum))"
    }

    private static func number(_ value: Int64) -> String { value == .min ? "Int64.min" : String(value) }

    // A text form for the documentation, such as `@Drone:7~120`.
    private static func example(_ handle: GesModelHandle, _ root: GesModelHandle?) -> String {
        let owner = root ?? handle
        let text = "@" + handle.name + ":" + (owner.id!.isText ? "north" : "7")
        let revision = handle.version == .revision ? "~120" : ""
        if !handle.hasPart { return text + revision }
        let part = "/" + (handle.part!.isText ? "hangar" : "2")
        return handle.isKeyed ? text + part + revision : text + revision + part
    }

    // The support helpers of maps with handle keys.
    static func handleHelpers(_ code: inout Code) {
        code.blank()
        code.open("static func handleMap<K, V>(_ map: [K: V], _ key: (K) -> GesValue, _ value: (V) -> GesValue) -> GesValue {")
        code.line("var keys: [GesValue] = []")
        code.line("var values: [GesValue] = []")
        code.line("keys.reserveCapacity(map.count)")
        code.line("values.reserveCapacity(map.count)")
        code.open("for (entryKey, entryValue) in map {")
        code.line("keys.append(key(entryKey))")
        code.line("values.append(value(entryValue))")
        code.close()
        code.line("return try! .map(keyValues: keys, values: values)")
        code.close()
        code.blank()
        code.open("static func handleMap<K, V>(_ value: GesValue, _ key: (GesValue) -> K?, _ element: (GesValue) -> V?) -> [K: V]? {")
        code.line("guard value.valueKind == .map, value.customTypeName == nil, let map = value.asMap else { return nil }")
        code.line("var entries: [K: V] = [:]")
        code.line("entries.reserveCapacity(map.length)")
        code.open("for index in 0..<map.length {")
        code.line(
            "guard let handle = map.handleKey(at: index), let entryKey = key(.handle(handle)), let entryValue = element(map.value(at: index)), entries.updateValue(entryValue, forKey: entryKey) == nil else { return nil }"
        )
        code.close()
        code.line("return entries")
        code.close()
    }
}
