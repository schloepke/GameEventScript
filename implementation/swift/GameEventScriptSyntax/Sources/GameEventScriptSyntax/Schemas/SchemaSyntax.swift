// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The kinds of types a schema field can have.
public enum GesSchemaTypeKind: String, Sendable, Equatable {
    /// A Boolean.
    case boolean = "Boolean"
    /// A Number.
    case number = "Number"
    /// A Percentage.
    case percentage = "Percentage"
    /// A Number with a unit.
    case quantity = "Quantity"
    /// A Point.
    case point = "Point"
    /// A Vector.
    case vector = "Vector"
    /// A Tag, of a closed set or any.
    case tag = "Tag"
    /// A Text.
    case text = "Text"
    /// A List.
    case list = "List"
    /// A reference to a schema.
    case reference = "Reference"
    /// A Map.
    case map = "Map"
    /// A Range.
    case range = "Range"
    /// A built-in Series.
    case series = "Series"
    /// A Dice value.
    case dice = "Dice"
    /// A Message.
    case message = "Message"
    /// A Handler.
    case handler = "Handler"
    /// Nothing.
    case nothing = "Nothing"
    /// A Bytes value.
    case bytes = "Bytes"
    /// Flags of a closed set, as a Map of the set members.
    case flags = "Flags"
    /// An axis-aligned Box between two Points.
    case box = "Box"
    /// A polygon of Points in the plane z = 0.
    case poly = "Poly"
    /// A filled circle in the plane z = 0: a centre Point and a radius.
    case disc = "Disc"
}

@_spi(Syntax) public enum GesSchemaTypeNames {
    // A name no declaration can take: the types below and the language's Record, which is no schema type.
    @_spi(Syntax) public static func isBuiltIn(_ name: String) -> Bool { name == "Record" || kind(name) != nil }

    // The built-in type of a name; any other name refers to a declaration.
    @_spi(Syntax) public static func kind(_ name: String) -> GesSchemaTypeKind? {
        switch name {
        case "Boolean": .boolean
        case "Number": .number
        case "Percentage": .percentage
        case "Quantity": .quantity
        case "Point": .point
        case "Vector": .vector
        case "Box": .box
        case "Poly": .poly
        case "Disc": .disc
        case "Tag": .tag
        case "Text": .text
        case "Bytes": .bytes
        case "Flags": .flags
        case "List": .list
        case "Map": .map
        case "Range": .range
        case "Series": .series
        case "Dice": .dice
        case "Message": .message
        case "Handler": .handler
        case "Nothing": .nothing
        default: nil
        }
    }
}

@_spi(Syntax) public enum GesSchemaFieldMarker: Sendable, Equatable {
    case none
    case optional
    case key
}

@_spi(Syntax) public enum GesSchemaDeclarationKind: Sendable, Equatable {
    case constant
    case parameter
    case schema
    case message
    case messages
    case tagSet
}

/// An exact decimal in lowest terms: no trailing zero in the significand, and zero is 0e0.
@_spi(Syntax) public struct GesSchemaDecimal: Sendable, Equatable {
    @_spi(Syntax) public let significand: Int64
    @_spi(Syntax) public let exponent: Int

    @_spi(Syntax) public init(_ significand: Int64, _ exponent: Int) {
        var significand = significand
        var exponent = significand == 0 ? 0 : exponent
        while significand != 0 && significand % 10 == 0 {
            significand /= 10
            exponent += 1
        }
        self.significand = significand
        self.exponent = exponent
    }

    /// The canonical spelling of the schema hash.
    @_spi(Syntax) public var canonical: String { "\(significand)e\(exponent)" }
}

/// A literal, a constant, or a name of a let or parameter in a bound, step, delta range or let.
@_spi(Syntax) public struct GesSchemaValueSyntax: Sendable {
    @_spi(Syntax) public var value = GesSchemaDecimal(0, 0)
    @_spi(Syntax) public var unit = GesQuantityUnit.none
    @_spi(Syntax) public var isPercent = false
    /// The spelling of a literal, which formatting keeps.
    @_spi(Syntax) public var text = ""
    /// The name of a constant, or a lowercase name of a let of the schema or a parameter of the file; empty for a literal.
    @_spi(Syntax) public var constant = ""
    @_spi(Syntax) public var name = ""
    @_spi(Syntax) public var offset = 0
}

@_spi(Syntax) public struct GesSchemaConstraintsSyntax: Sendable {
    @_spi(Syntax) public var integer = false
    @_spi(Syntax) public var wrapping = false
    @_spi(Syntax) public var lower: GesSchemaValueSyntax?
    @_spi(Syntax) public var upper: GesSchemaValueSyntax?
    @_spi(Syntax) public var step: GesSchemaValueSyntax?
    @_spi(Syntax) public var delta: GesSchemaValueSyntax?

    @_spi(Syntax) public init() {}
}

@_spi(Syntax) public struct GesSchemaAxisSyntax: Sendable {
    @_spi(Syntax) public let axis: String
    @_spi(Syntax) public let constraints: GesSchemaConstraintsSyntax
    @_spi(Syntax) public let offset: Int
}

@_spi(Syntax) public struct GesSchemaTypeSyntax: Sendable {
    @_spi(Syntax) public var kind = GesSchemaTypeKind.nothing
    @_spi(Syntax) public var unit = GesQuantityUnit.none
    /// The referenced schema's name.
    @_spi(Syntax) public var name = ""
    @_spi(Syntax) public var tags: [String]?
    /// The written maximum of Text and List, or the let that holds it, and where that name starts.
    @_spi(Syntax) public var max: Int64?
    @_spi(Syntax) public var maxName = ""
    @_spi(Syntax) public var maxOffset = 0
    /// The alternatives of a List's element.
    @_spi(Syntax) public var element: [GesSchemaTypeSyntax]?
    /// The written `depth` of a List whose element is the schema that declares it, and where it starts; 0 without one.
    @_spi(Syntax) public var depth = 0
    @_spi(Syntax) public var depthOffset = 0
    @_spi(Syntax) public var constraints = GesSchemaConstraintsSyntax()
    @_spi(Syntax) public var axes: [GesSchemaAxisSyntax]?
    @_spi(Syntax) public var offset = 0
    /// The label of a union alternative, such as `bind` in `bind: :Loadout_UnitBind`, or empty, and where it starts.
    @_spi(Syntax) public var label = ""
    @_spi(Syntax) public var labelOffset = 0
}

@_spi(Syntax) public struct GesSchemaFieldSyntax: Sendable {
    @_spi(Syntax) public let name: String
    @_spi(Syntax) public let unlabeled: Bool
    @_spi(Syntax) public let alternatives: [GesSchemaTypeSyntax]
    @_spi(Syntax) public let marker: GesSchemaFieldMarker
    @_spi(Syntax) public let defaultValue: GesSchemaDefaultSyntax?
    @_spi(Syntax) public let offset: Int
    /// The expression of a field written `computed by`, which is never encoded.
    @_spi(Syntax) public var computed: GesSchemaExpressionSyntax?
    /// The index of the entry among all fields, lets and withs of its schema, which orders computed and taken fields.
    @_spi(Syntax) public var sequence = 0
}

/// The kinds of the expressions of computed fields.
@_spi(Syntax) public enum GesSchemaExpressionKind: Sendable {
    /// A number literal.
    case literal
    /// `true` or `false`.
    case boolean
    /// A file constant.
    case constant
    /// A field, let or projection variable.
    case name
    /// A field of a record.
    case member
    /// Unary minus.
    case negate
    /// `not`.
    case not
    /// `abs`.
    case abs
    /// A binary operator.
    case binary
    /// `value when condition, …, otherwise fallback`.
    case choice
    /// `min of`.
    case min
    /// `max of`.
    case max
    /// `clamp … between … and …`.
    case clamp
    /// `default`.
    case `default`
    /// `has value`.
    case hasValue
    /// `[:sum]`.
    case sum
    /// `[:count]`.
    case count
    /// A function with its arguments and its ASCII spelling in `name`, such as `sqrt`, `round half up` or `atan2`.
    case function
}

/// The binary operators of computed expressions.
@_spi(Syntax) public enum GesSchemaBinaryOperator: Sendable {
    /// `+`.
    case add
    /// `-`.
    case subtract
    /// `*`.
    case multiply
    /// `/`.
    case divide
    /// `<`.
    case less
    /// `<=`.
    case lessOrEqual
    /// `>`.
    case greater
    /// `>=`.
    case greaterOrEqual
    /// `=`.
    case equal
    /// `<>`.
    case notEqual
    /// `and`.
    case and
    /// `or`.
    case or
    /// `^`, `²` or `³`.
    case power
}

/// An expression of a computed field, a subset of GES expressions with their spellings. A member reads `name` from its one
/// child; a choice holds value and condition pairs and then the fallback; a sum holds its list and, with a variable in
/// `name`, the projection; a clamp holds the value and both bounds; a function holds its arguments and the function's ASCII
/// spelling in `name`.
@_spi(Syntax) public final class GesSchemaExpressionSyntax: Sendable {
    @_spi(Syntax) public let kind: GesSchemaExpressionKind
    @_spi(Syntax) public let binary: GesSchemaBinaryOperator
    @_spi(Syntax) public let literal: GesSchemaValueSyntax?
    @_spi(Syntax) public let boolean: Bool
    @_spi(Syntax) public let name: String
    @_spi(Syntax) public let children: [GesSchemaExpressionSyntax]
    /// The depth of the tree below and including this node, which the reader limits.
    @_spi(Syntax) public let depth: Int
    @_spi(Syntax) public let offset: Int

    init(
        _ kind: GesSchemaExpressionKind,
        offset: Int,
        binary: GesSchemaBinaryOperator = .add,
        literal: GesSchemaValueSyntax? = nil,
        boolean: Bool = false,
        name: String = "",
        children: [GesSchemaExpressionSyntax] = []
    ) {
        self.kind = kind
        self.binary = binary
        self.literal = literal
        self.boolean = boolean
        self.name = name
        self.children = children
        depth = (children.map(\.depth).max() ?? 0) + 1
        self.offset = offset
    }
}

/// The value a field takes when a value or document leaves it out: `default` with a number, a constant, a let or a sibling
/// field (all in `value`), `true` or `false`, a tag, or a record literal such as `[:]` or `[efficiency: 90%]`.
@_spi(Syntax) public struct GesSchemaDefaultSyntax: Sendable {
    @_spi(Syntax) public var value: GesSchemaValueSyntax?
    @_spi(Syntax) public var boolean: Bool?
    @_spi(Syntax) public var tag: String?
    /// The named fields of a record literal in written order, empty for `[:]`; nil for every other default.
    @_spi(Syntax) public var record: [GesSchemaRecordEntrySyntax]?
    @_spi(Syntax) public var offset = 0
}

/// One `name: value` of a record literal; the value is again a number, a constant, `true`, `false`, a tag or a record literal.
@_spi(Syntax) public struct GesSchemaRecordEntrySyntax: Sendable {
    @_spi(Syntax) public let name: String
    @_spi(Syntax) public let value: GesSchemaDefaultSyntax
    @_spi(Syntax) public let offset: Int
}

/// A constant of a schema, `let name be value`, which its fields can name.
@_spi(Syntax) public struct GesSchemaLetSyntax: Sendable {
    @_spi(Syntax) public let name: String
    @_spi(Syntax) public let value: GesSchemaValueSyntax
    @_spi(Syntax) public let offset: Int
}

/// `with :Name all optional without (a, b)` inside a schema: the fields of another schema, copied where it stands.
@_spi(Syntax) public struct GesSchemaWithSyntax: Sendable {
    @_spi(Syntax) public var name = ""
    @_spi(Syntax) public var allOptional = false
    @_spi(Syntax) public var without: [String] = []
    @_spi(Syntax) public var withoutOffsets: [Int] = []
    /// The number of the schema's own fields declared before it.
    @_spi(Syntax) public var position = 0
    /// The index of the entry among all fields, lets and withs of its schema.
    @_spi(Syntax) public var sequence = 0
    @_spi(Syntax) public var offset = 0
}

@_spi(Syntax) public struct GesSchemaDeclarationSyntax: Sendable {
    @_spi(Syntax) public var kind = GesSchemaDeclarationKind.constant
    @_spi(Syntax) public var name = ""
    @_spi(Syntax) public var offset = 0
    @_spi(Syntax) public var value: GesSchemaValueSyntax?
    /// The kind and unit of a parameter.
    @_spi(Syntax) public var type: GesSchemaTypeSyntax?
    @_spi(Syntax) public var fields: [GesSchemaFieldSyntax] = []
    @_spi(Syntax) public var lets: [GesSchemaLetSyntax] = []
    @_spi(Syntax) public var withs: [GesSchemaWithSyntax] = []
    /// The fields written `computed by`, in declared order; `fields` holds the others.
    @_spi(Syntax) public var computed: [GesSchemaFieldSyntax] = []
    /// An abstract schema only lends its fields with `with`.
    @_spi(Syntax) public var isAbstract = false
    /// A packed schema is one machine word.
    @_spi(Syntax) public var isPacked = false
    /// The width of a schema written `packed(width)`, 8, 16, 32 or 64; 0 otherwise.
    @_spi(Syntax) public var packedWidth = 0
    /// The UTF-8 offset of the word `packed`.
    @_spi(Syntax) public var packedOffset = 0
    @_spi(Syntax) public var tags: [String] = []
    @_spi(Syntax) public var members: [String] = []
    @_spi(Syntax) public var memberOffsets: [Int] = []
    /// The group of a wire or model root, or nil when the declaration has no such role.
    @_spi(Syntax) public var wireGroup: String?
    @_spi(Syntax) public var modelGroup: String?
}

@_spi(Syntax) public struct GesSchemaDiagnostic: Sendable, Equatable {
    @_spi(Syntax) public static let syntax = "schema.syntax"
    @_spi(Syntax) public static let duplicateName = "schema.duplicateName"
    @_spi(Syntax) public static let unknownName = "schema.unknownName"
    @_spi(Syntax) public static let cycle = "schema.cycle"
    @_spi(Syntax) public static let unitMismatch = "schema.unitMismatch"
    @_spi(Syntax) public static let invalidRange = "schema.invalidRange"
    @_spi(Syntax) public static let invalidStep = "schema.invalidStep"
    @_spi(Syntax) public static let invalidConstraint = "schema.invalidConstraint"
    @_spi(Syntax) public static let notEncodable = "schema.notEncodable"
    @_spi(Syntax) public static let invalidKey = "schema.invalidKey"
    @_spi(Syntax) public static let invalidUnion = "schema.invalidUnion"
    @_spi(Syntax) public static let invalidBound = "schema.invalidBound"
    @_spi(Syntax) public static let tooWide = "schema.tooWide"
    @_spi(Syntax) public static let tooLarge = "schema.tooLarge"
    @_spi(Syntax) public static let invalidRole = "schema.invalidRole"
    @_spi(Syntax) public static let duplicateRole = "schema.duplicateRole"
    @_spi(Syntax) public static let invalidDefault = "schema.invalidDefault"
    @_spi(Syntax) public static let invalidAbstract = "schema.invalidAbstract"
    @_spi(Syntax) public static let packedField = "schema.packedField"
    @_spi(Syntax) public static let packedTooWide = "schema.packedTooWide"
    @_spi(Syntax) public static let invalidExpression = "schema.invalidExpression"
    @_spi(Syntax) public static let computedMayBeAbsent = "schema.computedMayBeAbsent"

    @_spi(Syntax) public let code: String
    /// The UTF-8 offset of the error.
    @_spi(Syntax) public let offset: Int
    @_spi(Syntax) public let line: Int
    @_spi(Syntax) public let column: Int
    @_spi(Syntax) public let message: String

    @_spi(Syntax) public init(_ code: String, _ bytes: [UInt8], _ offset: Int, _ message: String) {
        self.code = code
        self.offset = offset
        self.message = message
        (line, column) = GesDocumentSyntax.location(bytes, offset)
    }
}

@_spi(Syntax) public struct GesSchemaSourceSyntax: Sendable {
    /// The UTF-8 bytes of the text, which all offsets index.
    @_spi(Syntax) public let bytes: [UInt8]
    @_spi(Syntax) public let declarations: [GesSchemaDeclarationSyntax]
    @_spi(Syntax) public let error: GesSchemaDiagnostic?
}
