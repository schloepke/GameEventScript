// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// What every emitter generates, independent of the language: per type its members in wire order with their node offsets,
// the field paths in node order for errors, and the nested types it declares. Names are bases in PascalCase that each
// emitter spells for its language. A type that holds a Text or List, directly or through a nested type, is a class, whose
// storage grows on demand; other types are structs.
final class GesPlanType {
    var layout = GesLayoutType()
    var name = ""
    var kind = GesSchemaKind.schema
    var isClass = false

    // A synthesized list element type, which shares the paths of its owner.
    var isItem = false

    // The depth of a recursive schema's List of itself, 0 for other types. Only such a schema takes the level of its record
    // in the recursion, since its deepest List holds nothing; every other type keeps constant offsets.
    var depth = 0
    var paths: [String] = []
    var fields: [GesPlanField] = []

    // A message's tags, each a Boolean member at its node.
    var tags: [(tag: String, base: String, node: Int)] = []

    // A message set's members, named after their messages.
    var members: [String] = []
    var membersAreClasses = false
}

// A packed schema's word as wire code reads it: its type, its bits and each field of a fixed width below it, with the node
// whose path an error names, its first bit and the largest valid index when its bits allow more.
final class GesPackedWord {
    var type = ""
    var bits = 0
    var width = 0
    var leaves: [(node: Int, shift: Int, width: Int, largest: UInt64?)] = []

    // Whether every word of its bits is valid, so that a read needs no check.
    var full: Bool { leaves.allSatisfy { $0.largest == nil } }

    static func of(_ layout: GesSchemaLayout, _ name: String, _ node: Int) -> GesPackedWord {
        let type = layout.type(name)
        let word = GesPackedWord()
        word.type = name
        word.bits = type.packedBits
        word.width = type.packedWidth
        var shift = 0
        _ = add(layout, type, node + 1, &shift, &word.leaves)
        return word
    }

    // The leaves of a packed type's fields from `node` on; a nested packed schema has a group node before its own.
    private static func add(_ layout: GesSchemaLayout, _ type: GesLayoutType, _ start: Int, _ shift: inout Int, _ leaves: inout [(node: Int, shift: Int, width: Int, largest: UInt64?)]) -> Int {
        var node = start
        for field in type.fields {
            if field.encoding.kind == .schema {
                node = add(layout, layout.type(field.encoding.schema), node + 1, &shift, &leaves)
                continue
            }
            leaves.append((node, shift, field.encoding.bits, packedLargest(field.encoding)))
            shift += field.encoding.bits
            node += 1
        }
        return node
    }

    // The largest valid index of a field, nil when every index of its bits is valid.
    private static func packedLargest(_ encoding: GesLayoutEncoding) -> UInt64? {
        let full: UInt64 = encoding.width >= 64 ? UInt64.max : (1 << UInt64(encoding.width)) - 1
        let largest: UInt64 =
            switch encoding.kind {
            case .tag: UInt64(encoding.tags.count) &- 1
            case .integer, .quantized: encoding.maxIndex
            default: full
            }
        return largest < full ? largest : nil
    }
}

struct GesPlanField {
    var layout = GesLayoutField()
    var base = ""
    var optional = false
    var key = false
    var value = GesPlanValue()
}

// One encoding at a node, stored in the member named by base. A union holds its alternatives, a Point or Vector its axes,
// and a List its element: a value of the element encoding, or a synthesized item type for a union, Text or List.
final class GesPlanValue {
    var base = ""
    var label = ""
    var node = 0
    var encoding = GesLayoutEncoding()
    var axes: [GesPlanValue] = []
    // The corners of a Box, as the Points that its min and max hold, or the centre and the radius of a Disc.
    var corners: [GesPlanValue] = []
    var alternatives: [GesPlanValue] = []
    var element: GesPlanValue?
    var item: GesPlanType?

    // Whether the value refers to a type that is a class.
    var isClass = false

    // The word of a packed schema that the value refers to, nil for every other value.
    var packed: GesPackedWord?

    // Whether the value is the item element of a recursive List of a union, whose item takes the level of the record that
    // holds the List.
    var passesLevel = false
    // Whether the value is the element of a recursive List: its schema's record one level deeper, whose nodes follow the
    // declaring schema's own nodes.
    var recursive = false

    // The depth of a recursive List of the schema that holds it, 0 for every other value. Where `with` copies such a List
    // into another schema, it is an ordinary List there.
    var depth = 0

    // How an emitter spells the type of a Tag of a named tag set, which is declared once beside the records.
    var tagType = ""

    init(base: String = "", label: String = "", node: Int = 0, encoding: GesLayoutEncoding = GesLayoutEncoding(), isClass: Bool = false) {
        self.base = base
        self.label = label
        self.node = node
        self.encoding = encoding
        self.isClass = isClass
    }
}

extension GesSchemaLayout {
    func type(_ name: String) -> GesLayoutType { types.first { $0.name == name }! }
}

enum GesSchemaPlanner {
    // The named tag sets the types use, in order of first use, with their members; each Tag of a set gets the type that an
    // emitter spells with the qualifier in front of the set's name.
    static func tagSets(_ types: [GesPlanType], _ qualifier: String) -> [(name: String, tags: [String])] {
        var sets: [(name: String, tags: [String])] = []
        var seen: Set<String> = []
        var visited: Set<ObjectIdentifier> = []

        func visitType(_ type: GesPlanType) {
            if !visited.insert(ObjectIdentifier(type)).inserted { return }
            for field in type.fields { visitValue(field.value) }
        }

        func visitValue(_ value: GesPlanValue) {
            if value.encoding.kind == .tag && !value.encoding.tagSet.isEmpty {
                value.tagType = qualifier + value.encoding.tagSet
                if seen.insert(value.encoding.tagSet).inserted { sets.append((value.encoding.tagSet, value.encoding.tags)) }
            }
            for alternative in value.alternatives { visitValue(alternative) }
            if let element = value.element { visitValue(element) }
            if let item = value.item { visitType(item) }
        }

        for type in types { visitType(type) }
        return sets
    }

    // The schemas that are elements of a keyed list, which compare and read their keys for the list's delta.
    static func keyedElements(_ types: [GesPlanType]) -> Set<String> {
        var elements = Set<String>()
        for type in types {
            for field in type.fields { keyedElements(field.value, &elements) }
        }
        return elements
    }

    private static func keyedElements(_ value: GesPlanValue, _ elements: inout Set<String>) {
        if keyed(value) { elements.insert(value.element!.encoding.schema) }
        for alternative in value.alternatives { keyedElements(alternative, &elements) }
        if let item = value.item {
            for field in item.fields { keyedElements(field.value, &elements) }
        } else if let element = value.element {
            keyedElements(element, &elements)
        }
    }

    // Whether a value holds storage that a read writes into besides the record's own fields: a class schema, a list of classes
    // with its pool or, when lists count as storage, any list.
    static func holdsStorage(_ value: GesPlanValue, lists: Bool) -> Bool {
        switch value.encoding.kind {
        case .schema: value.isClass
        case .list: lists || value.element!.isClass
        case .union: value.alternatives.contains { holdsStorage($0, lists: lists) }
        default: false
        }
    }

    // The axes of a Point or Vector that its block leaves out: they are not transmitted, are written only when zero and read as zero.
    static func omitted(_ value: GesPlanValue) -> [String] {
        ["X", "Y", "Z"].filter { axis in !value.axes.contains { $0.base == axis } }
    }

    static func keyed(_ value: GesPlanValue) -> Bool { value.encoding.kind == .list && value.encoding.keyed }

    static func hasKeyed(_ value: GesPlanValue) -> Bool { keyed(value) || value.alternatives.contains(where: hasKeyed) }

    static func plan(_ layout: GesSchemaLayout) throws(GesGeneratorError) -> [GesPlanType] {
        let context = Context(layout)
        return layout.types.map { type(context, $0) }
    }

    private final class Context {
        let layout: GesSchemaLayout
        var counts: [String: Int] = [:]
        var paths: [String: [String]] = [:]
        var classes: [String: Bool] = [:]

        init(_ layout: GesSchemaLayout) { self.layout = layout }
    }

    private static func type(_ context: Context, _ type: GesLayoutType) -> GesPlanType {
        let plan = GesPlanType()
        plan.layout = type
        plan.name = type.name
        plan.kind = type.kind
        plan.isClass = isClass(context, type.name)
        if type.kind == .messages {
            plan.members = type.members
            for member in type.members where isClass(context, member) { plan.membersAreClasses = true }
            return plan
        }
        plan.paths = paths(context.layout, type, &context.paths)
        var node = 0
        for field in type.fields {
            var planned = GesPlanField(layout: field, base: member(field.name, type.name), optional: field.optional, key: field.key)
            planned.value = value(context, field.encoding, planned.base, "", node)
            node += count(context.layout, field.encoding, &context.counts, owner: type.name)
            plan.fields.append(planned)
        }
        for tag in type.tags {
            plan.tags.append((tag, member(tag, type.name), node))
            node += 1
        }
        // The element of a recursive List and its nodes come after the schema's own nodes, so that every own node keeps its
        // offset at every level and only the element's subtree, whose size depends on the level, varies.
        for field in plan.fields where recursive(field.layout.encoding, type.name) {
            plan.depth = field.value.encoding.depth
            field.value.depth = field.value.encoding.depth
            field.value.element!.node = node
            guard let item = field.value.item else {
                field.value.element!.recursive = true
                continue
            }
            // A union of scalars and the schema itself: the item takes the level of the record that holds the List, and the
            // schema's nodes come after the union's own ones, so that every other alternative keeps its offset at every level.
            item.depth = plan.depth
            field.value.element!.passesLevel = true
            selfLast(context, item.fields[0].value, type.name)
        }
        return plan
    }

    private static func value(_ context: Context, _ encoding: GesLayoutEncoding, _ name: String, _ label: String, _ node: Int) -> GesPlanValue {
        let value = GesPlanValue(base: name, label: label, node: node, encoding: encoding, isClass: holdsStorage(context, encoding))
        if encoding.kind == .schema && context.layout.type(encoding.schema).packedWidth > 0 { value.packed = GesPackedWord.of(context.layout, encoding.schema, node) }
        switch encoding.kind {
        case .point, .vector:
            var axisNode = node + 1
            for axis in encoding.axes {
                value.axes.append(GesPlanValue(base: pascal(axis.name), label: axis.name, node: axisNode, encoding: axis.encoding))
                axisNode += 1
            }
        case .box:
            var cornerNode = node + 1
            for corner in encoding.corners {
                value.corners.append(self.value(context, corner.encoding, corner.name == "lower" ? "Min" : "Max", corner.name, cornerNode))
                cornerNode += 1 + corner.encoding.axes.count
            }
        case .disc:
            var partNode = node + 1
            for part in encoding.corners {
                value.corners.append(self.value(context, part.encoding, pascal(part.name), part.name, partNode))
                partNode += 1 + part.encoding.axes.count
            }
        case .union:
            var alternativeNode = node + 1
            for alternative in encoding.alternatives {
                value.alternatives.append(self.value(context, alternative.encoding, name + pascal(alternative.name), pascal(alternative.name), alternativeNode))
                alternativeNode += count(context.layout, alternative.encoding, &context.counts)
            }
        case .list:
            let element = encoding.element!.encoding
            if element.kind == .union || element.kind == .text || element.kind == .bytes || element.kind == .list {
                // The item's single field sits at node 0 of the paths its owner passes on.
                let item = GesPlanType()
                item.name = name + "Item"
                item.isItem = true
                item.isClass = holdsStorage(context, element)
                item.fields.append(GesPlanField(layout: encoding.element!, base: "Value", value: self.value(context, element, "Value", "", 0)))
                value.item = item
                value.element = GesPlanValue(base: name + "Item", node: node + 1, encoding: element, isClass: item.isClass)
            } else {
                value.element = self.value(context, element, name + "Item", "", node + 1)
            }
        default:
            break
        }
        return value
    }

    // Whether values of an encoding need storage that grows, so that their type is a class.
    private static func holdsStorage(_ context: Context, _ encoding: GesLayoutEncoding) -> Bool {
        switch encoding.kind {
        case .text, .bytes, .list: true
        case .schema: isClass(context, encoding.schema)
        case .union: encoding.alternatives.contains { holdsStorage(context, $0.encoding) }
        default: false
        }
    }

    private static func isClass(_ context: Context, _ name: String) -> Bool {
        if let known = context.classes[name] { return known }
        let type = context.layout.type(name)
        let result = type.kind == .messages ? type.members.contains { isClass(context, $0) } : type.fields.contains { holdsStorage(context, $0.encoding) }
        context.classes[name] = result
        return result
    }

    // Plans the alternatives of a recursive union again, with the schema's alternative after the others.
    private static func selfLast(_ context: Context, _ union: GesPlanValue, _ owner: String) {
        let alternatives = union.encoding.alternatives
        var node = union.node + 1
        var selfIndex = 0
        for (index, alternative) in alternatives.enumerated() {
            if alternative.encoding.schema == owner {
                selfIndex = index
                continue
            }
            union.alternatives[index] = value(context, alternative.encoding, union.base + pascal(alternative.name), pascal(alternative.name), node)
            node += count(context.layout, alternative.encoding, &context.counts)
        }
        union.alternatives[selfIndex].node = node
        union.alternatives[selfIndex].recursive = true
    }

    // Whether an encoding is a recursive List of the schema that holds it, alone or as an alternative of its element.
    private static func recursive(_ encoding: GesLayoutEncoding, _ owner: String) -> Bool {
        encoding.kind == .list && encoding.depth > 0 && selfAlternative(encoding.element!)?.encoding.schema == owner
    }

    // The element of a recursive List that is its schema: the element itself or the schema alternative of its union.
    private static func selfAlternative(_ element: GesLayoutField) -> GesLayoutField? {
        element.encoding.kind == .union ? element.encoding.alternatives.first { $0.encoding.kind == .schema } : element
    }

    // The nodes of a recursive List's element at one level, without the schema's own: one for a schema, and for a union the
    // union, its other alternatives and the schema's group.
    private static func elementNodes(_ layout: GesSchemaLayout, _ list: GesLayoutEncoding, _ owner: String, _ counts: inout [String: Int]) -> Int {
        let element = list.element!.encoding
        if element.kind != .union { return 1 }
        var total = 2
        for alternative in element.alternatives where alternative.encoding.schema != owner { total += count(layout, alternative.encoding, &counts) }
        return total
    }

    // The number of nodes an encoding occupies in a bound layout; the element of a recursive List of `owner` is counted
    // apart, after its own nodes.
    static func count(_ layout: GesSchemaLayout, _ encoding: GesLayoutEncoding, _ counts: inout [String: Int], owner: String = "") -> Int {
        switch encoding.kind {
        case .list:
            return recursive(encoding, owner) ? 1 : 1 + count(layout, encoding.element!.encoding, &counts)
        case .point, .vector:
            return 1 + encoding.axes.count
        case .box:
            return 1 + 2 * (1 + encoding.corners[0].encoding.axes.count)
        case .disc:
            return encoding.corners.count + 1 + encoding.corners[0].encoding.axes.count
        case .union:
            var total = 1
            for alternative in encoding.alternatives { total += count(layout, alternative.encoding, &counts) }
            return total
        case .schema:
            if let nested = counts[encoding.schema] { return 1 + nested }
            let nested = nodes(layout, layout.type(encoding.schema), 1, &counts)
            counts[encoding.schema] = nested
            return 1 + nested
        default:
            return 1
        }
    }

    // The nodes of a schema's fields at a level of its recursion: its own nodes and, below the depth of a recursive List, the
    // element and its nodes one level deeper.
    private static func nodes(_ layout: GesSchemaLayout, _ type: GesLayoutType, _ level: Int, _ counts: inout [String: Int]) -> Int {
        var total = 0
        var recursiveList: GesLayoutEncoding?
        for field in type.fields {
            total += count(layout, field.encoding, &counts, owner: type.name)
            if recursive(field.encoding, type.name) { recursiveList = field.encoding }
        }
        guard let list = recursiveList, level < list.depth else { return total }
        return total + elementNodes(layout, list, type.name, &counts) + nodes(layout, type, level + 1, &counts)
    }

    // The paths of a type's nodes in order; a nested schema's paths follow its field's path, and a message's tags its fields.
    // A recursive List's element and its paths come last, one level deeper each time.
    private static func paths(_ layout: GesSchemaLayout, _ type: GesLayoutType, _ cache: inout [String: [String]]) -> [String] {
        if let known = cache[type.name] { return known }
        let paths = self.paths(layout, type, 1, &cache)
        cache[type.name] = paths
        return paths
    }

    private static func paths(_ layout: GesSchemaLayout, _ type: GesLayoutType, _ level: Int, _ cache: inout [String: [String]]) -> [String] {
        var paths: [String] = []
        for field in type.fields { self.paths(layout, field.path, field.encoding, &paths, &cache, owner: type.name) }
        for tag in type.tags { paths.append("#" + tag) }
        for field in type.fields where recursive(field.encoding, type.name) && level < field.encoding.depth {
            let element = field.encoding.element!
            paths.append(element.path)
            var selfPath = element.path
            if element.encoding.kind == .union {
                // The schema's alternative and its nodes come after the other alternatives.
                for alternative in element.encoding.alternatives where alternative.encoding.schema != type.name {
                    self.paths(layout, alternative.path, alternative.encoding, &paths, &cache)
                }
                selfPath = selfAlternative(element)!.path
                paths.append(selfPath)
            }
            for nested in self.paths(layout, type, level + 1, &cache) { paths.append(selfPath + "." + nested) }
        }
        return paths
    }

    private static func paths(_ layout: GesSchemaLayout, _ path: String, _ encoding: GesLayoutEncoding, _ paths: inout [String], _ cache: inout [String: [String]], owner: String = "") {
        paths.append(path)
        switch encoding.kind {
        case .list:
            if !recursive(encoding, owner) { self.paths(layout, encoding.element!.path, encoding.element!.encoding, &paths, &cache) }
        case .point, .vector:
            for axis in encoding.axes { paths.append(axis.path) }
        case .box, .disc:
            for corner in encoding.corners { self.paths(layout, corner.path, corner.encoding, &paths, &cache) }
        case .union:
            for alternative in encoding.alternatives { self.paths(layout, alternative.path, alternative.encoding, &paths, &cache) }
        case .schema:
            for nested in self.paths(layout, layout.type(encoding.schema), &cache) { paths.append(path + "." + nested) }
        default:
            break
        }
    }

    // A string literal with backslashes and quotes escaped, the same in C# and Swift.
    static func quote(_ text: String) -> String {
        var result = "\""
        for character in text {
            if character == "\\" || character == "\"" { result += "\\" }
            result.append(character)
        }
        return result + "\""
    }

    // A string literal that stays within printable ASCII: other characters become escapes, \uXXXX or \UXXXXXXXX in C#
    // and \u{X} in Swift, so that both ports write identical text whatever a source holds.
    /// A let as a code literal: a whole value for an integer, otherwise its exact decimal, which C# and Swift round alike.
    static func constantValue(_ constant: GesLayoutConstant) -> String {
        if !constant.integer { return decimal(constant.significand, constant.exponent) }
        var value = constant.significand
        for _ in 0..<constant.exponent { value *= 10 }
        return String(value)
    }

    /// A let as its source declares it, for generated documentation: a Percentage in percent, a Quantity with its symbol.
    static func constantSource(_ constant: GesLayoutConstant) -> String {
        "let \(constant.name) be \(decimal(constant.significand, constant.exponent + (constant.suffix == "%" ? 2 : 0)))\(constant.suffix)"
    }

    /// Plain digits within 18 places of the point, otherwise <significand>e<exponent>.
    static func decimal(_ significand: Int64, _ exponent: Int) -> String {
        let sign = significand < 0 ? "-" : ""
        let digits = String(significand.magnitude)
        if exponent >= 0 && digits.count + exponent <= 18 { return sign + digits + String(repeating: "0", count: exponent) }
        if exponent < 0 && -exponent <= 18 {
            let point = digits.count + exponent
            return sign + (point > 0 ? String(digits.prefix(point)) + "." + String(digits.dropFirst(point)) : "0." + String(repeating: "0", count: -point) + digits)
        }
        return sign + digits + "e" + String(exponent)
    }

    static func literal(_ text: String, swift: Bool) -> String {
        var result = "\""
        for scalar in text.unicodeScalars {
            switch scalar.value {
            case 0x5C: result += "\\\\"
            case 0x22: result += "\\\""
            case 0x09: result += "\\t"
            case 0x0D: result += "\\r"
            case 0x0A: result += "\\n"
            case 0x20..<0x7F: result.unicodeScalars.append(scalar)
            default:
                let hex = String(scalar.value, radix: 16, uppercase: true)
                result += swift ? "\\u{" + hex + "}" : scalar.value > 0xFFFF ? "\\U" + pad(hex, 8) : "\\u" + pad(hex, 4)
            }
        }
        return result + "\""
    }

    // The lines of a text split at each line feed, by Unicode scalars as in C#, where `\r\n` is no single character.
    static func lines(_ text: String) -> [String] {
        text.unicodeScalars.split(separator: "\n", omittingEmptySubsequences: false).map { String(String.UnicodeScalarView($0)) }
    }

    private static func pad(_ text: String, _ count: Int) -> String { String(repeating: "0", count: max(0, count - text.count)) + text }

    // The FNV-1a 64 hash of a text's UTF-8 bytes in lowercase hex, which names a helper type uniquely per generated file.
    static func hash(_ text: String) -> String {
        var hash: UInt64 = 14_695_981_039_346_656_037
        for value in text.utf8 { hash = (hash ^ UInt64(value)) &* 1_099_511_628_211 }
        return GesSchemaLayoutJson.hex(hash)
    }

    // A member that would carry its type's name gets a suffix, since C# forbids such a member.
    private static func member(_ name: String, _ type: String) -> String { pascal(name) == type ? pascal(name) + "Value" : pascal(name) }

    static func pascal(_ name: String) -> String { name.isEmpty ? name : name.prefix(1).uppercased() + name.dropFirst() }

    static func camel(_ name: String) -> String { name.isEmpty ? name : name.prefix(1).lowercased() + name.dropFirst() }

    // Qualifies the packed types that values refer to, everywhere below them.
    static func qualifyPacked(_ value: GesPlanValue, _ qualifier: String) {
        if let word = value.packed, !word.type.contains(".") { word.type = qualifier + word.type }
        if let element = value.element { qualifyPacked(element, qualifier) }
        if let item = value.item {
            for field in item.fields { qualifyPacked(field.value, qualifier) }
        }
        for alternative in value.alternatives { qualifyPacked(alternative, qualifier) }
    }

    // Checks a namespace of dot-separated identifiers that are no keywords of the language.
    static func namespace(_ text: String, _ keywords: Set<String>) throws(GesGeneratorError) -> [String] {
        if text.isEmpty { return [] }
        var parts: [String] = []
        for part in text.split(separator: ".", omittingEmptySubsequences: false).map(String.init) {
            var valid = !part.isEmpty && (isLetter(part.utf8.first!) || part.utf8.first! == UInt8(ascii: "_")) && !keywords.contains(part)
            for byte in part.utf8 { valid = valid && (isLetter(byte) || byte == UInt8(ascii: "_") || (byte >= 0x30 && byte <= 0x39)) }
            if !valid { throw GesGeneratorError(GesGeneratorError.invalidNamespace, "The namespace '\(text)' is no sequence of identifiers.") }
            parts.append(part)
        }
        return parts
    }

    private static func isLetter(_ byte: UInt8) -> Bool { (byte >= 0x61 && byte <= 0x7A) || (byte >= 0x41 && byte <= 0x5A) }

    // Fails when two generated members of one scope get the same name.
    // A record's nested types are named after its fields. Each would hide a top-level type or tag set of the same name for
    // every reference inside the record, so that name fails like any other collision; their origins carry this prefix.
    static let nestedTypeOrigin = "the nested type "

    static func topLevel(_ types: [GesPlanType], _ sets: [(name: String, tags: [String])]) -> Set<String> {
        var names = Set(types.filter { !$0.isItem }.map(\.name))
        for set in sets { names.insert(set.name) }
        return names
    }

    static func unique(_ scope: String, _ names: [(name: String, origin: String)], _ topLevel: Set<String>) throws(GesGeneratorError) {
        try unique(scope, names)
        for (name, origin) in names {
            if origin.hasPrefix(nestedTypeOrigin) && topLevel.contains(name) {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(scope)', \(origin) is generated as '\(name)', which hides the top-level type of that name.")
            }
            if isRuntimeName(name) {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(scope)', \(origin) is generated as '\(name)', a name of the runtime types that generated code uses.")
            }
        }
    }

    // Generated Swift names these standard-library and runtime types without qualification, so a type of the same name in
    // the namespace would hide them; Type, Any, AnyObject, Protocol and Self cannot name a nested type at all. Generated C#
    // qualifies the platform types it uses with global::, so only the runtime's names are reserved there.
    private static let swiftReserved: Set<String> = [
        "Any", "AnyObject", "Array", "Bool", "CaseIterable", "Codable", "Double", "Equatable", "Error", "GameEventScriptRuntime", "Hashable",
        "Identifiable", "Int", "Int32", "Int64", "OptionSet", "Protocol", "Result", "Self", "Sendable", "String", "Swift", "Type", "UInt64",
        "UInt8", "UTF8", "UnsafeMutableRawBufferPointer", "UnsafeRawBufferPointer",
    ]

    // Names of the form Ges and a capital letter belong to the runtime types that generated code names without qualification.
    private static func isRuntimeName(_ name: String) -> Bool {
        let scalars = Array(name.unicodeScalars)
        return scalars.count > 3 && name.hasPrefix("Ges") && ("A"..."Z").contains(scalars[3])
    }

    // How many leading Swift namespace components another generation declares, which this one extends: the `extended` ones
    // that extendNamespace names, and those that the wire code shares with the models' namespace.
    static func extended(_ space: [String], _ extended: Int, _ models: [String]?) -> Int {
        guard let models else { return extended }
        var common = 0
        while common < space.count && common < models.count && space[common] == models[common] { common += 1 }
        return max(extended, common)
    }

    // Reserves the `<Name>Wire` type of every packed wire root that the models declare, which holds its wire members beside the
    // records, so that no record or tag set takes that name.
    static func reserveSharedRoots(_ layout: GesSchemaLayout, _ types: [GesPlanType], _ names: inout Set<String>) throws(GesGeneratorError) {
        for type in types where type.layout.packedWidth > 0 && layout.roots.contains(type.name) {
            if !names.insert(type.name + "Wire").inserted {
                throw GesGeneratorError(
                    GesGeneratorError.nameCollision,
                    "The wire members of the packed root '\(type.name)' take the name '\(type.name)Wire', which another type of the generation has."
                )
            }
        }
    }

    // Swift wire code names the models' types through their namespace from inside its own, where a wire type, a type nested in
    // a record or a later component of the wire namespace of the same name as the first component would hide it. Reserving
    // that name among the top-level ones also makes nested types of that name fail.
    static func reserveModelsRoot(_ space: [String], _ models: [String], _ names: inout Set<String>) throws(GesGeneratorError) {
        guard let root = models.first else { return }
        for part in space.dropFirst() where part == root {
            throw GesGeneratorError(
                GesGeneratorError.nameCollision,
                "The component '\(part)' of the wire namespace hides the models' namespace '\(root)', through which the wire code names their types."
            )
        }
        if !names.insert(root).inserted {
            throw GesGeneratorError(
                GesGeneratorError.nameCollision,
                "The wire type '\(root)' hides the models' namespace '\(root)', through which the wire code names their types."
            )
        }
    }

    // The types declared beside the records must not take a name that the generated code uses for another type.
    static func checkTopLevel(_ names: Set<String>, swift: Bool) throws(GesGeneratorError) {
        for name in names.sorted(by: { $0.unicodeScalars.lexicographicallyPrecedes($1.unicodeScalars) }) {
            if isRuntimeName(name) {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "The type '\(name)' takes a name of the runtime types that generated code uses.")
            }
            if swift && swiftReserved.contains(name) {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "The type '\(name)' takes a name of a Swift type that generated code uses.")
            }
        }
    }

    static func unique(_ scope: String, _ names: [(name: String, origin: String)]) throws(GesGeneratorError) {
        var seen: [String: String] = [:]
        for (name, origin) in names {
            if let first = seen[name] {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(scope)', \(first) and \(origin) are both generated as '\(name)'.")
            }
            seen[name] = origin
        }
    }

    /// The suffix of helper type names: unique per single file, and for split output stable across schema and root changes,
    /// so that a change touches only the files of the declarations it changes. Split code of one namespace is one generation.
    static func suffix(_ header: String, _ options: GesSchemaGenerateOptions) -> String {
        hash(options.split == .none ? header : "split\n" + options.namespace)
    }

    static func header(_ layout: GesSchemaLayout) -> String {
        var text = "// <auto-generated>\n"
        text += "//     Generated by `ges schema generate` from \(layout.roots.count == 1 ? "the root " : "the roots ")\(layout.roots.joined(separator: ", ")).\n"
        text += "//     Do not edit it; generate it again when a schema or a parameter changes.\n"
        for parameter in layout.parameters {
            text += "//     Parameter \(parameter.name) = \(parameter.value)\(parameter.unit == "none" ? "" : " " + parameter.unit).\n"
        }
        for root in layout.roots {
            let type = layout.type(root)
            text += "//     \(root): schema hash \(GesSchemaLayoutJson.hex(type.hash)), binding hash \(GesSchemaLayoutJson.hex(type.bindingHash)).\n"
        }
        text += "// </auto-generated>\n"
        return text
    }

    // Writes code with four spaces of indentation per level.
    struct Code {
        private(set) var text = ""
        private(set) var depth: Int

        init(depth: Int = 0) { self.depth = depth }

        mutating func line(_ line: String) {
            if !line.isEmpty { text += String(repeating: " ", count: depth * 4) + line }
            text += "\n"
        }

        mutating func blank() { text += "\n" }

        mutating func open(_ line: String) {
            self.line(line)
            depth += 1
        }

        // Indents the following lines, after a line that close wrote, such as `} else {`.
        mutating func indent() { depth += 1 }

        // Ends an indentation that indent began, without writing a line.
        mutating func dedent() { depth -= 1 }

        mutating func close(_ line: String = "}") {
            depth -= 1
            self.line(line)
        }

        mutating func raw(_ text: String) { self.text += text }
    }
}

extension GesSchemaPlanner {
    /// The translator of wire records' computed fields: an optional field on a path is read only where its presence flag is
    /// set, and a computed field through its property or exact value.
    static func computed(_ types: [GesPlanType], _ helper: String, swift: Bool) -> GesComputedEmitter {
        GesComputedEmitter(swift: swift, helper: helper, access: GesWireAccess(types: types, swift: swift))
    }
}

private struct GesWireAccess: GesComputedAccess {
    let types: [GesPlanType]
    let swift: Bool

    func reach(_ root: String, _ owner: String, _ path: [String]) -> GesComputedReach {
        let reach = GesComputedReach(text: root)
        var type = self.type(owner)
        var value: GesPlanValue?
        for (step, name) in path.enumerated() {
            guard let field = type!.fields.first(where: { $0.layout.name == name }) else {
                reach.computedOwner = type!.name
                reach.computedName = name
                return reach
            }
            if field.optional { reach.conditions.append((step, GesComputedEmitter.join(reach.text, (swift ? "has" : "Has") + field.base))) }
            reach.text = GesComputedEmitter.join(reach.text, member(field.base))
            value = field.value
            type = field.value.encoding.kind == .schema ? self.type(field.value.encoding.schema) : nil
        }
        if let value { Self.declare(value.encoding, reach) }
        return reach
    }

    func element(_ owner: String, _ path: [String]) -> GesComputedReach {
        let reach = GesComputedReach(text: "item")
        Self.declare(value(owner, path).element!.encoding, reach)
        return reach
    }

    func elementOwner(_ owner: String, _ path: [String]) -> String {
        let element = value(owner, path).element!
        return element.encoding.kind == .schema ? element.encoding.schema : ""
    }

    func computed(_ owner: String) -> [GesSchemaComputedFieldInfo] { type(owner)!.layout.computed }

    /// The declared range of a number and the largest count of a list.
    private static func declare(_ encoding: GesLayoutEncoding, _ reach: GesComputedReach) {
        switch encoding.kind {
        case .integer:
            reach.integer = true
            reach.lower = encoding.range ? Double(encoding.lower) : Double(Int64.min)
            reach.upper = encoding.range ? Double(encoding.lower) + Double(encoding.maxIndex) : Double(Int64.max)
        case .quantized:
            reach.lower = encoding.lowerValue
            reach.upper = encoding.upperValue
        case .float where encoding.range:
            reach.lower = encoding.lowerValue
            reach.upper = encoding.upperValue
        case .list:
            reach.maxCount = Double(encoding.maxCount)
        default:
            break
        }
    }

    private func value(_ owner: String, _ path: [String]) -> GesPlanValue {
        var type = self.type(owner)
        var value: GesPlanValue?
        for name in path {
            value = type!.fields.first { $0.layout.name == name }!.value
            type = value!.encoding.kind == .schema ? self.type(value!.encoding.schema) : nil
        }
        return value!
    }

    private func type(_ name: String) -> GesPlanType? { name.isEmpty ? nil : types.first { $0.name == name } }

    private func member(_ pascal: String) -> String {
        if !swift { return pascal }
        let camel = GesSchemaPlanner.camel(pascal)
        return GesSwiftEmitter.keywords.contains(camel) ? "`" + camel + "`" : camel
    }
}
