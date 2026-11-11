// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

// The language-neutral generation layout: every type reached from the roots, bound once with the given parameters. Emitters
// read only this model, and its JSON form is compared between the ports.
struct GesSchemaLayout {
    static let version = 1
    var roots: [String]
    var parameters: [GesLayoutParameter]
    var types: [GesLayoutType]
    // The sources the schemas come from, under their file names, which code that converts GES values embeds. Not part of the JSON.
    var sources: [GesSchemaSource]
}

struct GesLayoutParameter {
    var name: String
    var type: String
    var unit: String
    // The exact decimal in base units, as the binding hash spells it.
    var value: String
    // The value as a literal that binds to the same hash, which generated code passes when it binds its schemas.
    var literal: String
}

struct GesLayoutType {
    var name = ""
    var kind = GesSchemaKind.schema
    var hash: UInt64 = 0
    var bindingHash: UInt64 = 0
    var bindingHashDepends: [String] = []
    var maxByteCount = 0
    var maxByteCountDepends: [String] = []
    var fields: [GesLayoutField] = []
    var tags: [String] = []
    var members: [String] = []
    var memberWidth = 0
}

struct GesLayoutField {
    var name = ""
    var path = ""
    var optional = false
    var key = false
    var unlabeled = false
    var encoding = GesLayoutEncoding()
}

enum GesLayoutKind {
    case boolean
    case integer
    case quantized
    case float
    case tag
    case text
    case bytes
    case list
    case point
    case vector
    case schema
    case union
    case nothing
    case flags
}

// A class, because a list's element and a union's alternatives nest encodings.
final class GesLayoutEncoding {
    var kind = GesLayoutKind.boolean

    // Numbers: what a value is, Number, Quantity or Percentage, and its unit.
    var type = ""
    var unit = ""
    var range = false
    var wrapping = false
    var lower: Int64 = 0
    var maxIndex: UInt64 = 0
    var width = 0
    var numerator: Int64 = 0
    var tenPower: Int64 = 0
    var lowerValue = 0.0
    var upperValue = 0.0
    var deltaRange: UInt64 = 0
    var deltaWidth = 0
    var depends: [String] = []

    // Tag sets, Texts and lists. A Tag of a named tag set carries its name.
    var tags: [String] = []
    var tagSet = ""
    var tagName = false
    var maxCount: Int64 = 0
    var dice = false
    var keyed = false
    var positionWidth = 0
    var element: GesLayoutField?

    // Point and Vector axes, union alternatives and schema references.
    var axes: [GesLayoutField] = []
    var alternatives: [GesLayoutField] = []
    var schema = ""
}

// Builds the layout from bound schemas. Each reached schema is bound on its own, and a nested schema appears in its parent
// as a reference, since parameters belong to files and a schema binds the same wherever it is reached.
enum GesSchemaLayoutBuilder {
    static func build(_ set: GesSchemaSet, roots: [String], parameters: GesSchemaParameters) throws -> GesSchemaLayout {
        guard !roots.isEmpty else { throw GesGeneratorError(GesGeneratorError.usage, "Specify at least one root.") }
        var reached: [String: GesSchema] = [:]
        var rootNames: [String] = []
        var parameterInfos: [String: GesSchemaParameterInfo] = [:]
        for root in roots where !rootNames.contains(root) {
            rootNames.append(root)
            let schema = try set.schema(root)
            reach(schema, &reached)
            for parameter in schema.parameters { parameterInfos[parameter.name] = parameter }
        }
        var layoutParameters: [GesLayoutParameter] = []
        for name in parameterInfos.keys.sorted(by: ordinal) {
            let parameter = parameterInfos[name]!
            let value = parameters.value(name)?.number.canonical ?? ""
            layoutParameters.append(
                GesLayoutParameter(name: name, type: parameter.kind.rawValue, unit: unitName(parameter.kind, parameter.unit), value: value, literal: literal(parameter.kind, parameter.unit, value))
            )
        }
        var types: [GesLayoutType] = []
        for name in reached.keys.sorted(by: ordinal) { types.append(try type(reached[name]!, parameters)) }
        let sources = set.sources.map { source in
            let scalars = source.name.unicodeScalars
            let start = scalars.lastIndex { $0 == "/" || $0 == "\\" }.map { scalars.index(after: $0) } ?? scalars.startIndex
            return GesSchemaSource(name: String(scalars[start...]), text: source.text)
        }
        return GesSchemaLayout(roots: rootNames, parameters: layoutParameters, types: types, sources: sources)
    }

    // Names order by their UTF-16 code units, as the C# port's ordinal comparison does.
    static func ordinal(_ left: String, _ right: String) -> Bool { left.utf16.lexicographicallyPrecedes(right.utf16) }

    private static func reach(_ schema: GesSchema, _ reached: inout [String: GesSchema]) {
        if reached[schema.name] != nil { return }
        reached[schema.name] = schema
        for member in schema.members { reach(member, &reached) }
        for field in schema.fields {
            for type in field.alternatives { reach(type, &reached) }
        }
    }

    private static func reach(_ type: GesSchemaType, _ reached: inout [String: GesSchema]) {
        if let schema = type.schema { reach(schema, &reached) }
        for element in type.element { reach(element, &reached) }
    }

    private static func type(_ schema: GesSchema, _ parameters: GesSchemaParameters) throws -> GesLayoutType {
        let bound = try schema.bind(parameters: parameters)
        var type = GesLayoutType()
        type.name = schema.name
        type.kind = schema.kind
        type.hash = bound.hash
        type.bindingHash = bound.bindingHash
        type.maxByteCount = bound.maxByteCount
        type.bindingHashDepends = schema.parameters.map(\.name)
        var depends = Set<String>()
        if schema.kind == .messages {
            type.members = schema.members.map(\.name)
            type.memberWidth = bound.memberWidth
            for member in bound.members {
                for field in fields(member.schema, member) { collect(field, &depends) }
            }
        } else {
            type.fields = fields(schema, bound)
            for field in type.fields { collect(field, &depends) }
            type.tags = schema.tags
        }
        type.maxByteCountDepends = depends.sorted(by: ordinal)
        return type
    }

    private static func fields(_ schema: GesSchema, _ bound: GesBoundSchema) -> [GesLayoutField] {
        var fields: [GesLayoutField] = []
        var index = 0
        for info in schema.fields {
            var field = field(bound, &index, info.name, info.alternatives)
            field.unlabeled = info.isUnlabeled
            fields.append(field)
        }
        return fields
    }

    // The parameters that a width of a binding depends on: those its fields and nested schemas name in their bounds.
    private static func collect(_ field: GesLayoutField, _ depends: inout Set<String>) {
        let encoding = field.encoding
        depends.formUnion(encoding.depends)
        if let element = encoding.element { collect(element, &depends) }
        for axis in encoding.axes { collect(axis, &depends) }
        for alternative in encoding.alternatives { collect(alternative, &depends) }
    }

    // One field from its description and its nodes, which start at `index`; `index` moves past them.
    private static func field(_ bound: GesBoundSchema, _ index: inout Int, _ name: String, _ alternatives: [GesSchemaType]) -> GesLayoutField {
        let node = bound.nodes[index]
        var field = GesLayoutField(name: name, path: bound.infos[index].path, optional: node.optional, key: node.key)
        if alternatives.count == 1 {
            field.encoding = encoding(bound, &index, alternatives[0])
            return field
        }
        let union = GesLayoutEncoding()
        union.kind = .union
        union.width = node.width
        index += 1
        for alternative in alternatives {
            let path = bound.infos[index].path
            let label = String(path[path.index(after: path.lastIndex(of: ":")!)...])
            let encoding: GesLayoutEncoding
            if alternative.kind == .nothing {
                index += 1
                encoding = GesLayoutEncoding()
                encoding.kind = .nothing
            } else {
                encoding = self.encoding(bound, &index, alternative)
            }
            union.alternatives.append(GesLayoutField(name: label, path: path, encoding: encoding))
        }
        field.encoding = union
        return field
    }

    private static func encoding(_ bound: GesBoundSchema, _ index: inout Int, _ type: GesSchemaType) -> GesLayoutEncoding {
        let node = bound.nodes[index]
        let encoding = GesLayoutEncoding()
        switch type.kind {
        case .boolean:
            encoding.kind = .boolean
        case .number, .percentage, .quantity:
            number(node, type.kind, type.unit, type.lower, type.upper, encoding)
        case .tag where node.kind == .tag:
            encoding.kind = .tag
            encoding.tags = bound.infos[index].tags
            encoding.tagSet = bound.infos[index].tagSet
            encoding.width = node.width
        case .flags:
            encoding.kind = .flags
            encoding.tags = bound.infos[index].tags
            encoding.width = node.width
        case .bytes:
            encoding.kind = .bytes
            encoding.maxCount = node.maxCount
            encoding.width = node.width
        case .tag, .text:
            encoding.kind = .text
            encoding.tagName = node.isTagText
            encoding.maxCount = node.maxCount
            encoding.width = node.width
        case .list:
            list(node, encoding)
            index += 1
            encoding.element = field(bound, &index, "[]", type.element)
            return encoding
        case .dice:
            list(node, encoding)
            encoding.dice = true
            index += 1
            let roll = GesLayoutEncoding()
            number(bound.nodes[index], .number, .none, type.lower, type.upper, roll)
            encoding.element = GesLayoutField(name: "[]", path: bound.infos[index].path, encoding: roll)
            index += 1
            return encoding
        case .point, .vector:
            encoding.kind = type.kind == .point ? .point : .vector
            encoding.unit = unitName(type.kind, type.unit)
            index += 1
            var axis = 0
            while axis < 3 && index < node.next {
                let bounds = type.axes.isEmpty ? nil : type.axes[axis]
                let axisEncoding = GesLayoutEncoding()
                number(bound.nodes[index], .number, .none, bounds?.lower, bounds?.upper, axisEncoding)
                encoding.axes.append(GesLayoutField(name: bound.infos[index].name, path: bound.infos[index].path, encoding: axisEncoding))
                index += 1
                axis += 1
            }
            return encoding
        case .reference:
            encoding.kind = .schema
            encoding.schema = bound.infos[index].schemaName
            encoding.depends = (type.schema?.parameters.map(\.name) ?? []).sorted(by: ordinal)
            index = node.next
            return encoding
        default:
            preconditionFailure("A \(type.kind.rawValue) has no encoding.")
        }
        index += 1
        return encoding
    }

    private static func list(_ node: GesSchemaNode, _ encoding: GesLayoutEncoding) {
        encoding.kind = .list
        encoding.maxCount = node.maxCount
        encoding.width = node.width
        encoding.keyed = node.keyedElements
        encoding.positionWidth = node.keyedElements ? node.positionWidth : 0
    }

    private static func number(_ node: GesSchemaNode, _ kind: GesSchemaTypeKind, _ unit: GesQuantityUnit, _ lower: GesSchemaBound?, _ upper: GesSchemaBound?, _ encoding: GesLayoutEncoding) {
        switch node.kind {
        case .integer: encoding.kind = .integer
        case .quantized: encoding.kind = .quantized
        default: encoding.kind = .float
        }
        encoding.type = kind.rawValue
        encoding.unit = unitName(kind, unit)
        encoding.range = node.hasRange
        encoding.wrapping = node.wrapping
        encoding.width = node.width
        encoding.deltaRange = node.deltaRange
        encoding.deltaWidth = node.deltaWidth
        if node.kind == .integer || node.kind == .quantized {
            encoding.lower = node.lower
            encoding.maxIndex = node.maxIndex
        }
        if node.kind == .quantized {
            encoding.numerator = node.p
            encoding.tenPower = Int64(node.sBinary)
        }
        if node.kind == .quantized || (node.kind == .float && node.hasRange) {
            encoding.lowerValue = node.lowerValue
            encoding.upperValue = node.upperValue
        }
        var depends = Set<String>()
        if let lower, lower.isParameter { depends.insert(lower.parameter) }
        if let upper, upper.isParameter { depends.insert(upper.parameter) }
        encoding.depends = depends.sorted(by: ordinal)
    }

    // A Percentage holds a fraction, so its literal moves the exponent by two; a Quantity takes the symbol of its unit.
    private static func literal(_ kind: GesSchemaTypeKind, _ unit: GesQuantityUnit, _ value: String) -> String {
        if value.isEmpty { return "" }
        if kind == .percentage {
            let exponent = value.firstIndex(of: "e")!
            return String(value[...exponent]) + String(Int64(value[value.index(after: exponent)...])! + 2) + "%"
        }
        return kind == .quantity && unit != .none ? value + unit.suffix : value
    }

    static func unitName(_ kind: GesSchemaTypeKind, _ unit: GesQuantityUnit) -> String {
        (kind == .quantity || kind == .point || kind == .vector) && unit != .none ? unit.rawValue : "none"
    }
}
