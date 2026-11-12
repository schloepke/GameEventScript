// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

// The language-neutral model layout: every schema reached from the model roots, described without binding, since a model
// type is a plain value that is never encoded. Model emitters read only this, and its JSON form is compared between ports.
struct GesModelLayout {
    static let version = 1

    var roots: [String]
    var types: [GesModelType]
    /// The schemas whose fields reached types take unchanged with `with`, directly or through each other, by name.
    var protocols: [GesModelProtocol] = []
    /// The schema sources, with their file names only, which generated code that reads documents embeds.
    var sources: [GesSchemaSource] = []

    func type(_ name: String) -> GesModelType { types.first { $0.name == name }! }
}

struct GesModelType {
    var name = ""
    var hash: UInt64 = 0
    var fields: [GesModelField] = []
    var constants: [GesLayoutConstant] = []
    /// The schemas it takes unchanged with `with`, whose protocols or interfaces it adopts, in declared order.
    var takes: [String] = []
    /// The fields written `computed by`, which models derive from their other fields.
    var computed: [GesSchemaComputedFieldInfo] = []
}

/// A schema that types take with `with`: generated as a protocol (Swift) or interface (C#) with read-only properties. It
/// refines the protocols of the schemas it takes unchanged and requires its other fields whose type is shared by name, as
/// every field's type is except a union, Flags and a closed Tag written in place, which generated code declares per field.
struct GesModelProtocol {
    var name = ""
    var isAbstract = false
    var takes: [String] = []
    var fields: [GesModelField] = []
}

final class GesModelField {
    var name = ""
    var path = ""
    var optional = false
    var key = false
    var defaultValue: GesLayoutDefault?
    var value = GesModelValue()
}

enum GesModelKind {
    case boolean, number, tag, text, bytes, list, dice, point, vector, schema, union, nothing, flags, box, poly
}

final class GesModelValue {
    var kind = GesModelKind.boolean
    // Numbers: Number, Quantity or Percentage, the unit and the documented limits as canonical decimals or parameter names.
    var type = ""
    var unit = "none"
    var integer = false
    var wrapping = false
    var lower: String?
    var upper: String?
    var step: String?
    // A closed tag set, empty for any tag, or the members of Flags; the documented maximum of a Tag, Text, List or Dice.
    var tags: [String] = []
    var tagSet = ""
    var max: Int64?
    var element: GesModelField?
    // The depth of a recursive List, 0 for every other List.
    var depth = 0
    // The axes a Point or Vector declares, empty without an axis block; union alternatives; a schema reference.
    var axes: [String] = []
    var alternatives: [GesModelField] = []
    var schema = ""
}

enum GesModelLayoutBuilder {
    static func build(_ set: GesSchemaSet, roots: [String]) throws -> GesModelLayout {
        guard !roots.isEmpty else { throw GesGeneratorError(GesGeneratorError.usage, "Specify at least one root.") }
        var reached: [String: GesSchema] = [:]
        var rootNames: [String] = []
        for root in roots where !rootNames.contains(root) {
            let schema = try set.schema(root)
            if schema.kind != .schema { throw GesGeneratorError(GesGeneratorError.usage, "The model root '\(root)' is no schema.") }
            if schema.isAbstract {
                throw GesGeneratorError(GesGeneratorError.usage, "The model root '\(root)' is abstract; only its protocol is generated, where a model takes it.")
            }
            rootNames.append(root)
            reach(schema, &reached)
        }
        var types: [GesModelType] = []
        for name in reached.keys.sorted(by: GesSchemaLayoutBuilder.ordinal) {
            let schema = reached[name]!
            var type = GesModelType(name: schema.name, hash: schema.hash, constants: GesLayoutConstant.of(schema))
            type.computed = schema.computedFields
            for info in schema.fields {
                let field = try self.field(info.name, info.name, info.alternatives)
                field.optional = info.isOptional
                field.key = info.isKey
                field.defaultValue = GesLayoutDefault.of(info)
                type.fields.append(field)
            }
            type.takes = schema.takes.filter(\.isUnchanged).map(\.schema.name)
            types.append(type)
        }
        var layout = GesModelLayout(roots: rootNames, types: types)
        layout.sources = set.sources.map { source in
            let scalars = source.name.unicodeScalars
            let start = scalars.lastIndex { $0 == "/" || $0 == "\\" }.map { scalars.index(after: $0) } ?? scalars.startIndex
            return GesSchemaSource(name: String(scalars[start...]), text: source.text)
        }
        var protocols: [String: GesSchema] = [:]
        for schema in reached.values { collectProtocols(schema, &protocols) }
        for name in protocols.keys.sorted(by: GesSchemaLayoutBuilder.ordinal) {
            let schema = protocols[name]!
            var protocolType = GesModelProtocol(name: schema.name, isAbstract: schema.isAbstract)
            var inherited: Set<String> = []
            for take in schema.takes where take.isUnchanged {
                protocolType.takes.append(take.schema.name)
                for field in take.schema.fields { inherited.insert(field.name) }
            }
            for info in schema.fields where !inherited.contains(info.name) {
                let field = try self.field(info.name, info.name, info.alternatives)
                field.optional = info.isOptional
                field.key = info.isKey
                if shared(field.value) { protocolType.fields.append(field) }
            }
            layout.protocols.append(protocolType)
        }
        return layout
    }

    private static func collectProtocols(_ schema: GesSchema, _ protocols: inout [String: GesSchema]) {
        for take in schema.takes where take.isUnchanged && protocols[take.schema.name] == nil {
            protocols[take.schema.name] = take.schema
            collectProtocols(take.schema, &protocols)
        }
    }

    /// Whether generated code names the field's type alike in every type: not a union, Flags or a closed Tag written in place.
    private static func shared(_ value: GesModelValue) -> Bool {
        switch value.kind {
        case .union:
            let present = value.alternatives.filter { $0.value.kind != .nothing }
            return present.count == 1 && shared(present[0].value)
        case .flags: return false
        case .tag: return value.tags.isEmpty || !value.tagSet.isEmpty
        case .list: return value.element.map { shared($0.value) } ?? true
        default: return true
        }
    }

    private static func reach(_ schema: GesSchema, _ reached: inout [String: GesSchema]) {
        if reached[schema.name] != nil { return }
        reached[schema.name] = schema
        for field in schema.fields {
            for type in field.alternatives { reach(type, &reached) }
        }
    }

    private static func reach(_ type: GesSchemaType, _ reached: inout [String: GesSchema]) {
        if let schema = type.schema { reach(schema, &reached) }
        for element in type.element { reach(element, &reached) }
    }

    private static func field(_ name: String, _ path: String, _ alternatives: [GesSchemaType]) throws -> GesModelField {
        let field = GesModelField()
        field.name = name
        field.path = path
        if alternatives.count == 1 {
            field.value = try value(alternatives[0], path)
            return field
        }
        field.value.kind = .union
        for alternative in alternatives {
            let type = alternative.kind == .reference ? alternative.schema!.name : alternative.kind.rawValue
            let item = GesModelField()
            item.name = alternative.label.isEmpty ? type : alternative.label
            item.path = path + ":" + type
            item.value = try value(alternative, item.path)
            field.value.alternatives.append(item)
        }
        return field
    }

    private static func value(_ type: GesSchemaType, _ path: String) throws -> GesModelValue {
        let value = GesModelValue()
        switch type.kind {
        case .boolean:
            value.kind = .boolean
        case .number, .percentage, .quantity:
            value.kind = .number
            value.type = type.kind.rawValue
            value.unit = GesSchemaLayoutBuilder.unitName(type.kind, type.unit)
            value.integer = type.isInteger
            value.wrapping = type.isWrapping
            value.lower = bound(type.lower)
            value.upper = bound(type.upper)
            value.step = bound(type.step)
        case .tag:
            value.kind = .tag
            value.tags = type.tags
            value.tagSet = type.tagSet
            value.max = type.max
        case .flags:
            value.kind = .flags
            value.tags = type.tags
        case .text:
            value.kind = .text
            value.max = type.max
        case .bytes:
            value.kind = .bytes
            value.max = type.max
        case .list where !type.element.isEmpty:
            value.kind = .list
            value.max = type.max
            value.depth = type.depth
            value.element = try field("[]", path + "[]", type.element)
        case .dice:
            value.kind = .dice
            value.max = type.max
            value.lower = bound(type.lower)
            value.upper = bound(type.upper)
        case .point, .vector, .box, .poly:
            value.kind = type.kind == .point ? .point : type.kind == .vector ? .vector : type.kind == .box ? .box : .poly
            value.unit = GesSchemaLayoutBuilder.unitName(type.kind, type.unit)
            value.max = type.max
            value.axes = type.axes.map(\.name)
        case .reference:
            value.kind = .schema
            value.schema = type.schema!.name
        case .nothing:
            value.kind = .nothing
        default:
            let what = type.kind == .list ? "A List without an element type" : "A \(type.kind.rawValue)"
            throw GesGeneratorError(GesGeneratorError.unsupported, "\(what) at '\(path)' has no model type.")
        }
        return value
    }

    private static func bound(_ bound: GesSchemaBound?) -> String? {
        guard let bound else { return nil }
        return bound.isParameter ? bound.parameter : "\(bound.significand)e\(bound.exponent)"
    }
}

// The canonical JSON form of a model layout, in the manner of the generation layout.
enum GesModelLayoutJson {
    static func write(_ layout: GesModelLayout) -> String {
        var json = GesSchemaLayoutJson.Writer()
        json.beginObject()
        json.number("model", Int64(GesModelLayout.version))
        json.strings("roots", layout.roots)
        json.beginArray("types")
        for type in layout.types {
            json.beginObject()
            json.string("name", type.name)
            json.string("hash", GesSchemaLayoutJson.hex(type.hash))
            json.beginArray("fields")
            for field in type.fields { self.field(&json, field, member: true) }
            json.endArray()
            GesSchemaLayoutJson.constants(&json, type.constants)
            GesSchemaLayoutJson.computed(&json, type.computed)
            if !type.takes.isEmpty { json.strings("takes", type.takes) }
            json.endObject()
        }
        json.endArray()
        if !layout.protocols.isEmpty {
            json.beginArray("protocols")
            for protocolType in layout.protocols {
                json.beginObject()
                json.string("name", protocolType.name)
                json.boolean("abstract", protocolType.isAbstract)
                json.strings("takes", protocolType.takes)
                json.beginArray("fields")
                for field in protocolType.fields { self.field(&json, field, member: true) }
                json.endArray()
                json.endObject()
            }
            json.endArray()
        }
        json.endObject()
        return json.text + "\n"
    }

    private static func field(_ json: inout GesSchemaLayoutJson.Writer, _ field: GesModelField, member: Bool, value: Bool = false) {
        if value { json.beginValueObject() } else { json.beginObject() }
        json.string("name", field.name)
        json.string("path", field.path)
        if member {
            json.boolean("optional", field.optional)
            json.boolean("key", field.key)
            field.defaultValue?.write(&json)
        }
        json.key("type")
        self.value(&json, field.value)
        json.endObject()
    }

    private static func value(_ json: inout GesSchemaLayoutJson.Writer, _ value: GesModelValue) {
        json.beginValueObject()
        json.string("kind", kind(value.kind))
        switch value.kind {
        case .number:
            json.string("type", value.type)
            json.string("unit", value.unit)
            json.boolean("integer", value.integer)
            json.boolean("wrapping", value.wrapping)
            optional(&json, "lower", value.lower)
            optional(&json, "upper", value.upper)
            optional(&json, "step", value.step)
        case .tag:
            json.strings("tags", value.tags)
            if !value.tagSet.isEmpty { json.string("tagSet", value.tagSet) }
            optional(&json, "max", value.max)
        case .flags:
            json.strings("tags", value.tags)
        case .text, .bytes:
            optional(&json, "max", value.max)
        case .list:
            optional(&json, "max", value.max)
            if value.depth > 0 { json.number("depth", Int64(value.depth)) }
            json.key("element")
            field(&json, value.element!, member: false, value: true)
        case .dice:
            optional(&json, "max", value.max)
            optional(&json, "lower", value.lower)
            optional(&json, "upper", value.upper)
        case .point, .vector, .box:
            json.string("unit", value.unit)
            json.strings("axes", value.axes)
        case .poly:
            json.string("unit", value.unit)
            json.strings("axes", value.axes)
            optional(&json, "max", value.max)
        case .schema:
            json.string("name", value.schema)
        case .union:
            json.beginArray("alternatives")
            for alternative in value.alternatives { field(&json, alternative, member: false) }
            json.endArray()
        case .boolean, .nothing:
            break
        }
        json.endObject()
    }

    private static func optional(_ json: inout GesSchemaLayoutJson.Writer, _ key: String, _ value: String?) {
        if let value { json.string(key, value) } else { json.null(key) }
    }

    private static func optional(_ json: inout GesSchemaLayoutJson.Writer, _ key: String, _ value: Int64?) {
        if let value { json.number(key, value) } else { json.null(key) }
    }

    static func kind(_ kind: GesModelKind) -> String {
        switch kind {
        case .boolean: "boolean"
        case .number: "number"
        case .tag: "tag"
        case .flags: "flags"
        case .text: "text"
        case .bytes: "bytes"
        case .list: "list"
        case .dice: "dice"
        case .point: "point"
        case .vector: "vector"
        case .box: "box"
        case .poly: "poly"
        case .schema: "schema"
        case .union: "union"
        case .nothing: "nothing"
        }
    }
}
