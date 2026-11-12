// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// The kinds of declarations that have a hash and can be bound.
public enum GesSchemaKind: Sendable {
    /// A schema.
    case schema
    /// A message.
    case message
    /// A message set.
    case messages
}

/// A bound, step or delta range: a parameter or an exact decimal.
public struct GesSchemaBound: Sendable {
    /// The parameter's name, or an empty text for an exact decimal.
    public let parameter: String
    /// The significand of the exact decimal, without trailing zeros.
    public let significand: Int64
    /// The decimal exponent of the exact decimal.
    public let exponent: Int

    init(_ value: GesResolvedValue) {
        parameter = value.parameter
        significand = value.value.significand
        exponent = value.value.exponent
    }

    /// Whether the value comes from a parameter at binding.
    public var isParameter: Bool { !parameter.isEmpty }

    /// The closest Binary64 of the exact decimal, or nil for a parameter.
    public var doubleValue: Double? { isParameter ? nil : Double("\(significand)e\(exponent)") }
}

/// The limits of a numeric axis of a Point or Vector.
public struct GesSchemaAxisInfo: Sendable {
    /// The axis: `x`, `y` or `z`.
    public let name: String
    /// Whether the axis holds whole numbers.
    public let isInteger: Bool
    /// Whether the range wraps around.
    public let isWrapping: Bool
    /// The lower bound, if there is a range.
    public let lower: GesSchemaBound?
    /// The upper bound, if there is a range.
    public let upper: GesSchemaBound?
    /// The step, if the axis is quantized.
    public let step: GesSchemaBound?
    /// The range of a delta, if there is one.
    public let delta: GesSchemaBound?

    init(_ axis: GesResolvedAxis) {
        name = axis.axis
        let constraints = axis.constraints
        isInteger = constraints.integer
        isWrapping = constraints.wrapping
        lower = constraints.lower.map(GesSchemaBound.init)
        upper = constraints.upper.map(GesSchemaBound.init)
        step = constraints.step.map(GesSchemaBound.init)
        delta = constraints.delta.map(GesSchemaBound.init)
    }
}

/// The type of a field, or one alternative of a union.
public struct GesSchemaType: Sendable {
    /// The kind of type.
    public let kind: GesSchemaTypeKind
    /// The unit of a Quantity, Point or Vector.
    public let unit: GesQuantityUnit
    /// Whether a number holds whole numbers.
    public let isInteger: Bool
    /// Whether the range wraps around.
    public let isWrapping: Bool
    /// The lower bound, if there is a range.
    public let lower: GesSchemaBound?
    /// The upper bound, if there is a range.
    public let upper: GesSchemaBound?
    /// The step, if the number is quantized.
    public let step: GesSchemaBound?
    /// The range of a delta, if there is one.
    public let delta: GesSchemaBound?
    /// The closed set of a Tag, empty when any tag is allowed, or the members of Flags in declared order.
    public let tags: [String]
    /// The name of the declared tag set a Tag takes its members from, or an empty string; the name belongs to no hash and
    /// does not change the encoding.
    public let tagSet: String
    /// The label of a union alternative, as in `bind: :Loadout_UnitBind`, or an empty string; generated code names the
    /// alternative after it, and it belongs to no hash.
    public let label: String
    /// The maximum of a Text in UTF-8 bytes or of a List in elements, if there is one.
    public let max: Int64?
    /// The alternatives of a List's element; empty when any element is allowed.
    public let element: [GesSchemaType]
    /// The axes of an axis block in x, y, z order; empty without a block, when all three are stored in full.
    public let axes: [GesSchemaAxisInfo]
    /// The schema a `.reference` refers to.
    public let schema: GesSchema?

    init(_ type: GesResolvedType) {
        kind = type.kind
        unit = type.unit
        let constraints = type.constraints
        isInteger = constraints.integer
        isWrapping = constraints.wrapping
        lower = constraints.lower.map(GesSchemaBound.init)
        upper = constraints.upper.map(GesSchemaBound.init)
        step = constraints.step.map(GesSchemaBound.init)
        delta = constraints.delta.map(GesSchemaBound.init)
        tags = type.tags ?? []
        tagSet = type.tagSet
        label = type.label
        max = type.max
        element = (type.element ?? []).map(GesSchemaType.init)
        axes = (type.axes ?? []).map(GesSchemaAxisInfo.init)
        schema = type.reference?.schema
    }
}

/// A field of a schema or an argument of a message.
public struct GesSchemaFieldInfo: Sendable {
    /// The name.
    public let name: String
    /// Whether a message argument has no label.
    public let isUnlabeled: Bool
    /// Whether the field may be absent.
    public let isOptional: Bool
    /// Whether the field is the key of its schema.
    public let isKey: Bool
    /// The types of the field: one, or the alternatives of a union in source order.
    public let alternatives: [GesSchemaType]
    /// The value the field takes when a value or document leaves it out, or nil when it has none.
    public let defaultValue: GesSchemaDefaultInfo?

    init(_ field: GesResolvedField) {
        name = field.name
        isUnlabeled = field.unlabeled
        isOptional = field.marker == .optional
        isKey = field.marker == .key
        alternatives = field.alternatives.map(GesSchemaType.init)
        defaultValue = field.defaultValue.map(GesSchemaDefaultInfo.init)
    }
}

/// A field written `computed by`: a value derived from the other fields that is never encoded or read from a document.
public struct GesSchemaComputedFieldInfo: Sendable {
    /// The name.
    public let name: String
    /// The type: `.number`, `.quantity`, `.percentage` or `.boolean`.
    public let kind: GesSchemaTypeKind
    /// The unit of a Quantity.
    public let unit: GesQuantityUnit
    /// Whether the value may be absent, which generated code then gives an optional type.
    public let isOptional: Bool
    /// The checked expression in the prefix form of the canonical form, with literals as exact decimals.
    public let expression: String
    /// The checked expression tree, which generated code translates.
    @_spi(Generator) public let tree: GesComputedNode
    /// The expression as its source spells it, on one line.
    @_spi(Generator) public let spelling: String

    init(_ field: GesResolvedComputed) {
        name = field.name
        kind = field.kind
        unit = field.unit
        isOptional = field.isOptional
        var text = ""
        GesComputedCanonical.write(field.expression, &text)
        expression = text
        tree = field.expression
        spelling = GesSchemaFormatter.expression(field.syntax.computed!)
    }
}

/// The fields a schema takes from another with `with`.
public struct GesSchemaTakeInfo: Sendable {
    /// The schema whose fields are taken.
    public let schema: GesSchema
    /// Whether `all optional` makes every taken field optional and drops its default.
    public let isAllOptional: Bool
    /// The fields that `without` leaves out, in written order.
    public let without: [String]

    /// Whether the fields are taken unchanged, so that generated models take the schema's protocol or interface.
    public var isUnchanged: Bool { !isAllOptional && without.isEmpty }
}

/// The kinds of a field's default.
public enum GesSchemaDefaultKind: Sendable {
    /// A number of a numeric field, in base units; a Percentage holds its ratio.
    case number
    /// `true` or `false` of a Boolean field.
    case boolean
    /// A tag of a Tag field.
    case tag
    /// A sibling field of the same type, whose value the field takes.
    case field
}

/// The value a field takes when a value or document leaves it out, declared with `default`; the wire always carries the field.
public struct GesSchemaDefaultInfo: Sendable {
    /// The kind of default.
    public let kind: GesSchemaDefaultKind
    /// The exact number in base units of a ``GesSchemaDefaultKind/number`` default.
    public let number: GesSchemaBound?
    /// The value of a ``GesSchemaDefaultKind/boolean`` default.
    public let boolean: Bool
    /// The tag without `#` of a ``GesSchemaDefaultKind/tag`` default, the sibling field of a ``GesSchemaDefaultKind/field``
    /// default, or an empty string.
    public let name: String

    init(_ fallback: GesResolvedDefault) {
        kind = fallback.kind
        number = fallback.kind == .number ? GesSchemaBound(GesResolvedValue(value: fallback.value)) : nil
        boolean = fallback.boolean
        name = fallback.name
    }
}

/// A parameter that a declaration reaches and every binding must supply.
public struct GesSchemaParameterInfo: Sendable {
    /// The name.
    public let name: String
    /// The kind: `.number`, `.percentage` or `.quantity`.
    public let kind: GesSchemaTypeKind
    /// The unit of a Quantity.
    public let unit: GesQuantityUnit
}

/// A constant of a schema, declared as `let name be value`: never encoded, part of the hash, and generated as a static
/// member of the schema's types.
public struct GesSchemaConstantInfo: Sendable {
    /// The name.
    public let name: String
    /// The kind its literal gives it: `.number`, `.percentage` or `.quantity`.
    public let kind: GesSchemaTypeKind
    /// The unit of a Quantity.
    public let unit: GesQuantityUnit
    /// Whether the value is a whole Number within Int64; any other value is a Binary64 in base units.
    public let isInteger: Bool
    /// The exact value in base units; a Percentage holds its ratio.
    public let value: GesSchemaBound

    init(_ declared: GesResolvedLet) {
        name = declared.name
        kind = declared.kind
        unit = declared.unit
        isInteger = declared.integer
        value = GesSchemaBound(GesResolvedValue(value: declared.value))
    }
}

/// The immutable description of one schema, message or message set of a `GesSchemaSet`. It can be shared between threads
/// and bound any number of times.
public final class GesSchema: @unchecked Sendable {
    let declaration: GesResolvedDeclaration
    /// The name.
    public let name: String
    /// The kind of declaration.
    public let kind: GesSchemaKind
    /// The tags of a message, in declared order.
    public let tags: [String]
    /// The group in which the declaration is a wire root, `default` for `wire` without a group, or nil when it is no wire
    /// root. Roles are generation metadata: they are not part of the canonical form or any hash.
    public let wireGroup: String?
    /// The group in which the schema is a model root, `default` for `model` without a group, or nil when it is no model
    /// root. Roles are not part of the canonical form or any hash.
    public let modelGroup: String?
    /// Whether the schema is abstract: it only lends its fields with `with` and is never bound, checked against a document
    /// or generated as a type.
    public let isAbstract: Bool
    /// The schemas whose fields this schema takes with `with`, in declared order. Their fields are part of ``fields``, as if
    /// written in place.
    public private(set) var takes: [GesSchemaTakeInfo] = []
    /// The schema hash: FNV-1a 64 over the canonical form.
    public private(set) var hash: UInt64 = 0
    /// The parameters it reaches, in ascending name order.
    public private(set) var parameters: [GesSchemaParameterInfo] = []
    /// The fields of a schema or the arguments of a message, in declared order.
    public private(set) var fields: [GesSchemaFieldInfo] = []
    /// The fields of a schema written `computed by`, own and taken, in the order their expressions may read each other.
    public private(set) var computedFields: [GesSchemaComputedFieldInfo] = []
    /// The constants a schema declares with `let`, in declared order.
    public private(set) var constants: [GesSchemaConstantInfo] = []
    /// The members of a message set, in declared order.
    public private(set) var members: [GesSchema] = []

    init(_ declaration: GesResolvedDeclaration) {
        self.declaration = declaration
        name = declaration.name
        kind = declaration.kind
        tags = declaration.tags
        wireGroup = declaration.syntax.wireGroup
        modelGroup = declaration.syntax.modelGroup
        isAbstract = declaration.isAbstract
    }

    /// The canonical form, which the hash runs over. It is computed on each call.
    public var canonicalForm: String {
        // Reading the set has already computed it once without an error.
        (try? GesSchemaCanonical.form(declaration)) ?? ""
    }

    /// Binds the declaration for encoding and decoding. It can be bound any number of times.
    /// - Parameter parameters: The values of the parameters it reaches, if it has any.
    /// - Returns: The immutable bound schema.
    /// - Throws: `GesSchemaError` when the declaration cannot be encoded or a value breaks a rule.
    public func bind(parameters: GesSchemaParameters = GesSchemaParameters()) throws -> GesBoundSchema {
        try GesSchemaBinder.bind(declaration, parameters)
    }

    /// Checks a GES document against this schema, whatever its header says.
    /// - Parameters:
    ///   - text: The document text.
    ///   - parameters: Parameter values; a bound whose parameter has no value is not checked.
    /// - Throws: `GesSchemaError` with a `document.*` code and its position, or a `schema.*` code when this is no schema or a
    ///   parameter value has another kind.
    public func checkDocument(_ text: String, parameters: GesSchemaParameters? = nil) throws {
        try GesSchemaSet.check(try GesSchemaSet.readDocument(text), declaration, parameters)
    }

    // Declarations can refer to each other, so the sets are filled once every declaration has its object.
    func complete() throws {
        var declarations: [GesResolvedDeclaration] = []
        var reached: [String: GesSchemaTypeSyntax] = [:]
        try GesSchemaCanonical.reach(declaration, &declarations, &reached)
        parameters = reached.keys.sorted { Array($0.utf8).lexicographicallyPrecedes(Array($1.utf8)) }.map { GesSchemaParameterInfo(name: $0, kind: reached[$0]!.kind, unit: reached[$0]!.unit) }
        hash = GesSchemaCanonical.hash(try GesSchemaCanonical.form(declaration))
        fields = declaration.fields.map(GesSchemaFieldInfo.init)
        computedFields = declaration.computed.map(GesSchemaComputedFieldInfo.init)
        constants = declaration.lets.map(GesSchemaConstantInfo.init)
        members = declaration.members.map { $0.schema! }
        takes = declaration.takes.map { GesSchemaTakeInfo(schema: $0.schema.schema!, isAllOptional: $0.with.allOptional, without: $0.with.without) }
    }
}
