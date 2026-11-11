// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

// Checks a read document against a schema on the document's nodes, so errors keep their positions. It needs no VM; explicit
// forms construct through document reading.
struct GesSchemaDocumentCheck {
    private static let missingField = "document.missingField"
    private static let unknownField = "document.unknownField"
    private static let fieldType = "document.fieldType"
    private static let outOfRange = "document.outOfRange"

    private struct Failure: Error {
        let code: String
        let offset: Int
    }

    private struct Attempt: Hashable {
        let type: ObjectIdentifier
        let value: GesDocumentNode
    }

    // What each alternative of a union answered, so nested unions stay linear.
    private final class Attempts {
        var holds: [Attempt: Bool] = [:]
    }

    let parameters: GesSchemaParameters?
    private let attempts = Attempts()

    static func check(_ syntax: GesDocumentSyntax, _ schema: GesResolvedDeclaration, _ parameters: GesSchemaParameters?) throws {
        if let parameters { try GesSchemaBinder.checkParameters(schema, parameters) }
        do {
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets)? = syntax.root else { return }
            try Self(parameters: parameters).fields(keys, values, keyOffsets, valueOffsets, start: 0, schema)
        } catch let failure as Failure {
            let (line, column) = syntax.location(failure.offset)
            throw GesSchemaError(code: failure.code, sourceName: "", line: line, column: column, message: "The document breaks its schema (\(failure.code)) at line \(line), column \(column).")
        }
    }

    private func fields(_ keys: [String], _ values: [GesDocumentNode], _ keyOffsets: [Int], _ valueOffsets: [Int], start: Int, _ schema: GesResolvedDeclaration) throws {
        var seen: Set<String> = []
        for (index, key) in keys.enumerated() {
            guard let field = schema.fields.first(where: { $0.name == key }) else { throw Failure(code: Self.unknownField, offset: keyOffsets[index]) }
            seen.insert(field.name)
            let value = Self.resolved(values[index], valueOffsets[index])
            if field.marker == .optional, case .nothing = value { continue }
            try union(field.alternatives, value, valueOffsets[index])
        }
        for field in schema.fields where !seen.contains(field.name) && field.marker != .optional && !field.alternatives.contains(where: { $0.kind == .nothing }) {
            throw Failure(code: Self.missingField, offset: start)
        }
    }

    // The first alternative that holds decides; a single type reports its own error.
    private func union(_ alternatives: [GesResolvedType], _ written: GesDocumentNode, _ start: Int) throws {
        let value = Self.resolved(written, start)
        if alternatives.count == 1 { return try type(alternatives[0], value, start) }
        for alternative in alternatives {
            let attempt = Attempt(type: ObjectIdentifier(alternative), value: value)
            if let holds = attempts.holds[attempt] {
                if holds { return }
                continue
            }
            do {
                try type(alternative, value, start)
                attempts.holds[attempt] = true
                return
            } catch is Failure {
                attempts.holds[attempt] = false
            }
        }
        throw Failure(code: Self.fieldType, offset: start)
    }

    private func type(_ type: GesResolvedType, _ value: GesDocumentNode, _ start: Int) throws {
        func require(_ holds: Bool) throws { if !holds { throw Failure(code: Self.fieldType, offset: start) } }

        switch type.kind {
        case .boolean:
            if case .boolean = value { return }
            try require(false)
        case .number, .percentage, .quantity:
            guard case .number(let number) = value else { return try require(false) }
            let percent = number.kind == .percentage
            try require(percent == (type.kind == .percentage) && number.unit == type.unit)
            try numeric(type.constraints, number.kind == .integer, number.integerValue, number.floatValue, start)
        case .point, .vector:
            guard case .spatial(let point, let x, let y, let z, let unit) = value else { return try require(false) }
            try require(point == (type.kind == .point) && unit == type.unit)
            guard let axes = type.axes else { return }
            for (axis, component) in [("x", x), ("y", y), ("z", z)] {
                if let declared = axes.first(where: { $0.axis == axis }) {
                    try numeric(declared.constraints, false, 0, component, start)
                } else {
                    try require(component == 0)
                }
            }
        case .tag:
            guard case .tag(let name) = value else { return try require(false) }
            try require(type.tags?.contains(name) ?? true)
            if let max = type.max, name.utf8.count > max { throw Failure(code: Self.outOfRange, offset: start) }
        case .text:
            guard case .text(let text) = value else { return try require(false) }
            if let max = type.max, text.utf8.count > max { throw Failure(code: Self.outOfRange, offset: start) }
        case .bytes:
            guard case .bytes(let bytes) = value else { return try require(false) }
            if let max = type.max, bytes.count > max { throw Failure(code: Self.outOfRange, offset: start) }
        case .list:
            guard case .list(let items, let offsets) = value else { return try require(false) }
            if let max = type.max, items.count > max { throw Failure(code: Self.outOfRange, offset: start) }
            if let element = type.element {
                for (index, item) in items.enumerated() { try union(element, item, offsets[index]) }
            }
        case .reference:
            var fields = value
            if case .form("record", let labels, let arguments, _) = value {
                // Labels may come in any order.
                guard let positions = GesLiteralForms.positions("record", labels), let typeIndex = positions.firstIndex(of: 0), let fieldsIndex = positions.firstIndex(of: 1),
                    case .text(let name) = Self.resolved(arguments[typeIndex], start), name == type.referenceName
                else { return try require(false) }
                fields = arguments[fieldsIndex]
            }
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets) = Self.resolved(fields, start) else { return try require(false) }
            try self.fields(keys, values, keyOffsets, valueOffsets, start: start, type.reference!)
        case .map:
            if case .map = value { return }
            try require(false)
        case .range:
            if case .form("range", _, _, _) = value { return }
            try require(false)
        case .series:
            if case .form("series", _, _, _) = value { return }
            try require(false)
        case .dice:
            guard case .dice(let rolls) = value else { return try require(false) }
            if let max = type.max, rolls.count > max { throw Failure(code: Self.outOfRange, offset: start) }
            for roll in rolls { try numeric(type.constraints, true, Int64(roll), Double(roll), start) }
        case .message:
            if case .message = value { return }
            if case .form("message", _, _, _) = value { return }
            try require(false)
        case .handler:
            if case .handler = value { return }
            if case .form("handler", _, _, _) = value { return }
            try require(false)
        case .nothing:
            if case .nothing = value { return }
            try require(false)
        }
    }

    // A form counts as the value it constructs; these five the check accepts by their type.
    private static func resolved(_ value: GesDocumentNode, _ start: Int) -> GesDocumentNode {
        guard case .form(let type, _, _, _) = value, !["record", "range", "series", "message", "handler"].contains(type) else { return value }
        return GesDocumentReading.resolve(value, start: start)
    }

    private func numeric(_ constraints: GesResolvedConstraints, _ written: Bool, _ writtenInteger: Int64, _ number: Double, _ start: Int) throws {
        var isInteger = written
        var integer = writtenInteger
        if constraints.integer {
            if !isInteger {
                guard number == number.rounded(.down), number < 9_223_372_036_854_775_808, number >= -9_223_372_036_854_775_808 else { throw Failure(code: Self.fieldType, offset: start) }
                integer = Int64(number)
            }
            isInteger = true
        }
        if constraints.wrapping {
            if !isInteger && !number.isFinite { throw Failure(code: Self.outOfRange, offset: start) }
            return
        }
        if let lower = bound(constraints.lower), isInteger ? GesSchemaBinder.compare(GesSchemaDecimal(integer, 0), lower) < 0 : number < Double(lower.canonical)! {
            throw Failure(code: Self.outOfRange, offset: start)
        }
        if let upper = bound(constraints.upper), isInteger ? GesSchemaBinder.compare(GesSchemaDecimal(integer, 0), upper) > 0 : number > Double(upper.canonical)! {
            throw Failure(code: Self.outOfRange, offset: start)
        }
    }

    // A bound whose parameter has no value is not checked.
    private func bound(_ value: GesResolvedValue?) -> GesSchemaDecimal? {
        guard let value else { return nil }
        return value.isParameter ? parameters?.value(value.parameter)?.number : value.value
    }
}
