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
    var paths: [String] = []
    var fields: [GesPlanField] = []

    // A message's tags, each a Boolean member at its node.
    var tags: [(tag: String, base: String, node: Int)] = []

    // A message set's members, named after their messages.
    var members: [String] = []
    var membersAreClasses = false
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
    var alternatives: [GesPlanValue] = []
    var element: GesPlanValue?
    var item: GesPlanType?

    // Whether the value refers to a type that is a class.
    var isClass = false

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
            node += count(context.layout, field.encoding, &context.counts)
            plan.fields.append(planned)
        }
        for tag in type.tags {
            plan.tags.append((tag, member(tag, type.name), node))
            node += 1
        }
        return plan
    }

    private static func value(_ context: Context, _ encoding: GesLayoutEncoding, _ name: String, _ label: String, _ node: Int) -> GesPlanValue {
        let value = GesPlanValue(base: name, label: label, node: node, encoding: encoding, isClass: holdsStorage(context, encoding))
        switch encoding.kind {
        case .point, .vector:
            var axisNode = node + 1
            for axis in encoding.axes {
                value.axes.append(GesPlanValue(base: pascal(axis.name), label: axis.name, node: axisNode, encoding: axis.encoding))
                axisNode += 1
            }
        case .union:
            var alternativeNode = node + 1
            for alternative in encoding.alternatives {
                value.alternatives.append(self.value(context, alternative.encoding, name + pascal(alternative.name), alternative.name, alternativeNode))
                alternativeNode += count(context.layout, alternative.encoding, &context.counts)
            }
        case .list:
            let element = encoding.element!.encoding
            if element.kind == .union || element.kind == .text || element.kind == .list {
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
        case .text, .list: true
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

    // The number of nodes an encoding occupies in a bound layout.
    static func count(_ layout: GesSchemaLayout, _ encoding: GesLayoutEncoding, _ counts: inout [String: Int]) -> Int {
        switch encoding.kind {
        case .list:
            return 1 + count(layout, encoding.element!.encoding, &counts)
        case .point, .vector:
            return 1 + encoding.axes.count
        case .union:
            var total = 1
            for alternative in encoding.alternatives { total += count(layout, alternative.encoding, &counts) }
            return total
        case .schema:
            if let nested = counts[encoding.schema] { return 1 + nested }
            var nested = 0
            for field in layout.type(encoding.schema).fields { nested += count(layout, field.encoding, &counts) }
            counts[encoding.schema] = nested
            return 1 + nested
        default:
            return 1
        }
    }

    // The paths of a type's nodes in order; a nested schema's paths follow its field's path, and a message's tags its fields.
    private static func paths(_ layout: GesSchemaLayout, _ type: GesLayoutType, _ cache: inout [String: [String]]) -> [String] {
        if let known = cache[type.name] { return known }
        var paths: [String] = []
        for field in type.fields { self.paths(layout, field.path, field.encoding, &paths, &cache) }
        for tag in type.tags { paths.append("#" + tag) }
        cache[type.name] = paths
        return paths
    }

    private static func paths(_ layout: GesSchemaLayout, _ path: String, _ encoding: GesLayoutEncoding, _ paths: inout [String], _ cache: inout [String: [String]]) {
        paths.append(path)
        switch encoding.kind {
        case .list:
            self.paths(layout, encoding.element!.path, encoding.element!.encoding, &paths, &cache)
        case .point, .vector:
            for axis in encoding.axes { paths.append(axis.path) }
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
    static func unique(_ scope: String, _ names: [(name: String, origin: String)]) throws(GesGeneratorError) {
        var seen: [String: String] = [:]
        for (name, origin) in names {
            if let first = seen[name] {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(scope)', \(first) and \(origin) are both generated as '\(name)'.")
            }
            seen[name] = origin
        }
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
        private var depth = 0

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
