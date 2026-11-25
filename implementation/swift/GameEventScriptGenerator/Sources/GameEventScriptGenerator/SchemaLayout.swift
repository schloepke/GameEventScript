// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

// The language-neutral generation layout: every type reached from the roots, bound once with the given parameters. Emitters
// read only this model, and its JSON form is compared between the ports.
struct GesSchemaLayout {
    static let version = 1
    // The roots in order as generated type names: the given ones, each network followed by the roots of its routes that are
    // no root yet.
    var roots: [String]
    // The roots as a schema set looks them up, in the order of roots: the declared name, written `:Name` or `Name()` where
    // the set declares it in both namespaces. Generated code binds them, and the JSON, headers and manifests name them.
    var rootNames: [String]
    var parameters: [GesLayoutParameter]
    var types: [GesLayoutType]
    // The networks among the roots, in root order.
    var networks: [GesLayoutNetwork] = []
    // The sources the schemas come from, under their file names, which code that converts GES values embeds. Not part of the JSON.
    var sources: [GesSchemaSource]
}

// A network root: its hash bound with the layout's parameters and its routes in declared order.
struct GesLayoutNetwork {
    var name: String
    var hash: UInt64
    var routes: [GesLayoutRoute]
}

// One route: its side `client` or `server`, its verb `sends` or `publishes`, its root, its class `reliable`, `latest` or
// `bulk` and its rate in microseconds, 0 without one.
struct GesLayoutRoute {
    var index: Int
    var side: String
    var verb: String
    // The declared name of the root, which names the route's constants.
    var root: String
    // The generated type of the root, the root's name with the message suffix for a message.
    var type: String
    // The root as a schema set looks it up, see GesSchemaLayout.rootNames.
    var rootName: String
    // Whether the root is a message, which the source names without `:`.
    var isMessage: Bool
    var deliveryClass: String
    var rateMicroseconds: Int64

    // The root as the route spells it in source: a message by its name, a schema or set with `:`.
    var written: String { isMessage ? root : ":" + root }

    // The name of the route's constants: side, verb and root, which identify a route within its network.
    var name: String { (side == "client" ? "Client" : "Server") + (verb == "sends" ? "Sends" : "Publishes") + root }
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

/// The default of a field: its kind, `number`, `boolean`, `tag`, `field` or `record`, and its value, a canonical decimal in
/// base units, a Boolean, a tag without `#` or a sibling field's name; a record lists the fields it names with their defaults.
struct GesLayoutDefault {
    var kind = GesSchemaDefaultKind.number
    var value = ""
    var boolean = false
    var entries: [GesLayoutDefaultEntry] = []

    static func of(_ field: GesSchemaFieldInfo) -> GesLayoutDefault? { field.defaultValue.map(of) }

    private static func of(_ fallback: GesSchemaDefaultInfo) -> GesLayoutDefault {
        GesLayoutDefault(
            kind: fallback.kind,
            value: fallback.number.map { "\($0.significand)e\($0.exponent)" } ?? fallback.name,
            boolean: fallback.boolean,
            entries: fallback.entries.map { GesLayoutDefaultEntry(name: $0.name, value: of($0.value)) }
        )
    }

    func write(_ json: inout GesSchemaLayoutJson.Writer) {
        json.key("default")
        json.beginValueObject()
        let kind =
            switch self.kind {
            case .number: "number"
            case .boolean: "boolean"
            case .tag: "tag"
            case .field: "field"
            case .record: "record"
            case .empty: "empty"
            }
        json.string("kind", kind)
        if self.kind == .record {
            json.beginArray("entries")
            for entry in entries {
                json.beginObject()
                json.string("name", entry.name)
                entry.value.write(&json)
                json.endObject()
            }
            json.endArray()
        } else if self.kind == .boolean {
            json.boolean("value", boolean)
        } else if self.kind != .empty {
            json.string("value", value)
        }
        json.endObject()
    }
}

/// A field a record default names, with its value.
struct GesLayoutDefaultEntry {
    var name: String
    var value: GesLayoutDefault
}

/// A `let` of a schema: its kind, unit, exact value in base units and whether it is a whole Number within Int64.
struct GesLayoutConstant {
    var name = ""
    var type = ""
    var unit = ""
    var significand: Int64 = 0
    var exponent = 0
    var integer = false
    /// The unit symbol or `%` that a literal of it ends with, for generated documentation; not part of the JSON.
    var suffix = ""

    var value: String { "\(significand)e\(exponent)" }

    static func of(_ schema: GesSchema) -> [GesLayoutConstant] {
        schema.constants.map { constant in
            GesLayoutConstant(
                name: constant.name,
                type: constant.kind.rawValue,
                unit: GesSchemaLayoutBuilder.unitName(constant.kind, constant.unit),
                significand: constant.value.significand,
                exponent: constant.value.exponent,
                integer: constant.isInteger,
                suffix: constant.kind == .percentage ? "%" : constant.kind == .quantity ? constant.unit.suffix : ""
            )
        }
    }
}

struct GesLayoutType {
    // The generated type name: the declared name, with the message suffix for a message.
    var name = ""
    // The declared name, which values, documents, diagnostics and the JSON use.
    var declared = ""
    // The name a schema set looks the type up by: the declared name, `:Name` or `Name()` when a schema or set and a message
    // share it.
    var lookup = ""
    // The name of the source that declares it, which places it in split output.
    var source = ""
    var kind = GesSchemaKind.schema
    var hash: UInt64 = 0
    var bindingHash: UInt64 = 0
    var bindingHashDepends: [String] = []
    var maxByteCount = 0
    var maxByteCountDepends: [String] = []
    var fields: [GesLayoutField] = []
    var constants: [GesLayoutConstant] = []
    /// The fields written `computed by`, which are never encoded, with their checked expressions.
    var computed: [GesSchemaComputedFieldInfo] = []
    var tags: [String] = []
    // A message set's members as generated type names and as declared names, which name its cases.
    var members: [String] = []
    var memberNames: [String] = []
    var memberWidth = 0

    // A packed schema's word: its width of 8, 16, 32 or 64 bits and the bits its fields take; 0 for other types.
    var packedWidth = 0
    var packedBits = 0
}

struct GesLayoutField {
    var name = ""
    var path = ""
    var optional = false
    var key = false
    var unlabeled = false
    var defaultValue: GesLayoutDefault?
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
    case box
    case schema
    case union
    case nothing
    case flags
    case disc
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

    // The bits a field of fixed width takes on the wire: a Boolean has no layout width but takes one bit.
    var bits: Int { kind == .boolean ? 1 : width }
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
    var poly = false
    var keyed = false
    var positionWidth = 0
    var element: GesLayoutField?

    // A recursive List: how many records of its schema nest at most, and 0 for every other List.
    var depth = 0

    // Point and Vector axes, the corners lower and upper of a Box or the centre and the radius of a Disc, union alternatives
    // and schema references. A Disc whose block leaves out the radius has the centre alone.
    var axes: [GesLayoutField] = []
    var corners: [GesLayoutField] = []
    var alternatives: [GesLayoutField] = []
    var schema = ""
}

// Builds the layout from bound schemas. Each reached schema is bound on its own, and a nested schema appears in its parent
// as a reference, since parameters belong to files and a schema binds the same wherever it is reached.
enum GesSchemaLayoutBuilder {
    // Messages have their own namespace, so a message is keyed `Name()`; `(` sorts below every name character, which keeps
    // the order of names that differ.
    static func build(_ set: GesSchemaSet, roots: [String], parameters: GesSchemaParameters, messageSuffix: String = "") throws -> GesSchemaLayout {
        guard !roots.isEmpty else { throw GesGeneratorError(GesGeneratorError.usage, "Specify at least one root.") }
        var reached: [String: GesSchema] = [:]
        var rootKeys: [String] = []
        var rootTypes: [String] = []
        var rootNames: [String] = []
        var parameterInfos: [String: GesSchemaParameterInfo] = [:]
        var networks: [GesNetwork] = []
        for root in roots {
            if let network = try findNetwork(set, root) {
                if rootKeys.contains(":" + network.name) { continue }
                rootKeys.append(":" + network.name)
                rootTypes.append(network.name)
                rootNames.append(set.namesBoth(network.name) ? ":" + network.name : network.name)
                // A network makes the roots of its routes roots of the generation.
                networks.append(network)
                for route in network.routes {
                    addRoot(set, route.root, &rootKeys, &rootTypes, &rootNames, messageSuffix)
                    self.root(route.root, &reached, &parameterInfos)
                }
                continue
            }
            let schema = try set.schema(root)
            addRoot(set, schema, &rootKeys, &rootTypes, &rootNames, messageSuffix)
            self.root(schema, &reached, &parameterInfos)
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
        for name in reached.keys.sorted(by: ordinal) { types.append(try type(set, reached[name]!, parameters, messageSuffix)) }
        let layoutNetworks = try networks.map { try network(set, $0, parameters, messageSuffix) }
        let sources = set.sources.map { source in
            let scalars = source.name.unicodeScalars
            let start = scalars.lastIndex { $0 == "/" || $0 == "\\" }.map { scalars.index(after: $0) } ?? scalars.startIndex
            return GesSchemaSource(name: String(scalars[start...]), text: source.text)
        }
        return GesSchemaLayout(roots: rootTypes, rootNames: rootNames, parameters: layoutParameters, types: types, networks: layoutNetworks, sources: sources)
    }

    // A network named plainly or `:Name`; a plain name that a message shares is ambiguous.
    private static func findNetwork(_ set: GesSchemaSet, _ name: String) throws -> GesNetwork? {
        let qualified = name.hasPrefix(":")
        let plain = qualified ? String(name.dropFirst()) : name
        guard let network = set.networks.first(where: { $0.name == plain }) else { return nil }
        if !qualified && set.namesBoth(plain) { _ = try set.schema(plain) }
        return network
    }

    private static func addRoot(_ set: GesSchemaSet, _ schema: GesSchema, _ keys: inout [String], _ types: inout [String], _ names: inout [String], _ messageSuffix: String) {
        let key = key(schema)
        if keys.contains(key) { return }
        keys.append(key)
        types.append(typeName(schema, messageSuffix))
        names.append(set.rootName(of: schema))
    }

    private static func key(_ schema: GesSchema) -> String { schema.kind == .message ? schema.name + "()" : schema.name }

    // The generated type name: a message's name ends with the suffix.
    static func typeName(_ schema: GesSchema, _ messageSuffix: String) -> String { schema.kind == .message ? schema.name + messageSuffix : schema.name }

    private static func root(_ schema: GesSchema, _ reached: inout [String: GesSchema], _ parameterInfos: inout [String: GesSchemaParameterInfo]) {
        reach(schema, &reached)
        for parameter in schema.parameters { parameterInfos[parameter.name] = parameter }
    }

    private static func network(_ set: GesSchemaSet, _ network: GesNetwork, _ parameters: GesSchemaParameters, _ messageSuffix: String) throws -> GesLayoutNetwork {
        let routes = network.routes.map { route in
            GesLayoutRoute(
                index: route.index,
                side: route.side == .client ? "client" : "server",
                verb: route.verb == .sends ? "sends" : "publishes",
                root: route.root.name,
                type: typeName(route.root, messageSuffix),
                rootName: set.rootName(of: route.root),
                isMessage: route.root.kind == .message,
                deliveryClass: GesBoundNetwork.className(route.deliveryClass),
                rateMicroseconds: route.rateMicroseconds
            )
        }
        return GesLayoutNetwork(name: network.name, hash: try network.bind(parameters: parameters).hash, routes: routes)
    }

    // Names order by their UTF-16 code units, as the C# port's ordinal comparison does.
    static func ordinal(_ left: String, _ right: String) -> Bool { left.utf16.lexicographicallyPrecedes(right.utf16) }

    private static func reach(_ schema: GesSchema, _ reached: inout [String: GesSchema]) {
        let key = key(schema)
        if reached[key] != nil { return }
        reached[key] = schema
        for member in schema.members { reach(member, &reached) }
        for field in schema.fields {
            for type in field.alternatives { reach(type, &reached) }
        }
    }

    private static func reach(_ type: GesSchemaType, _ reached: inout [String: GesSchema]) {
        if let schema = type.schema { reach(schema, &reached) }
        for element in type.element { reach(element, &reached) }
    }

    private static func type(_ set: GesSchemaSet, _ schema: GesSchema, _ parameters: GesSchemaParameters, _ messageSuffix: String) throws -> GesLayoutType {
        let bound = try schema.bind(parameters: parameters)
        var type = GesLayoutType()
        type.name = typeName(schema, messageSuffix)
        type.declared = schema.name
        type.lookup = set.rootName(of: schema)
        type.source = schema.kind == .message ? set.messageSourceName(of: schema.name) : set.sourceName(of: schema.name)
        type.kind = schema.kind
        type.hash = bound.hash
        type.bindingHash = bound.bindingHash
        type.maxByteCount = bound.maxByteCount
        type.packedWidth = schema.packedWidth
        type.packedBits = schema.packedBits
        type.bindingHashDepends = schema.parameters.map(\.name)
        var depends = Set<String>()
        if schema.kind == .messages {
            type.members = schema.members.map { typeName($0, messageSuffix) }
            type.memberNames = schema.members.map(\.name)
            type.memberWidth = bound.memberWidth
            type.fields = fields(schema, bound)
            for field in type.fields { collect(field, &depends) }
            for member in bound.members {
                for field in fields(member.schema, member) { collect(field, &depends) }
            }
        } else {
            type.fields = fields(schema, bound)
            type.constants = GesLayoutConstant.of(schema)
            type.computed = schema.computedFields
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
            field.defaultValue = GesLayoutDefault.of(info)
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
            let label = alternative.label.isEmpty ? String(path[path.index(after: path.lastIndex(of: ":")!)...]) : alternative.label
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
        let start = index
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
            encoding.depth = type.depth
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
            axes(bound, &index, start, type, encoding)
            return encoding
        case .box:
            // Two corners, each a Point of the block's axes.
            encoding.kind = .box
            encoding.unit = unitName(type.kind, type.unit)
            index += 1
            encoding.corners.append(corner(bound, &index, type))
            encoding.corners.append(corner(bound, &index, type))
            return encoding
        case .disc:
            // The centre, a Point of the block's axes x and y, then the radius, unless the block leaves it out.
            encoding.kind = .disc
            encoding.unit = unitName(type.kind, type.unit)
            let disc = index
            index += 1
            encoding.corners.append(corner(bound, &index, type))
            if index < bound.nodes[disc].next {
                let radius = GesLayoutEncoding()
                let bounds = type.axes.last { $0.name == "radius" }
                number(bound.nodes[index], .number, .none, bounds?.lower, bounds?.upper, radius)
                encoding.corners.append(GesLayoutField(name: bound.infos[index].name, path: bound.infos[index].path, encoding: radius))
                index += 1
            }
            return encoding
        case .poly:
            // A List of Points with the axes x and y.
            list(node, encoding)
            encoding.poly = true
            encoding.unit = unitName(type.kind, type.unit)
            index += 1
            encoding.element = corner(bound, &index, type)
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

    // The axes of a Point, a Box corner or a Poly point, whose group node ends at `group`'s next.
    private static func axes(_ bound: GesBoundSchema, _ index: inout Int, _ group: Int, _ type: GesSchemaType, _ encoding: GesLayoutEncoding) {
        var axis = 0
        while axis < 3 && index < bound.nodes[group].next {
            let bounds = type.axes.isEmpty ? nil : type.axes[axis]
            let axisEncoding = GesLayoutEncoding()
            number(bound.nodes[index], .number, .none, bounds?.lower, bounds?.upper, axisEncoding)
            encoding.axes.append(GesLayoutField(name: bound.infos[index].name, path: bound.infos[index].path, encoding: axisEncoding))
            index += 1
            axis += 1
        }
    }

    private static func corner(_ bound: GesBoundSchema, _ index: inout Int, _ type: GesSchemaType) -> GesLayoutField {
        let group = index
        let corner = GesLayoutEncoding()
        corner.kind = .point
        corner.unit = unitName(type.kind, type.unit)
        index += 1
        axes(bound, &index, group, type, corner)
        return GesLayoutField(name: bound.infos[group].name, path: bound.infos[group].path, encoding: corner)
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
        (kind == .quantity || kind == .point || kind == .vector || kind == .box || kind == .poly || kind == .disc) && unit != .none ? unit.rawValue : "none"
    }
}
