// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

// Quantity units belong to the Syntax module; schema parameters name them.
@_exported import enum GameEventScriptSyntax.GesQuantityUnit

private typealias Code = GesSchemaDiagnostic

/// A named schema source text.
public struct GesSchemaSource: Sendable {
    /// The name that errors report, such as a file name.
    public let name: String
    /// The source text.
    public let text: String

    /// Creates a source.
    public init(name: String, text: String) {
        self.name = name
        self.text = text
    }
}

/// Validated schema sources. A set is immutable and can be bound any number of times, also with different parameter
/// values within one session.
public final class GesSchemaSet: @unchecked Sendable {
    private static let maximumNesting = 32
    // A Flags field holds its members in one 64-bit mask.
    static let maximumFlagCount = 64

    private var declarations: [String: GesResolvedDeclaration] = [:]
    private var ordered: [GesResolvedDeclaration] = []

    /// The sources as given, which generated code embeds to convert to and from GES values.
    @_spi(Generator) public let sources: [GesSchemaSource]

    /// Reads and validates one or several sources. References resolve across all of them.
    /// - Throws: `GesSchemaError` for the first rule a source breaks.
    public init(sources: [GesSchemaSource]) throws {
        self.sources = sources
        var files: [GesFileScope] = []
        for source in sources {
            let syntax = GesSchemaSourceReader.read(source.text)
            if let error = syntax.error { throw GesSchemaError(code: error.code, sourceName: source.name, line: error.line, column: error.column, message: error.message) }
            files.append(GesFileScope(source, syntax))
        }
        var tagSets: [String: GesSchemaDeclarationSyntax] = [:]
        for file in files { try file.declareNames(&declarations, &ordered, &tagSets) }
        for file in files { try file.resolve(declarations, tagSets) }
        var visited: [ObjectIdentifier: Int] = [:]
        for declaration in ordered {
            var path: [GesResolvedDeclaration] = []
            try Self.checkNesting(declaration, &path, &visited)
        }
        for declaration in ordered where declaration.syntax.wireGroup != nil { try GesSchemaBinder.checkEncodable(declaration) }
        schemas = ordered.map { declaration in
            let schema = GesSchema(declaration)
            declaration.schema = schema
            return schema
        }
        for schema in schemas { try schema.complete() }
    }

    /// Reads and validates one source named `schema.gess`.
    /// - Throws: `GesSchemaError` for the first rule the source breaks.
    public convenience init(parsing text: String) throws { try self.init(sources: [GesSchemaSource(name: "schema.gess", text: text)]) }

    /// The schemas, messages and message sets, in source order.
    public private(set) var schemas: [GesSchema] = []

    /// A declaration by name.
    /// - Throws: `GesSchemaError` when the name is not declared.
    public func schema(_ name: String) throws -> GesSchema { try declaration(name).schema! }

    /// Checks a GES document against the schema of its header: a type name, or for a file name the one schema of the source
    /// with that name that no other schema of it refers to. `GesSchema.checkDocument` ignores the header.
    /// - Parameters:
    ///   - text: The document text.
    ///   - parameters: Parameter values; a bound whose parameter has no value is not checked.
    /// - Throws: `GesSchemaError` with a `document.*` code and its position, or a `schema.*` code when no schema is
    ///   determined.
    public func checkDocument(_ text: String, parameters: GesSchemaParameters? = nil) throws {
        let syntax = try Self.readDocument(text)
        let schema: GesResolvedDeclaration
        switch syntax.schema {
        case .type(let name): schema = try declaration(name)
        case .file(let source): schema = try rootSchema(source)
        case .none: throw GesSchemaError(code: Code.unknownName, sourceName: "", line: 0, column: 0, message: "The document names no schema.")
        }
        try Self.check(syntax, schema, parameters)
    }

    static func readDocument(_ text: String) throws -> GesDocumentSyntax {
        do {
            // Reads the values too, so that a form that constructs nothing is an error here as well.
            _ = try GesDocumentReading.read(text)
        } catch let error as GesDocumentError {
            throw GesSchemaError(code: error.code, sourceName: "", line: error.line, column: error.column, message: GesDocumentDiagnostic.explain(error.code, limitName: error.limitName, limitValue: error.limit ?? 0))
        }
        return GesDocumentReader.read(text)
    }

    static func check(_ syntax: GesDocumentSyntax, _ schema: GesResolvedDeclaration, _ parameters: GesSchemaParameters?) throws {
        if schema.kind != .schema { throw GesSchemaError(code: Code.unknownName, sourceName: "", line: 0, column: 0, message: "'\(schema.name)' is no schema.") }
        try GesSchemaDocumentCheck.check(syntax, schema, parameters)
    }

    // The one schema of a source that no other schema of it refers to.
    private func rootSchema(_ source: String) throws -> GesResolvedDeclaration {
        var referenced: Set<ObjectIdentifier> = []
        for declaration in ordered where declaration.scope.sourceName == source && declaration.kind == .schema {
            for field in declaration.fields {
                for type in field.alternatives { Self.collectReferences(type, &referenced) }
            }
        }
        var root: GesResolvedDeclaration?
        for declaration in ordered where declaration.scope.sourceName == source && declaration.kind == .schema && !referenced.contains(ObjectIdentifier(declaration)) {
            if root != nil { throw GesSchemaError(code: Code.unknownName, sourceName: "", line: 0, column: 0, message: "The schema source '\(source)' has more than one root schema.") }
            root = declaration
        }
        guard let root else { throw GesSchemaError(code: Code.unknownName, sourceName: "", line: 0, column: 0, message: "The schema source '\(source)' has no root schema.") }
        return root
    }

    private static func collectReferences(_ type: GesResolvedType, _ referenced: inout Set<ObjectIdentifier>) {
        if let reference = type.reference { referenced.insert(ObjectIdentifier(reference)) }
        for alternative in type.element ?? [] { collectReferences(alternative, &referenced) }
    }

    func declaration(_ root: String) throws -> GesResolvedDeclaration {
        guard let found = declarations[root] else { throw GesSchemaError(code: Code.unknownName, sourceName: "", line: 0, column: 0, message: "'\(root)' is not declared.") }
        return found
    }

    // Reference cycles and too deep nesting, along every path from the declaration.
    // Reference cycles and too deep nesting. A declaration already finished at this depth or deeper holds no error, so
    // the check stays polynomial however often schemas refer to each other.
    private static func checkNesting(_ declaration: GesResolvedDeclaration, _ path: inout [GesResolvedDeclaration], _ visited: inout [ObjectIdentifier: Int]) throws {
        if declaration.kind != .schema { return }
        if path.contains(where: { $0 === declaration }) { throw declaration.scope.error(Code.cycle, declaration.offset, "Schema '\(declaration.name)' refers to itself.") }
        if path.count >= maximumNesting { throw declaration.scope.error(Code.cycle, declaration.offset, "Schema '\(declaration.name)' is nested more than \(maximumNesting) levels deep.") }
        if let depth = visited[ObjectIdentifier(declaration)], depth >= path.count { return }
        visited[ObjectIdentifier(declaration)] = path.count
        path.append(declaration)
        for field in declaration.fields {
            for type in field.alternatives { try checkNesting(type, &path, &visited) }
        }
        path.removeLast()
    }

    private static func checkNesting(_ type: GesResolvedType, _ path: inout [GesResolvedDeclaration], _ visited: inout [ObjectIdentifier: Int]) throws {
        if let reference = type.reference { try checkNesting(reference, &path, &visited) }
        for alternative in type.element ?? [] { try checkNesting(alternative, &path, &visited) }
    }
}

/// A bound, step or delta range after constants are replaced: a number or a parameter.
struct GesResolvedValue {
    var value = GesSchemaDecimal(0, 0)
    var parameter = ""
    var offset = 0

    var isParameter: Bool { !parameter.isEmpty }
}

/// A constant of a schema: its kind, Number, Percentage or Quantity, its unit and its exact value.
struct GesResolvedLet {
    var name = ""
    var kind = GesSchemaTypeKind.number
    var unit = GesQuantityUnit.none
    var value = GesSchemaDecimal(0, 0)
    /// A whole unitless value within Int64; any other is a binary64 in base units.
    var integer = false
    var offset = 0
}

struct GesResolvedConstraints {
    var integer = false
    var wrapping = false
    var lower: GesResolvedValue?
    var upper: GesResolvedValue?
    var step: GesResolvedValue?
    var delta: GesResolvedValue?
}

struct GesResolvedAxis {
    let axis: String
    let constraints: GesResolvedConstraints
}

final class GesResolvedType {
    let kind: GesSchemaTypeKind
    let unit: GesQuantityUnit
    let tags: [String]?
    let max: Int64?
    let element: [GesResolvedType]?
    let constraints: GesResolvedConstraints
    let axes: [GesResolvedAxis]?
    let reference: GesResolvedDeclaration?
    let referenceName: String
    // The named tag set a Tag takes its members from, or empty.
    let tagSet: String
    let offset: Int
    // The label of a union alternative, or empty.
    var label = ""

    init(
        kind: GesSchemaTypeKind,
        unit: GesQuantityUnit,
        tags: [String]?,
        max: Int64?,
        element: [GesResolvedType]?,
        constraints: GesResolvedConstraints,
        axes: [GesResolvedAxis]?,
        reference: GesResolvedDeclaration?,
        referenceName: String,
        tagSet: String = "",
        offset: Int
    ) {
        self.kind = kind
        self.unit = unit
        self.tags = tags
        self.max = max
        self.element = element
        self.constraints = constraints
        self.axes = axes
        self.reference = reference
        self.referenceName = referenceName
        self.tagSet = tagSet
        self.offset = offset
    }
}

struct GesResolvedField {
    let name: String
    let unlabeled: Bool
    let alternatives: [GesResolvedType]
    let marker: GesSchemaFieldMarker
    let offset: Int
}

final class GesResolvedDeclaration {
    let kind: GesSchemaKind
    let name: String
    let offset: Int
    let scope: GesFileScope
    let syntax: GesSchemaDeclarationSyntax
    let tags: [String]
    var fields: [GesResolvedField] = []
    var lets: [GesResolvedLet] = []
    var members: [GesResolvedDeclaration] = []
    // The public description, which exists once all declarations are resolved. It holds this declaration, so this side is weak.
    weak var schema: GesSchema?

    init(kind: GesSchemaKind, name: String, offset: Int, scope: GesFileScope, syntax: GesSchemaDeclarationSyntax, tags: [String]) {
        self.kind = kind
        self.name = name
        self.offset = offset
        self.scope = scope
        self.syntax = syntax
        self.tags = tags
    }
}

/// One source: its constants and parameters, which only its own declarations see.
final class GesFileScope {
    private let source: GesSchemaSource
    private let syntax: GesSchemaSourceSyntax
    private var constants: [String: GesSchemaValueSyntax] = [:]
    private var own: [GesResolvedDeclaration] = []
    private var tagSets: [String: GesSchemaDeclarationSyntax] = [:]
    /// The lets of the schema being resolved, which its fields can name.
    private var lets: [String: GesResolvedLet] = [:]
    private(set) var parameters: [String: GesSchemaTypeSyntax] = [:]
    var sourceName: String { source.name }

    init(_ source: GesSchemaSource, _ syntax: GesSchemaSourceSyntax) {
        self.source = source
        self.syntax = syntax
    }

    func error(_ code: String, _ offset: Int, _ message: String) -> GesSchemaError {
        let (line, column) = GesDocumentSyntax.location(syntax.bytes, offset)
        return GesSchemaError(code: code, sourceName: source.name, line: line, column: column, message: message)
    }

    func declareNames(_ all: inout [String: GesResolvedDeclaration], _ ordered: inout [GesResolvedDeclaration], _ tagSets: inout [String: GesSchemaDeclarationSyntax]) throws {
        for declaration in syntax.declarations {
            switch declaration.kind {
            case .tagSet:
                if GesSchemaTypeNames.isBuiltIn(declaration.name) {
                    throw error(Code.duplicateName, declaration.offset, "'\(declaration.name)' is a built-in type.")
                }
                if all[declaration.name] != nil || tagSets[declaration.name] != nil {
                    throw error(Code.duplicateName, declaration.offset, "'\(declaration.name)' is declared twice.")
                }
                tagSets[declaration.name] = declaration
                var members: Set<String> = []
                for tag in declaration.tags where !members.insert(tag).inserted {
                    throw error(Code.duplicateName, declaration.offset, "Tag '#\(tag)' is listed twice.")
                }
            case .constant:
                if constants[declaration.name] != nil { throw error(Code.duplicateName, declaration.offset, "Constant '\(declaration.name)' is declared twice.") }
                constants[declaration.name] = declaration.value
            case .parameter:
                if parameters[declaration.name] != nil { throw error(Code.duplicateName, declaration.offset, "Parameter '\(declaration.name)' is declared twice.") }
                parameters[declaration.name] = declaration.type
            default:
                let kind: GesSchemaKind = declaration.kind == .schema ? .schema : declaration.kind == .message ? .message : .messages
                let resolved = GesResolvedDeclaration(kind: kind, name: declaration.name, offset: declaration.offset, scope: self, syntax: declaration, tags: declaration.tags)
                if GesSchemaTypeNames.isBuiltIn(declaration.name) {
                    throw error(Code.duplicateName, declaration.offset, "'\(declaration.name)' is a built-in type.")
                }
                if all[declaration.name] != nil || tagSets[declaration.name] != nil {
                    throw error(Code.duplicateName, declaration.offset, "'\(declaration.name)' is declared twice.")
                }
                all[declaration.name] = resolved
                ordered.append(resolved)
                own.append(resolved)
            }
        }
    }

    func resolve(_ all: [String: GesResolvedDeclaration], _ tagSets: [String: GesSchemaDeclarationSyntax]) throws {
        self.tagSets = tagSets
        for declaration in own {
            let syntax = declaration.syntax
            if declaration.kind == .messages {
                var seen: Set<String> = []
                for (index, name) in syntax.members.enumerated() {
                    if !seen.insert(name).inserted { throw error(Code.duplicateName, syntax.memberOffsets[index], "Message '\(name)' is listed twice.") }
                    guard let member = all[name], member.kind == .message else { throw error(Code.unknownName, syntax.memberOffsets[index], "Message '\(name)' is not declared.") }
                    declaration.members.append(member)
                }
                continue
            }
            var tags: Set<String> = []
            for tag in syntax.tags where !tags.insert(tag).inserted { throw error(Code.duplicateName, syntax.offset, "Tag '#\(tag)' is listed twice.") }
            var names: Set<String> = []
            declaration.lets = try resolveLets(syntax, &names)
            for (index, field) in syntax.fields.enumerated() {
                if !names.insert(field.name).inserted { throw error(Code.duplicateName, field.offset, "'\(field.name)' is declared twice in '\(declaration.name)'.") }
                declaration.fields.append(try resolveField(field, index, declaration.kind, all))
            }
        }
        // A declaration holds its scope, so the scope lets go of them once they are resolved.
        own = []
        self.tagSets = [:]
        lets = [:]
    }

    /// A let is a literal or a file constant. Its name differs from the fields and other lets of its schema and from the
    /// parameters of its file, since a bound names either.
    private func resolveLets(_ syntax: GesSchemaDeclarationSyntax, _ names: inout Set<String>) throws -> [GesResolvedLet] {
        lets = [:]
        var result: [GesResolvedLet] = []
        for declared in syntax.lets {
            if !names.insert(declared.name).inserted { throw error(Code.duplicateName, declared.offset, "'\(declared.name)' is declared twice in '\(syntax.name)'.") }
            if parameters[declared.name] != nil { throw error(Code.duplicateName, declared.offset, "'\(declared.name)' is a parameter of this file.") }
            var literal = declared.value
            if !literal.constant.isEmpty {
                guard let constant = constants[literal.constant] else {
                    throw error(Code.unknownName, declared.value.offset, "Constant '$\(declared.value.constant)' is not declared in this file.")
                }
                literal = constant
            }
            let kind: GesSchemaTypeKind = literal.isPercent ? .percentage : literal.unit != .none ? .quantity : .number
            var resolved = GesResolvedLet(name: declared.name, kind: kind, unit: literal.unit, value: literal.value, offset: declared.offset)
            resolved.integer = try GesSchemaBinder.checkLet(self, resolved)
            result.append(resolved)
            lets[declared.name] = resolved
        }
        return result
    }

    private func resolveField(_ field: GesSchemaFieldSyntax, _ index: Int, _ owner: GesSchemaKind, _ all: [String: GesResolvedDeclaration]) throws -> GesResolvedField {
        let alternatives = try resolveUnion(field.alternatives, all)
        if field.marker == .key {
            let first = alternatives[0]
            let keyable = first.constraints.integer || (first.kind == .tag && first.tags != nil)
            if owner != .schema || index != 0 || alternatives.count != 1 || !keyable || first.constraints.delta != nil {
                throw error(Code.invalidKey, field.offset, "A key is the first field of a schema, an integer or a Tag with a set, without delta within or a union.")
            }
        }
        if field.marker == .optional && alternatives.count > 1 && alternatives.contains(where: { $0.kind == .nothing }) {
            throw error(Code.invalidUnion, field.offset, "A union with Nothing already holds an absent value; it is not optional as well.")
        }
        return GesResolvedField(name: field.name, unlabeled: field.unlabeled, alternatives: alternatives, marker: field.marker, offset: field.offset)
    }

    private func resolveUnion(_ alternatives: [GesSchemaTypeSyntax], _ all: [String: GesResolvedDeclaration]) throws -> [GesResolvedType] {
        var result: [GesResolvedType] = []
        var seen: Set<String> = []
        var labels: Set<String> = []
        for alternative in alternatives {
            let resolved = try resolveType(alternative, all)
            result.append(resolved)
            if !alternative.label.isEmpty {
                if alternatives.count == 1 { throw error(Code.invalidUnion, alternative.labelOffset, "A label names an alternative of a union.") }
                if resolved.kind == .nothing { throw error(Code.invalidUnion, alternative.labelOffset, "A :Nothing alternative holds no value to label.") }
                resolved.label = alternative.label
            }
            if alternatives.count == 1 { continue }
            // The type name decides: a unit does not distinguish alternatives, and a named tag set is a Tag.
            let name = resolved.kind == .reference ? ":" + alternative.name : resolved.kind.rawValue
            if !seen.insert(name).inserted { throw error(Code.invalidUnion, alternative.offset, "A union repeats a type name; a unit does not distinguish alternatives.") }
            if alternative.constraints.delta != nil { throw error(Code.invalidUnion, alternative.offset, "A union alternative has no delta within.") }
            // Generated code names an alternative after its label or its type, so both must stay apart.
            var label = resolved.kind == .reference ? alternative.name : resolved.kind.rawValue
            if !alternative.label.isEmpty { label = alternative.label.prefix(1).uppercased() + alternative.label.dropFirst() }
            if !labels.insert(label).inserted {
                throw error(Code.duplicateName, alternative.label.isEmpty ? alternative.offset : alternative.labelOffset, "Two alternatives are both named '\(label)'.")
            }
        }
        return result
    }

    private func resolveType(_ type: GesSchemaTypeSyntax, _ all: [String: GesResolvedDeclaration]) throws -> GesResolvedType {
        // A named tag set stands for a Tag with its members; only its name differs, which belongs to no hash.
        if type.kind == .reference, let tagSet = tagSets[type.name] {
            return GesResolvedType(
                kind: .tag,
                unit: .none,
                tags: tagSet.tags,
                max: nil,
                element: nil,
                constraints: GesResolvedConstraints(),
                axes: nil,
                reference: nil,
                referenceName: "",
                tagSet: tagSet.name,
                offset: type.offset
            )
        }
        var reference: GesResolvedDeclaration?
        if type.kind == .reference {
            guard let found = all[type.name], found.kind == .schema else { throw error(Code.unknownName, type.offset, "Schema '\(type.name)' is not declared.") }
            reference = found
        }
        if let tags = type.tags {
            var seen: Set<String> = []
            for tag in tags where !seen.insert(tag).inserted { throw error(Code.duplicateName, type.offset, "Tag '#\(tag)' is listed twice.") }
            if type.kind == .flags && tags.count > GesSchemaSet.maximumFlagCount { throw error(Code.tooWide, type.offset, "Flags have at most \(GesSchemaSet.maximumFlagCount) members.") }
        }
        let max = type.maxName.isEmpty ? type.max : try letMax(type)
        if let max, max <= 0 { throw error(Code.invalidConstraint, type.offset, "A maximum is positive.") }
        if type.kind == .dice && (type.constraints.integer || type.constraints.wrapping || type.constraints.step != nil || type.constraints.delta != nil) {
            throw error(Code.invalidConstraint, type.offset, "A Dice has only a range.")
        }
        var axes: [GesResolvedAxis]?
        if let written = type.axes {
            var seen: Set<String> = []
            var list: [GesResolvedAxis] = []
            for axis in written {
                if !seen.insert(axis.axis).inserted { throw error(Code.duplicateName, axis.offset, "Axis '\(axis.axis)' is declared twice.") }
                list.append(GesResolvedAxis(axis: axis.axis, constraints: try resolveConstraints(axis.constraints, type.unit, false, axis.offset)))
            }
            axes = list.sorted { $0.axis < $1.axis }
        }
        var element: [GesResolvedType]?
        if let written = type.element { element = try resolveUnion(written, all) }
        let constraints = try resolveConstraints(type.constraints, type.unit, type.kind == .percentage, type.offset)
        if type.kind == .dice { try GesSchemaBinder.checkDice(self, constraints, type.offset, nil) }
        return GesResolvedType(
            kind: type.kind,
            unit: type.unit,
            tags: type.tags,
            max: max,
            element: element,
            constraints: constraints,
            axes: axes,
            reference: reference,
            referenceName: type.name,
            offset: type.offset
        )
    }

    /// A maximum that names a let takes its whole unitless value, which has at most 12 digits like a written one.
    private func letMax(_ type: GesSchemaTypeSyntax) throws -> Int64 {
        guard let declared = lets[type.maxName] else { throw error(Code.unknownName, type.maxOffset, "'\(type.maxName)' is no let of this schema.") }
        if declared.kind != .number { throw error(Code.unitMismatch, type.maxOffset, "Let '\(type.maxName)' has a unit; a maximum is a whole number.") }
        let significand = declared.value.significand
        if !declared.integer || significand <= 0 || declared.value.exponent + String(significand).count > 12 {
            throw error(Code.invalidConstraint, type.maxOffset, "Let '\(type.maxName)' is no positive whole number of at most 12 digits.")
        }
        var value = significand
        for _ in 0..<declared.value.exponent { value *= 10 }
        return value
    }

    private func resolveConstraints(_ constraints: GesSchemaConstraintsSyntax, _ unit: GesQuantityUnit, _ percentage: Bool, _ offset: Int) throws -> GesResolvedConstraints {
        if constraints.integer && percentage { throw error(Code.invalidConstraint, offset, "A Percentage is not an integer.") }
        if constraints.integer && constraints.step != nil { throw error(Code.invalidConstraint, offset, "An integer field has no step.") }
        if constraints.step != nil && constraints.lower == nil { throw error(Code.invalidConstraint, offset, "A step requires a range.") }
        if constraints.delta != nil && constraints.lower == nil { throw error(Code.invalidConstraint, offset, "A delta range requires a range.") }
        if constraints.delta != nil && !constraints.integer && constraints.step == nil { throw error(Code.invalidConstraint, offset, "A delta range requires a step or integer.") }
        var resolved = GesResolvedConstraints()
        resolved.integer = constraints.integer
        resolved.wrapping = constraints.wrapping
        resolved.lower = try resolveValue(constraints.lower, unit, percentage, bound: true)
        resolved.upper = try resolveValue(constraints.upper, unit, percentage, bound: true)
        resolved.step = try resolveValue(constraints.step, unit, percentage, bound: false)
        resolved.delta = try resolveValue(constraints.delta, unit, percentage, bound: false)
        try GesSchemaBinder.checkLiterals(self, resolved)
        return resolved
    }

    /// A name is a let of the schema and, only in a bound, a parameter of the file; a let is replaced by its value.
    private func resolveValue(_ value: GesSchemaValueSyntax?, _ unit: GesQuantityUnit, _ percentage: Bool, bound: Bool) throws -> GesResolvedValue? {
        guard let value else { return nil }
        if !value.name.isEmpty {
            if let declared = lets[value.name] {
                if declared.unit != unit || (declared.kind == .percentage) != percentage {
                    throw error(Code.unitMismatch, value.offset, "Let '\(value.name)' has another kind or unit than its field.")
                }
                return GesResolvedValue(value: declared.value, offset: value.offset)
            }
            if !bound { throw error(Code.unknownName, value.offset, "'\(value.name)' is no let of this schema; a step and a delta range never name a parameter.") }
            guard let parameter = parameters[value.name] else { throw error(Code.unknownName, value.offset, "'\(value.name)' is neither a let of this schema nor a parameter of this file.") }
            if parameter.unit != unit || (parameter.kind == .percentage) != percentage { throw error(Code.unitMismatch, value.offset, "Parameter '\(value.name)' has another kind or unit than its field.") }
            return GesResolvedValue(parameter: value.name, offset: value.offset)
        }
        var literal = value
        if !value.constant.isEmpty {
            guard let constant = constants[value.constant] else { throw error(Code.unknownName, value.offset, "Constant '$\(value.constant)' is not declared in this file.") }
            literal = constant
        }
        if literal.unit != unit || literal.isPercent != percentage { throw error(Code.unitMismatch, value.offset, "A value has another unit than its field.") }
        return GesResolvedValue(value: literal.value, offset: value.offset)
    }
}

/// The canonical form of a declaration and its hash.
enum GesSchemaCanonical {
    static func hash(_ text: String) -> UInt64 {
        var hash: UInt64 = 14_695_981_039_346_656_037
        for byte in text.utf8 { hash = (hash ^ UInt64(byte)) &* 1_099_511_628_211 }
        return hash
    }

    static func hex(_ hash: UInt64) -> String {
        let digits = String(hash, radix: 16)
        return String(repeating: "0", count: 16 - digits.count) + digits
    }

    static func form(_ root: GesResolvedDeclaration) throws -> String {
        var declarations: [GesResolvedDeclaration] = []
        var parameters: [String: GesSchemaTypeSyntax] = [:]
        try reach(root, &declarations, &parameters)
        var output = "gess1\n"
        for name in parameters.keys.sorted(by: { Array($0.utf8).lexicographicallyPrecedes(Array($1.utf8)) }) { output += "parameter \(name) \(parameterKind(parameters[name]!))\n" }
        for declaration in declarations { write(declaration, &output) }
        return output
    }

    static func reach(_ declaration: GesResolvedDeclaration, _ declarations: inout [GesResolvedDeclaration], _ parameters: inout [String: GesSchemaTypeSyntax]) throws {
        var seen: Set<ObjectIdentifier> = []
        try reach(declaration, &seen, &declarations, &parameters)
    }

    private static func reach(_ declaration: GesResolvedDeclaration, _ seen: inout Set<ObjectIdentifier>, _ declarations: inout [GesResolvedDeclaration], _ parameters: inout [String: GesSchemaTypeSyntax]) throws {
        if !seen.insert(ObjectIdentifier(declaration)).inserted { return }
        declarations.append(declaration)
        for field in declaration.fields {
            for type in field.alternatives { try reach(type, declaration.scope, &seen, &declarations, &parameters) }
        }
        for member in declaration.members { try reach(member, &seen, &declarations, &parameters) }
    }

    private static func reach(_ type: GesResolvedType, _ scope: GesFileScope, _ seen: inout Set<ObjectIdentifier>, _ declarations: inout [GesResolvedDeclaration], _ parameters: inout [String: GesSchemaTypeSyntax]) throws {
        try reachValues(type.constraints, scope, &parameters)
        for axis in type.axes ?? [] { try reachValues(axis.constraints, scope, &parameters) }
        for alternative in type.element ?? [] { try reach(alternative, scope, &seen, &declarations, &parameters) }
        if let reference = type.reference { try reach(reference, &seen, &declarations, &parameters) }
    }

    private static func reachValues(_ constraints: GesResolvedConstraints, _ scope: GesFileScope, _ parameters: inout [String: GesSchemaTypeSyntax]) throws {
        for value in [constraints.lower, constraints.upper] {
            guard let value, value.isParameter, let declared = scope.parameters[value.parameter] else { continue }
            if let other = parameters[value.parameter], other.kind != declared.kind || other.unit != declared.unit {
                throw scope.error(Code.duplicateName, value.offset, "Parameter '\(value.parameter)' has another kind or unit in another file.")
            }
            parameters[value.parameter] = declared
        }
    }

    static func parameterKind(_ type: GesSchemaTypeSyntax) -> String {
        switch type.kind {
        case .percentage: "Percentage"
        case .quantity: "Quantity(\(type.unit.rawValue))"
        default: "Number"
        }
    }

    static func letKind(_ declared: GesResolvedLet) -> String {
        switch declared.kind {
        case .percentage: "Percentage"
        case .quantity: "Quantity(\(declared.unit.rawValue))"
        default: "Number"
        }
    }

    private static func write(_ declaration: GesResolvedDeclaration, _ output: inout String) {
        switch declaration.kind {
        case .schema:
            output += "schema \(declaration.name)\n"
            for declared in declaration.lets { output += "let \(declared.name) \(letKind(declared)) \(declared.value.canonical)\n" }
            for field in declaration.fields { writeField("field ", field, &output) }
        case .message:
            output += "message \(declaration.name)"
            if !declaration.tags.isEmpty { output += " tags " + declaration.tags.map { "#" + $0 }.joined(separator: " ") }
            output += "\n"
            for field in declaration.fields { writeField("argument ", field, &output) }
        case .messages:
            output += "messages \(declaration.name)\n"
            for member in declaration.members { output += "member \(member.name)\n" }
        }
    }

    private static func writeField(_ prefix: String, _ field: GesResolvedField, _ output: inout String) {
        output += prefix
        if field.unlabeled { output += "_ " }
        output += field.name + " "
        writeUnion(field.alternatives, &output)
        if field.marker == .optional { output += " optional" } else if field.marker == .key { output += " key" }
        output += "\n"
    }

    private static func writeUnion(_ alternatives: [GesResolvedType], _ output: inout String) {
        for (index, alternative) in alternatives.enumerated() {
            if index > 0 { output += " | " }
            writeType(alternative, &output)
        }
    }

    private static func writeType(_ type: GesResolvedType, _ output: inout String) {
        switch type.kind {
        case .quantity:
            output += "Quantity(\(type.unit.rawValue))"
            writeConstraints(type.constraints, &output)
        case .number, .percentage:
            output += type.kind.rawValue
            writeConstraints(type.constraints, &output)
        case .point, .vector:
            output += "\(type.kind.rawValue)(\(type.unit == .none ? "none" : type.unit.rawValue))"
            if let axes = type.axes {
                output += " {"
                for (index, axis) in axes.enumerated() {
                    if index > 0 { output += " ;" }
                    output += " " + axis.axis
                    writeConstraints(axis.constraints, &output)
                }
                output += " }"
            }
        case .tag:
            output += "Tag"
            if let tags = type.tags { output += "(" + tags.map { "#" + $0 }.joined(separator: " ") + ")" }
            if let max = type.max { output += " max \(max)" }
        case .flags:
            output += "Flags(" + (type.tags ?? []).map { "#" + $0 }.joined(separator: " ") + ")"
        case .text, .bytes:
            output += type.kind.rawValue
            if let max = type.max { output += " max \(max)" }
        case .dice:
            output += "Dice"
            if let max = type.max { output += " max \(max)" }
            writeConstraints(type.constraints, &output)
        case .list:
            output += "List"
            if let element = type.element {
                output += "("
                writeUnion(element, &output)
                output += ")"
            }
            if let max = type.max { output += " max \(max)" }
        case .reference: output += type.referenceName
        default: output += type.kind.rawValue
        }
    }

    private static func writeConstraints(_ constraints: GesResolvedConstraints, _ output: inout String) {
        if constraints.integer { output += " integer" }
        if let lower = constraints.lower, let upper = constraints.upper {
            output += constraints.wrapping ? " wrapping between " : " between "
            output += "\(value(lower)) \(value(upper))"
        }
        if let step = constraints.step { output += " step \(value(step))" }
        if let delta = constraints.delta { output += " delta \(value(delta))" }
    }

    private static func value(_ value: GesResolvedValue) -> String { value.isParameter ? value.parameter : value.value.canonical }
}
