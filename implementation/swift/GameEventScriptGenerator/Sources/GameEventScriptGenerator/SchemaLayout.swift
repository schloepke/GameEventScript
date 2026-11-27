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
    // The handle types the roots reach, composite roots and network owners included, ordered by name.
    var handles: [GesLayoutHandle] = []
    // The networks among the roots, in root order.
    var networks: [GesLayoutNetwork] = []
    // The sources the schemas come from, under their file names, which code that converts GES values embeds. Not part of the JSON.
    var sources: [GesSchemaSource]

    func handle(_ name: String) -> GesLayoutHandle {
        guard let handle = handles.first(where: { $0.name == name }) else { preconditionFailure("Handle '@\(name)' is not in the layout.") }
        return handle
    }
}

// A network root: its hash bound with the layout's parameters and its routes in declared order.
struct GesLayoutNetwork {
    var name: String
    var hash: UInt64
    var routes: [GesLayoutRoute]
    // The `owns` routes in ascending scalar order of the handle type's name.
    var owners: [GesLayoutOwner] = []
}

// One `owns` route: the side, `client` or `server`, the handle type's name without `@`, the projection the other side
// reads, empty without one, and whether the owner sends state along.
struct GesLayoutOwner {
    var side: String
    var handle: String
    var projection: String
    var sendingState: Bool
}

// A handle type: its name without `@`, its handle hash, its state schema's type name and the encodings of its identity, an
// `integer` or `text` id and its version, or for a composite the root's name and the part. A revision has its integer encoding.
// A class, as the plan values of handles share it.
final class GesLayoutHandle {
    var name = ""
    var hash: UInt64 = 0
    var state = ""
    var id: GesLayoutEncoding?
    var version = GesHandleVersionKind.none
    var revision: GesLayoutEncoding?
    var root = ""
    var part: GesLayoutEncoding?

    // The root of a part-less composite, which encodes like a plain handle with its root's id and version.
    var partlessRoot = ""

    // The form that the handle type `<Name>Handle` checks, shared with models.
    var model = GesModelHandle()

    var isComposite: Bool { !root.isEmpty }

    // A composite's id is its root's.
    var versioned: Bool { version != .none }

    static func versionName(_ kind: GesHandleVersionKind) -> String {
        switch kind {
        case .revision: "revision"
        case .sha1: "sha1"
        case .sha256: "sha256"
        case .contentHash: "contentHash"
        default: "none"
        }
    }
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
    // Whether the root is a schema, which the source names with `:`; a message or message set goes by its bare name.
    var isSchema: Bool
    var deliveryClass: String
    var rateMicroseconds: Int64

    // The root as the route spells it in source: a schema with `:`, a message or message set by its name.
    var written: String { isSchema ? ":" + root : root }

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
    case handle
    case handleMap
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
    // A List of exactly maxCount elements, whose count the wire leaves out.
    var fixed = false
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

    // A handle or a handle Map's key: the handle type's name without `@`, and whether a handle carries its version.
    var handle = ""
    var versioned = false
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
        var handles: [String: GesHandleType] = [:]
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
                    self.root(route.root, &reached, &handles, &parameterInfos)
                }
                for owner in network.owners { reach(owner.handle, &reached, &handles) }
                continue
            }
            let schema = try set.schema(root)
            addRoot(set, schema, &rootKeys, &rootTypes, &rootNames, messageSuffix)
            self.root(schema, &reached, &handles, &parameterInfos)
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
        var layoutHandles: [GesLayoutHandle] = []
        for name in handles.keys.sorted(by: ordinal) { layoutHandles.append(try handle(handles[name]!)) }
        let layoutNetworks = try networks.map { try network(set, $0, parameters, messageSuffix) }
        let sources = set.sources.map { source in
            let scalars = source.name.unicodeScalars
            let start = scalars.lastIndex { $0 == "/" || $0 == "\\" }.map { scalars.index(after: $0) } ?? scalars.startIndex
            return GesSchemaSource(name: String(scalars[start...]), text: source.text)
        }
        return GesSchemaLayout(roots: rootTypes, rootNames: rootNames, parameters: layoutParameters, types: types, handles: layoutHandles, networks: layoutNetworks, sources: sources)
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

    private static func root(_ schema: GesSchema, _ reached: inout [String: GesSchema], _ handles: inout [String: GesHandleType], _ parameterInfos: inout [String: GesSchemaParameterInfo]) {
        reach(schema, &reached, &handles)
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
                isSchema: route.root.kind == .schema,
                deliveryClass: GesBoundNetwork.className(route.deliveryClass),
                rateMicroseconds: route.rateMicroseconds
            )
        }
        var owners = network.owners.map { GesLayoutOwner(side: $0.side == .client ? "client" : "server", handle: $0.handle.name, projection: $0.projection?.name ?? "", sendingState: $0.sendsState) }
        owners.sort { ordinal($0.handle, $1.handle) }
        return GesLayoutNetwork(name: network.name, hash: try network.bind(parameters: parameters).hash, routes: routes, owners: owners)
    }

    // Names order by their UTF-16 code units, as the C# port's ordinal comparison does.
    static func ordinal(_ left: String, _ right: String) -> Bool { left.utf16.lexicographicallyPrecedes(right.utf16) }

    private static func reach(_ schema: GesSchema, _ reached: inout [String: GesSchema], _ handles: inout [String: GesHandleType]) {
        let key = key(schema)
        if reached[key] != nil { return }
        reached[key] = schema
        for member in schema.members { reach(member, &reached, &handles) }
        for field in schema.fields {
            for type in field.alternatives { reach(type, &reached, &handles) }
        }
    }

    private static func reach(_ type: GesSchemaType, _ reached: inout [String: GesSchema], _ handles: inout [String: GesHandleType]) {
        if let schema = type.schema { reach(schema, &reached, &handles) }
        if let handle = type.handleType { reach(handle, &reached, &handles) }
        for element in type.element { reach(element, &reached, &handles) }
    }

    // A handle type brings a composite's root. Its state stays outside the layout: the wire carries only the identity, and a
    // state, often a document, need not be encodable.
    private static func reach(_ handle: GesHandleType, _ reached: inout [String: GesSchema], _ handles: inout [String: GesHandleType]) {
        if handles[handle.name] != nil { return }
        handles[handle.name] = handle
        if let root = handle.root { reach(root, &reached, &handles) }
    }

    // The encodings of a handle type's identity, from the nodes of the type on its own: the group, then the id, the version
    // and a composite's part.
    private static func handle(_ type: GesHandleType) throws -> GesLayoutHandle {
        let nodes = try type.bindNodes().nodes
        let handle = GesLayoutHandle()
        handle.name = type.name
        handle.hash = type.hash
        handle.state = type.stateName
        handle.version = type.version
        handle.model = GesModelHandle.of(type)
        var index = 1
        if let partless = type.root, type.part == nil {
            handle.partlessRoot = partless.name
        } else if let root = type.root {
            handle.root = root.name
            index = nodes[index].next
            if type.version != .none { index += 1 }
            handle.part = key(nodes[index])
            return handle
        }
        handle.id = key(nodes[index])
        index = nodes[index].next
        if type.version == .revision {
            let revision = GesLayoutEncoding()
            number(nodes[index], .number, .none, nil, nil, revision)
            handle.revision = revision
        }
        return handle
    }

    private static func key(_ node: GesSchemaNode) -> GesLayoutEncoding {
        let encoding = GesLayoutEncoding()
        if node.kind == .text {
            encoding.kind = .text
            encoding.maxCount = node.maxCount
            encoding.width = node.width
        } else {
            number(node, .number, .none, nil, nil, encoding)
        }
        return encoding
    }

    private static func type(_ set: GesSchemaSet, _ schema: GesSchema, _ parameters: GesSchemaParameters, _ messageSuffix: String) throws -> GesLayoutType {
        let bound = try schema.bind(parameters: parameters)
        var type = GesLayoutType()
        type.name = typeName(schema, messageSuffix)
        type.declared = schema.name
        type.lookup = set.rootName(of: schema)
        type.source = schema.kind == .schema ? set.sourceName(of: schema.name) : set.messageSourceName(of: schema.name)
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
            let label = alternative.label.isEmpty ? String(path[path.index(after: path.lastIndex(of: ":")!)...].drop { $0 == "@" }) : alternative.label
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
        case .handle:
            encoding.kind = .handle
            encoding.handle = type.handleType!.name
            encoding.versioned = type.isVersioned
            index = node.next
            return encoding
        case .map where type.handleType != nil:
            // The List node, then each entry's group of the key, a handle, and the value.
            encoding.kind = .handleMap
            encoding.handle = type.handleType!.name
            encoding.maxCount = node.maxCount
            encoding.width = node.width
            index += 2
            index = bound.nodes[index].next
            encoding.element = field(bound, &index, "[]", type.element)
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
        encoding.fixed = node.fixed
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
