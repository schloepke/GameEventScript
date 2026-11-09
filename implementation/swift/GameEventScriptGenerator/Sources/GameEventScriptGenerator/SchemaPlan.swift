// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// What every emitter generates, independent of the language: per type its members in wire order with their node offsets,
// the field paths in node order for errors, and the nested types it declares. Names are bases in PascalCase that each
// emitter spells for its language.
struct GesPlanType {
    var layout = GesLayoutType()
    var name = ""
    var paths: [String] = []
    var fields: [GesPlanField] = []
}

struct GesPlanField {
    var layout = GesLayoutField()
    var base = ""
    var optional = false
    var key = false
    var value = GesPlanValue()
}

// One encoding at a node, stored in the member named by base. A union holds its alternatives, a Point or Vector its axes.
struct GesPlanValue {
    var base = ""
    var label = ""
    var node = 0
    var encoding = GesLayoutEncoding()
    var axes: [GesPlanValue] = []
    var alternatives: [GesPlanValue] = []
}

extension GesSchemaLayout {
    func type(_ name: String) -> GesLayoutType { types.first { $0.name == name }! }
}

enum GesSchemaPlanner {
    static func plan(_ layout: GesSchemaLayout) throws(GesGeneratorError) -> [GesPlanType] {
        var counts: [String: Int] = [:]
        var paths: [String: [String]] = [:]
        var result: [GesPlanType] = []
        for type in layout.types {
            if type.kind != .schema {
                throw GesGeneratorError(GesGeneratorError.unsupported, "The \(type.kind == .message ? "message" : "message set") '\(type.name)' cannot be generated yet.")
            }
            result.append(try self.type(layout, type, &counts, &paths))
        }
        return result
    }

    private static func type(_ layout: GesSchemaLayout, _ type: GesLayoutType, _ counts: inout [String: Int], _ paths: inout [String: [String]]) throws(GesGeneratorError) -> GesPlanType {
        var plan = GesPlanType(layout: type, name: type.name, paths: self.paths(layout, type, &paths))
        var node = 0
        for field in type.fields {
            var planned = GesPlanField(layout: field, base: pascal(field.name), optional: field.optional, key: field.key)
            planned.value = try value(layout, type, field.encoding, planned.base, "", node, &counts)
            node += count(layout, field.encoding, &counts)
            plan.fields.append(planned)
        }
        return plan
    }

    private static func value(
        _ layout: GesSchemaLayout,
        _ owner: GesLayoutType,
        _ encoding: GesLayoutEncoding,
        _ name: String,
        _ label: String,
        _ node: Int,
        _ counts: inout [String: Int]
    ) throws(GesGeneratorError) -> GesPlanValue {
        var value = GesPlanValue(base: name, label: label, node: node, encoding: encoding)
        switch encoding.kind {
        case .text, .list:
            let kind = encoding.kind == .text ? (encoding.tagName ? "Tag with max" : "Text") : (encoding.dice ? "Dice" : "List")
            throw GesGeneratorError(GesGeneratorError.unsupported, "The field '\(name)' of '\(owner.name)' holds a \(kind), which cannot be generated yet.")
        case .point, .vector:
            var axisNode = node + 1
            for axis in encoding.axes {
                value.axes.append(GesPlanValue(base: pascal(axis.name), label: axis.name, node: axisNode, encoding: axis.encoding))
                axisNode += 1
            }
        case .union:
            var alternativeNode = node + 1
            for alternative in encoding.alternatives {
                value.alternatives.append(try self.value(layout, owner, alternative.encoding, name + pascal(alternative.name), alternative.name, alternativeNode, &counts))
                alternativeNode += count(layout, alternative.encoding, &counts)
            }
        default:
            break
        }
        return value
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

    // The paths of a type's nodes in order; a nested schema's paths follow its field's path.
    private static func paths(_ layout: GesSchemaLayout, _ type: GesLayoutType, _ cache: inout [String: [String]]) -> [String] {
        if let known = cache[type.name] { return known }
        var paths: [String] = []
        for field in type.fields { self.paths(layout, field.path, field.encoding, &paths, &cache) }
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

        mutating func close(_ line: String = "}") {
            depth -= 1
            self.line(line)
        }

        mutating func raw(_ text: String) { self.text += text }
    }
}
