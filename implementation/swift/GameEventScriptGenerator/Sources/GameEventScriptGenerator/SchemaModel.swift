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
    // The handle types the types reach, ordered by name, each with its composite root.
    var handles: [GesModelHandle] = []

    func type(_ name: String) -> GesModelType { types.first { $0.name == name }! }
}

struct GesModelType {
    var name = ""
    var hash: UInt64 = 0

    // A schema, a message reached by a model set or a model set; the name a schema set looks it up by.
    var kind = GesSchemaKind.schema
    var lookup = ""

    // The tags of a message and the members of a set, in declared order.
    var tags: [String] = []
    var members: [String] = []
    var fields: [GesModelField] = []
    var constants: [GesLayoutConstant] = []
    /// The schemas it takes unchanged with `with`, whose protocols or interfaces it adopts, in declared order.
    var takes: [String] = []
    /// The fields written `computed by`, which models derive from their other fields.
    var computed: [GesSchemaComputedFieldInfo] = []

    // The wire layout of a packed schema, whose fields' encodings place them in its word; nil for other types.
    var packed: GesSchemaLayout?

    // The conversions of a projection from the schemas it narrows that the layout holds, nearest first.
    var conversions: [GesModelConversion] = []
}

// A projection's conversion from one schema it narrows: per taken field, the field names of its path within that schema.
struct GesModelConversion {
    var source = ""
    var paths: [[String]] = []
}

// A handle type: its identity's form, which a model type `<Name>Handle` checks, and the state schema, a type of the layout.
struct GesModelHandle {
    var name = ""
    var hash: UInt64 = 0
    var state = ""
    var version = GesHandleVersionKind.none

    // The id of a plain handle type, or the part and the root's name of a composite. A part-less composite has its root's
    // id and version as its own and builds like a plain handle; partlessRoot names its root.
    var id: GesHandleKey?
    var root: String?
    var part: GesHandleKey?
    var partlessRoot: String?

    // The projections of the state that the layout holds, by name.
    var projections: [String] = []

    var isComposite: Bool { root != nil }

    // The key that a composite's equality compares after its root's id: its part; the id for any other.
    var key: GesHandleKey { (isComposite ? part : id)! }

    static func of(_ handle: GesHandleType) -> GesModelHandle {
        GesModelHandle(
            name: handle.name,
            hash: handle.hash,
            state: handle.stateName,
            version: handle.version,
            id: handle.id,
            root: handle.part == nil ? nil : handle.root?.name,
            part: handle.part,
            partlessRoot: handle.part == nil ? handle.root?.name : nil
        )
    }
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

    // Whether a message argument is written without a label, so that its message carries the label `_`.
    var unlabeled = false

    // Whether the member takes the suffix Value because a schema lends the field with `with` and the field is named like that
    // schema or one that takes it, so that the interface or protocol and every type taking it name it alike.
    var renamed = false
}

enum GesModelKind {
    case boolean, number, tag, text, bytes, list, dice, point, vector, schema, union, nothing, flags, box, poly, disc
    // Free data: a Map of any values, and a List without an element type, whose elements are any values.
    case map, values
    // A handle, and a Map from handles of one type to values, whose value is its element.
    case handle, handleMap
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
    // A List of exactly max elements.
    var fixed = false
    var element: GesModelField?
    // The depth of a recursive List, 0 for every other List.
    var depth = 0
    // The axes a Point or Vector declares, empty without an axis block; union alternatives; a schema reference.
    var axes: [String] = []
    var alternatives: [GesModelField] = []
    var schema = ""

    // Whether the schema is packed, so that its type is a value type over one word.
    var packed = false

    // The handle type of a handle or a handle Map's key, and whether a handle carries its version.
    var handle = ""
    var versioned = false
}

enum GesModelLayoutBuilder {
    static func build(_ set: GesSchemaSet, roots: [String]) throws -> GesModelLayout {
        guard !roots.isEmpty else { throw GesGeneratorError(GesGeneratorError.usage, "Specify at least one root.") }
        var reached: [String: GesSchema] = [:]
        var handles: [String: GesHandleType] = [:]
        var rootNames: [String] = []
        for root in roots {
            let schema = try set.schema(root)
            if rootNames.contains(schema.name) { continue }
            if schema.kind == .message {
                throw GesGeneratorError(GesGeneratorError.usage, "The model root '\(root)' is a message; a model root is a schema or message set.")
            }
            if schema.isAbstract {
                throw GesGeneratorError(GesGeneratorError.usage, "The model root '\(root)' is abstract; only its protocol is generated, where a model takes it.")
            }
            rootNames.append(schema.name)
            try reach(schema, &reached, &handles)
        }
        // A model root that is the state of a handle type with a Text id of its own reaches that type, whose store holds it.
        // A handle's state is a schema, which a message of the same name does not stand for.
        for handle in set.handleTypes
        where handle.root == nil && handle.storeOf == nil && handle.id?.isText == true && rootNames.contains(handle.stateName)
            && reached[handle.stateName]?.kind == .schema
        {
            reach(handle, &handles)
        }
        var types: [GesModelType] = []
        for name in reached.keys.sorted(by: GesSchemaLayoutBuilder.ordinal) {
            let schema = reached[name]!
            var type = GesModelType(name: schema.name, hash: schema.hash, kind: schema.kind, lookup: set.rootName(of: schema), constants: GesLayoutConstant.of(schema))
            type.computed = schema.computedFields
            if schema.packedWidth > 0 { type.packed = try GesSchemaLayoutBuilder.build(set, roots: [set.rootName(of: schema)], parameters: GesSchemaParameters()) }
            for info in schema.fields {
                let field = try self.field(info.name, info.name, info.alternatives)
                field.optional = info.isOptional
                field.key = info.isKey
                field.unlabeled = schema.kind != .schema && info.isUnlabeled
                field.defaultValue = GesLayoutDefault.of(info)
                type.fields.append(field)
            }
            type.takes = schema.takes.filter(\.isUnchanged).map(\.schema.name)
            if schema.kind == .message { type.tags.append(contentsOf: schema.tags) }
            type.members = schema.members.map(\.name)
            // A projection converts from its source and every schema below it that the layout holds.
            for (source, paths) in schema.projectionSources where reached[source.name] === source {
                type.conversions.append(GesModelConversion(source: source.name, paths: paths))
            }
            types.append(type)
        }
        var layout = GesModelLayout(roots: rootNames, types: types)
        for name in handles.keys.sorted(by: GesSchemaLayoutBuilder.ordinal) {
            let handle = handles[name]!
            var model = GesModelHandle.of(handle)
            model.projections = handle.projections.filter { reached[$0.name] === $0 }.map(\.name)
            layout.handles.append(model)
        }
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
        renameLent(layout)
        return layout
    }

    // A field that a schema lends with `with` takes one member name in its interface or protocol and in every type that takes
    // it: like a field named after its own type, it is renamed where it is named like the lending schema or one taking it.
    private static func renameLent(_ layout: GesModelLayout) {
        for protocolType in layout.protocols {
            var owners: Set<String> = [protocolType.name]
            var grew = true
            while grew {
                grew = false
                for other in layout.protocols where !owners.contains(other.name) && other.takes.contains(where: owners.contains) {
                    grew = owners.insert(other.name).inserted || grew
                }
                for type in layout.types where !owners.contains(type.name) && type.takes.contains(where: owners.contains) {
                    grew = owners.insert(type.name).inserted || grew
                }
            }
            for field in protocolType.fields where owners.contains(GesSchemaPlanner.pascal(field.name)) {
                field.renamed = true
                for type in layout.types where owners.contains(type.name) {
                    for taken in type.fields where taken.name == field.name { taken.renamed = true }
                }
            }
        }
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

    private static func reach(_ schema: GesSchema, _ reached: inout [String: GesSchema], _ handles: inout [String: GesHandleType]) throws {
        if let known = reached[schema.name] {
            // Model types share one namespace: a message beside a schema of its name has no second type name.
            if known === schema { return }
            throw GesGeneratorError(
                GesGeneratorError.nameCollision,
                "The model types of \(describe(known)) and \(describe(schema)) are both named '\(schema.name)'; models take declared names without a message suffix, so put the roots that reach them into different model groups, generated into different namespaces."
            )
        }
        reached[schema.name] = schema
        for field in schema.fields {
            for type in field.alternatives { try reach(type, &reached, &handles) }
        }
        for member in schema.members { try reach(member, &reached, &handles) }
    }

    // The models that wire code generated with `--models` converts to. Each model group is generated with `--model` like any
    // other, and those groups whose roots reach a wire type lie in the models' namespace: their types, laid out as the group's
    // generation lays them out, may not share a name there. A group that reaches no wire type is not part of that namespace,
    // so its types count for nothing, even where they share a name with a model the wire code converts to.
    static func converted(_ set: GesSchemaSet, wire: GesSchemaLayout) throws -> GesModelLayout? {
        let wanted = Set(wire.types.map(\.lookup))
        var groups: [String] = []
        for schema in set.schemas {
            if let group = schema.modelGroup, !groups.contains(group) { groups.append(group) }
        }
        var types: [String: GesModelType] = [:]
        var protocols: [String: GesModelProtocol] = [:]
        var handles: [String: GesModelHandle] = [:]
        var owners: [String: String] = [:]
        for group in groups {
            let roots = GesSchemaGenerator.modelRoots(set, group: group)
            var seen: [ObjectIdentifier: GesSchema] = [:]
            for root in roots { collect(try set.schema(root), &seen) }
            guard seen.values.contains(where: { wanted.contains(set.rootName(of: $0)) }) else { continue }
            let layout = try build(set, roots: roots)
            for type in layout.types {
                if let known = types[type.name] {
                    // One schema in several groups is one model type; two schemas of one name are two.
                    if known.lookup == type.lookup { continue }
                    throw GesGeneratorError(
                        GesGeneratorError.nameCollision,
                        "The wire code converts to the models of the groups '\(owners[known.name]!)' and '\(group)', whose model types of \(describe(try set.schema(known.lookup))) and \(describe(try set.schema(type.lookup))) are both named '\(type.name)' in the models' namespace; generate the wire roots that reach each group separately, each with the namespace of that group's models in --models."
                    )
                }
                types[type.name] = type
                owners[type.name] = group
            }
            for protocolType in layout.protocols where protocols[protocolType.name] == nil { protocols[protocolType.name] = protocolType }
            for handle in layout.handles where handles[handle.name] == nil { handles[handle.name] = handle }
        }
        if types.isEmpty { return nil }
        var converted = GesModelLayout(roots: [], types: types.keys.sorted(by: GesSchemaLayoutBuilder.ordinal).map { types[$0]! })
        converted.protocols = protocols.keys.sorted(by: GesSchemaLayoutBuilder.ordinal).map { protocols[$0]! }
        converted.handles = handles.keys.sorted(by: GesSchemaLayoutBuilder.ordinal).map { handles[$0]! }
        return converted
    }

    // Every schema that a model of the schema names, as reach finds them, without the check of their names.
    private static func collect(_ schema: GesSchema, _ seen: inout [ObjectIdentifier: GesSchema]) {
        if seen.updateValue(schema, forKey: ObjectIdentifier(schema)) != nil { return }
        for field in schema.fields {
            for type in field.alternatives { collect(type, &seen) }
        }
        for member in schema.members { collect(member, &seen) }
    }

    private static func collect(_ type: GesSchemaType, _ seen: inout [ObjectIdentifier: GesSchema]) {
        if let schema = type.schema { collect(schema, &seen) }
        for element in type.element { collect(element, &seen) }
    }

    private static func describe(_ schema: GesSchema) -> String {
        switch schema.kind {
        case .message: "the message " + schema.name
        case .messages: "the message set " + schema.name
        default: "the schema :" + schema.name
        }
    }

    private static func reach(_ type: GesSchemaType, _ reached: inout [String: GesSchema], _ handles: inout [String: GesHandleType]) throws {
        if let schema = type.schema { try reach(schema, &reached, &handles) }
        if let handle = type.handleType { reach(handle, &handles) }
        for element in type.element { try reach(element, &reached, &handles) }
    }

    // A handle type brings its `<Name>Handle` and a composite's root handle type, whose `<Name>Handle` it holds. It never brings
    // its state schema: the generated handle names the state in documentation only, so the state is a model type only where
    // a root reaches it otherwise, as in the wire layout.
    private static func reach(_ handle: GesHandleType, _ handles: inout [String: GesHandleType]) {
        if handles[handle.name] != nil { return }
        handles[handle.name] = handle
        if let root = handle.root { reach(root, &handles) }
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
            let type =
                switch alternative.kind {
                case .reference: alternative.schema!.name
                case .handle: "@" + alternative.handleType!.name
                default: alternative.kind.rawValue
                }
            let item = GesModelField()
            item.name = alternative.label.isEmpty ? String(type.drop { $0 == "@" }) : alternative.label
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
            value.fixed = type.isExact
            value.depth = type.depth
            value.element = try field("[]", path + "[]", type.element)
        case .list:
            value.kind = .values
        case .map where type.handleType != nil:
            value.kind = .handleMap
            value.handle = type.handleType!.name
            value.max = type.max
            value.element = try field("[]", path + "[].value", type.element)
        case .map:
            value.kind = .map
        case .handle:
            value.kind = .handle
            value.handle = type.handleType!.name
            value.versioned = type.isVersioned
        case .dice:
            value.kind = .dice
            value.max = type.max
            value.lower = bound(type.lower)
            value.upper = bound(type.upper)
        case .point, .vector, .box, .poly, .disc:
            value.kind = type.kind == .point ? .point : type.kind == .vector ? .vector : type.kind == .box ? .box : type.kind == .disc ? .disc : .poly
            value.unit = GesSchemaLayoutBuilder.unitName(type.kind, type.unit)
            value.max = type.max
            value.axes = type.axes.map(\.name)
        case .reference:
            value.kind = .schema
            value.schema = type.schema!.name
            value.packed = type.schema!.packedWidth > 0
        case .nothing:
            value.kind = .nothing
        default:
            throw GesGeneratorError(GesGeneratorError.unsupported, "A \(type.kind.rawValue) at '\(path)' has no model type.")
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
        // Only a generation with a model set tells its kinds of types apart, so that other layouts stay unchanged.
        let kinds = layout.types.contains { $0.kind == .messages }
        for type in layout.types {
            json.beginObject()
            json.string("name", type.name)
            if kinds { json.string("kind", type.kind == .message ? "message" : type.kind == .messages ? "messages" : "schema") }
            json.string("hash", GesSchemaLayoutJson.hex(type.hash))
            if let packed = type.packed?.type(type.name) {
                json.number("packedWidth", Int64(packed.packedWidth))
                json.number("packedBits", Int64(packed.packedBits))
            }
            if type.kind != .messages || !type.fields.isEmpty {
                json.beginArray("fields")
                for field in type.fields { self.field(&json, field, member: true) }
                json.endArray()
            }
            if type.kind == .message { json.strings("tags", type.tags) }
            if type.kind == .messages { json.strings("members", type.members) }
            GesSchemaLayoutJson.constants(&json, type.constants)
            GesSchemaLayoutJson.computed(&json, type.computed)
            if !type.takes.isEmpty { json.strings("takes", type.takes) }
            if !type.conversions.isEmpty {
                json.beginArray("conversions")
                for conversion in type.conversions {
                    json.beginObject()
                    json.string("source", conversion.source)
                    json.strings("paths", conversion.paths.map { $0.joined(separator: ".") })
                    json.endObject()
                }
                json.endArray()
            }
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
        // Only a model that reaches handle types names them, so that other layouts stay unchanged.
        if !layout.handles.isEmpty {
            json.beginArray("handles")
            for handle in layout.handles {
                json.beginObject()
                json.string("name", handle.name)
                json.string("hash", GesSchemaLayoutJson.hex(handle.hash))
                json.string("state", handle.state)
                json.string("version", GesLayoutHandle.versionName(handle.version))
                if let root = handle.root {
                    json.string("root", root)
                    json.key("part")
                    key(&json, handle.part!)
                } else {
                    if let partless = handle.partlessRoot { json.string("root", partless) }
                    json.key("id")
                    key(&json, handle.id!)
                }
                if !handle.projections.isEmpty { json.strings("projections", handle.projections) }
                json.endObject()
            }
            json.endArray()
        }
        json.endObject()
        return json.text + "\n"
    }

    private static func key(_ json: inout GesSchemaLayoutJson.Writer, _ key: GesHandleKey) {
        json.beginValueObject()
        if key.isText {
            json.string("kind", "text")
            json.number("max", key.maxLength)
        } else {
            json.string("kind", "integer")
            json.number("lower", key.minimum)
            json.number("upper", key.maximum)
        }
        json.endObject()
    }

    private static func field(_ json: inout GesSchemaLayoutJson.Writer, _ field: GesModelField, member: Bool, value: Bool = false) {
        if value { json.beginValueObject() } else { json.beginObject() }
        json.string("name", field.name)
        json.string("path", field.path)
        if member {
            json.boolean("optional", field.optional)
            json.boolean("key", field.key)
            if field.unlabeled { json.boolean("unlabeled", true) }
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
            if value.fixed { json.boolean("fixed", true) }
            if value.depth > 0 { json.number("depth", Int64(value.depth)) }
            json.key("element")
            field(&json, value.element!, member: false, value: true)
        case .dice:
            optional(&json, "max", value.max)
            optional(&json, "lower", value.lower)
            optional(&json, "upper", value.upper)
        case .point, .vector, .box, .disc:
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
        case .handle:
            json.string("name", value.handle)
            json.boolean("versioned", value.versioned)
        case .handleMap:
            json.string("key", value.handle)
            optional(&json, "max", value.max)
            json.key("value")
            field(&json, value.element!, member: false, value: true)
        case .boolean, .nothing, .map, .values:
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
        case .disc: "disc"
        case .schema: "schema"
        case .union: "union"
        case .map: "map"
        case .values: "values"
        case .handle: "handle"
        case .handleMap: "handleMap"
        case .nothing: "nothing"
        }
    }
}
