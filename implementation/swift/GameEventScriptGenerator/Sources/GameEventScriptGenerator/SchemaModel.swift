// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// The language-neutral model layout: every schema reached from the model roots, described without binding, since a model
// type is a plain value that is never encoded. Model emitters read only this, and its JSON form is compared between ports.
struct GesModelLayout {
    static let version = 1

    var roots: [String]
    var types: [GesModelType]

    func type(_ name: String) -> GesModelType { types.first { $0.name == name }! }
}

struct GesModelType {
    var name = ""
    var hash: UInt64 = 0
    var fields: [GesModelField] = []
}

final class GesModelField {
    var name = ""
    var path = ""
    var optional = false
    var key = false
    var value = GesModelValue()
}

enum GesModelKind {
    case boolean, number, tag, text, list, dice, point, vector, schema, union, nothing
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
    // A closed tag set, empty for any tag; the documented maximum of a Tag, Text, List or Dice.
    var tags: [String] = []
    var max: Int64?
    var element: GesModelField?
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
            rootNames.append(root)
            reach(schema, &reached)
        }
        var types: [GesModelType] = []
        for name in reached.keys.sorted(by: GesSchemaLayoutBuilder.ordinal) {
            let schema = reached[name]!
            var type = GesModelType(name: schema.name, hash: schema.hash)
            for info in schema.fields {
                let field = try self.field(info.name, info.name, info.alternatives)
                field.optional = info.isOptional
                field.key = info.isKey
                type.fields.append(field)
            }
            types.append(type)
        }
        return GesModelLayout(roots: rootNames, types: types)
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
            let label = alternative.kind == .reference ? alternative.schema!.name : alternative.kind.rawValue
            let item = GesModelField()
            item.name = label
            item.path = path + ":" + label
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
            value.max = type.max
        case .text:
            value.kind = .text
            value.max = type.max
        case .list where !type.element.isEmpty:
            value.kind = .list
            value.max = type.max
            value.element = try field("[]", path + "[]", type.element)
        case .dice:
            value.kind = .dice
            value.max = type.max
            value.lower = bound(type.lower)
            value.upper = bound(type.upper)
        case .point, .vector:
            value.kind = type.kind == .point ? .point : .vector
            value.unit = GesSchemaLayoutBuilder.unitName(type.kind, type.unit)
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
            json.endObject()
        }
        json.endArray()
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
            optional(&json, "max", value.max)
        case .text:
            optional(&json, "max", value.max)
        case .list:
            optional(&json, "max", value.max)
            json.key("element")
            field(&json, value.element!, member: false, value: true)
        case .dice:
            optional(&json, "max", value.max)
            optional(&json, "lower", value.lower)
            optional(&json, "upper", value.upper)
        case .point, .vector:
            json.string("unit", value.unit)
            json.strings("axes", value.axes)
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
        case .text: "text"
        case .list: "list"
        case .dice: "dice"
        case .point: "point"
        case .vector: "vector"
        case .schema: "schema"
        case .union: "union"
        case .nothing: "nothing"
        }
    }
}
