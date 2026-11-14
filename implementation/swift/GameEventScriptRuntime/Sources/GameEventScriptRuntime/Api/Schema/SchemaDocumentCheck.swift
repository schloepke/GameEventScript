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
    private static let tooDeep = "document.tooDeep"

    private struct Failure: Error {
        let code: String
        let offset: Int
        // The value path of the field that fails, as the value path and generated code name it, such as `units[].name`.
        let path: String
    }

    private struct Attempt: Hashable {
        let type: ObjectIdentifier
        let value: GesDocumentNode
        // Equal values at other positions are checked again, so that each records the alternatives of its own unions.
        let start: Int
    }

    // A union of several alternatives at the position of its value, whose held alternative converting the checked document
    // takes rather than the first one its shape fits.
    struct Choice: Hashable {
        let start: Int
        let alternatives: [ObjectIdentifier]

        init(_ start: Int, _ alternatives: [GesResolvedType]) {
            self.start = start
            self.alternatives = alternatives.map(ObjectIdentifier.init)
        }
    }

    // What each alternative of a union answered, so nested unions stay linear, and how many records of each schema enclose
    // the value being checked, which a recursive List counts against its depth.
    private final class Attempts {
        var holds: [Attempt: Bool] = [:]
        var chosen: [Choice: GesResolvedType] = [:]
        var levels: [ObjectIdentifier: Int] = [:]
        // The path of the value being checked.
        var path = ""
    }

    let parameters: GesSchemaParameters?
    private let attempts = Attempts()

    static func check(_ syntax: GesDocumentSyntax, _ schema: GesResolvedDeclaration, _ parameters: GesSchemaParameters?) throws {
        if let failure = try failing(syntax, schema, parameters) {
            throw GesSchemaError(
                code: failure.code,
                sourceName: "",
                line: failure.line,
                column: failure.column,
                message: "The document breaks its schema (\(failure.code)) at line \(failure.line), column \(failure.column)."
            )
        }
    }

    // The first error of a document against its schema, with its position and field path, or nil.
    static func failing(_ syntax: GesDocumentSyntax, _ schema: GesResolvedDeclaration, _ parameters: GesSchemaParameters?) throws -> GesDocumentError? {
        try choosing(syntax, schema, parameters).failure
    }

    // The first error, or nil with the alternative that held for each union of several.
    static func choosing(_ syntax: GesDocumentSyntax, _ schema: GesResolvedDeclaration, _ parameters: GesSchemaParameters?) throws -> (failure: GesDocumentError?, chosen: [Choice: GesResolvedType]) {
        if let parameters { try GesSchemaBinder.checkParameters(schema, parameters) }
        let check = Self(parameters: parameters)
        do {
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets)? = syntax.root else { return (nil, [:]) }
            try check.fields(keys, values, keyOffsets, valueOffsets, start: 0, schema)
            return (nil, check.attempts.chosen)
        } catch let failure as Failure {
            let (line, column) = syntax.location(failure.offset)
            return (GesDocumentError(code: failure.code, line: line, column: column, path: failure.path), [:])
        }
    }

    private func fail(_ code: String, _ offset: Int, path: String? = nil) -> Failure { Failure(code: code, offset: offset, path: path ?? attempts.path) }

    static func join(_ path: String, _ name: String) -> String { path.isEmpty ? name : path + "." + name }

    // Checks a nested value under its path segment: a field name, or `[]` for an element.
    private func under(_ segment: String, _ check: () throws -> Void) throws {
        let outer = attempts.path
        attempts.path = segment == "[]" ? outer + segment : Self.join(outer, segment)
        defer { attempts.path = outer }
        try check()
    }

    private func fields(_ keys: [String], _ values: [GesDocumentNode], _ keyOffsets: [Int], _ valueOffsets: [Int], start: Int, _ schema: GesResolvedDeclaration) throws {
        let level = attempts.levels[ObjectIdentifier(schema)] ?? 0
        attempts.levels[ObjectIdentifier(schema)] = level + 1
        defer { attempts.levels[ObjectIdentifier(schema)] = level == 0 ? nil : level }
        var seen: Set<String> = []
        for (index, key) in keys.enumerated() {
            guard let field = schema.fields.first(where: { GesText.scalarEqual($0.name, key) }) else { throw fail(Self.unknownField, keyOffsets[index], path: Self.join(attempts.path, key)) }
            seen.insert(field.name)
            let value = Self.resolved(values[index], valueOffsets[index])
            if field.marker == .optional, case .nothing = value { continue }
            try under(field.name) { try union(field.alternatives, value, valueOffsets[index]) }
        }
        for field in schema.fields where !seen.contains(field.name) && field.defaultValue == nil && field.marker != .optional && !field.alternatives.contains(where: { $0.kind == .nothing }) {
            throw fail(Self.missingField, start, path: Self.join(attempts.path, field.name))
        }
    }

    // The first alternative that holds decides; a single type reports its own error.
    private func union(_ alternatives: [GesResolvedType], _ written: GesDocumentNode, _ start: Int) throws {
        let value = Self.resolved(written, start)
        if alternatives.count == 1 { return try type(alternatives[0], value, start) }
        for alternative in alternatives {
            let attempt = Attempt(type: ObjectIdentifier(alternative), value: value, start: start)
            if let holds = attempts.holds[attempt] {
                if holds {
                    attempts.chosen[Choice(start, alternatives)] = alternative
                    return
                }
                continue
            }
            do {
                try type(alternative, value, start)
                attempts.holds[attempt] = true
                attempts.chosen[Choice(start, alternatives)] = alternative
                return
            } catch is Failure {
                attempts.holds[attempt] = false
            }
        }
        throw fail(Self.fieldType, start)
    }

    private func type(_ type: GesResolvedType, _ value: GesDocumentNode, _ start: Int) throws {
        func require(_ holds: Bool) throws { if !holds { throw fail(Self.fieldType, start) } }

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
            try spatial(type, value, start, point: type.kind == .point)
        case .box:
            // A document has no geometry literal: a Box is the Map of its corners, as Points, or a Record "Box" of them.
            var fields = value
            if case .form("record", let labels, let arguments, _) = value {
                guard let positions = GesLiteralForms.positions("record", labels), let typeIndex = positions.firstIndex(of: 0), let fieldsIndex = positions.firstIndex(of: 1),
                    case .text(let name) = Self.resolved(arguments[typeIndex], start), name == "Box"
                else { return try require(false) }
                fields = arguments[fieldsIndex]
            }
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets) = Self.resolved(fields, start) else { return try require(false) }
            var seen = 0
            for (index, key) in keys.enumerated() {
                let corner = key == "lower" ? 1 : key == "upper" ? 2 : 0
                if corner == 0 { throw fail(Self.unknownField, keyOffsets[index], path: Self.join(attempts.path, key)) }
                seen |= corner
                try under(key) { try spatial(type, Self.resolved(values[index], valueOffsets[index]), valueOffsets[index], point: true) }
            }
            if seen != 3 { throw fail(Self.missingField, start) }
        case .disc:
            // A Disc is the Map of its centre, a Point, and its radius, a number of the unit, or a Record "Disc" of them.
            var fields = value
            if case .form("record", let labels, let arguments, _) = value {
                guard let positions = GesLiteralForms.positions("record", labels), let typeIndex = positions.firstIndex(of: 0), let fieldsIndex = positions.firstIndex(of: 1),
                    case .text(let name) = Self.resolved(arguments[typeIndex], start), name == "Disc"
                else { return try require(false) }
                fields = arguments[fieldsIndex]
            }
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets) = Self.resolved(fields, start) else { return try require(false) }
            var seen = 0
            for (index, key) in keys.enumerated() {
                let part = key == "center" ? 1 : key == "radius" ? 2 : 0
                if part == 0 { throw fail(Self.unknownField, keyOffsets[index], path: Self.join(attempts.path, key)) }
                seen |= part
                let child = Self.resolved(values[index], valueOffsets[index])
                if part == 1 {
                    try under(key) { try spatial(type, child, valueOffsets[index], point: true) }
                } else {
                    try under(key) { try radius(type, child, valueOffsets[index]) }
                }
            }
            if seen != 3 { throw fail(Self.missingField, start) }
        case .poly:
            // A document has no geometry literal: a Poly is the List of its points.
            guard case .list(let items, let offsets) = value else { return try require(false) }
            if let max = type.max, items.count > max { throw fail(Self.outOfRange, start) }
            for (index, item) in items.enumerated() { try under("[]") { try spatial(type, Self.resolved(item, offsets[index]), offsets[index], point: true) } }
        case .tag:
            guard case .tag(let name) = value else { return try require(false) }
            try require(type.tags?.contains(name) ?? true)
            if let max = type.max, name.utf8.count > max { throw fail(Self.outOfRange, start) }
        case .text:
            guard case .text(let text) = value else { return try require(false) }
            if let max = type.max, text.utf8.count > max { throw fail(Self.outOfRange, start) }
        case .flags:
            // Each key names a member and each value is a Boolean.
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets) = value else { return try require(false) }
            for (index, key) in keys.enumerated() {
                if !(type.tags ?? []).contains(where: { GesText.scalarEqual($0, key) }) { throw fail(Self.fieldType, keyOffsets[index]) }
                guard case .boolean = Self.resolved(values[index], valueOffsets[index]) else { throw fail(Self.fieldType, valueOffsets[index]) }
            }
        case .bytes:
            guard case .bytes(let bytes) = value else { return try require(false) }
            if let max = type.max, bytes.count > max { throw fail(Self.outOfRange, start) }
        case .list:
            guard case .list(let items, let offsets) = value else { return try require(false) }
            if let recursion = type.recursion, !items.isEmpty, (attempts.levels[ObjectIdentifier(recursion)] ?? 0) >= type.depth {
                throw fail(Self.tooDeep, start)
            }
            if let max = type.max, items.count > max { throw fail(Self.outOfRange, start) }
            if let element = type.element {
                for (index, item) in items.enumerated() { try under("[]") { try union(element, item, offsets[index]) } }
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
            if let max = type.max, rolls.count > max { throw fail(Self.outOfRange, start) }
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

    // A radius of the Disc's unit, whose magnitude lies within the block's radius, as a disc takes the magnitude, or zero when
    // the block leaves it out.
    private func radius(_ type: GesResolvedType, _ value: GesDocumentNode, _ start: Int) throws {
        guard case .number(let number) = value, number.kind != .percentage, number.unit == type.unit else { throw fail(Self.fieldType, start) }
        guard let axes = type.axes else { return }
        // Int64.min has no Int64 magnitude, so it is checked as its binary64 magnitude.
        let integer = number.kind == .integer && number.integerValue != .min
        let whole = integer ? number.integerValue.magnitude : 0
        let fraction = (integer || number.kind == .integer ? Double(number.integerValue) : number.floatValue).magnitude
        if let declared = axes.first(where: { $0.axis == "radius" }) {
            try numeric(declared.constraints, integer, Int64(whole), fraction, start)
        } else if integer ? whole != 0 : fraction != 0 {
            throw fail(Self.fieldType, start)
        }
    }

    private func spatial(_ type: GesResolvedType, _ value: GesDocumentNode, _ start: Int, point: Bool) throws {
        guard case .spatial(let isPoint, let x, let y, let z, let unit) = value, isPoint == point, unit == type.unit else { throw fail(Self.fieldType, start) }
        guard let axes = type.axes else {
            // Without a block the points of a Poly and the centre of a Disc lie in the plane z = 0, as with a block that
            // declares no z.
            if (type.kind == .poly || type.kind == .disc) && z != 0 { throw fail(Self.fieldType, start) }
            return
        }
        for (axis, component) in [("x", x), ("y", y), ("z", z)] {
            if let declared = axes.first(where: { $0.axis == axis }) {
                try numeric(declared.constraints, false, 0, component, start)
            } else if component != 0 {
                throw fail(Self.fieldType, start)
            }
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
                guard number == number.rounded(.down), number < 9_223_372_036_854_775_808, number >= -9_223_372_036_854_775_808 else { throw fail(Self.fieldType, start) }
                integer = Int64(number)
            }
            isInteger = true
        }
        if constraints.wrapping {
            if !isInteger && !number.isFinite { throw fail(Self.outOfRange, start) }
            return
        }
        if let lower = bound(constraints.lower), isInteger ? GesSchemaBinder.compare(GesSchemaDecimal(integer, 0), lower) < 0 : number < Double(lower.canonical)! {
            throw fail(Self.outOfRange, start)
        }
        if let upper = bound(constraints.upper), isInteger ? GesSchemaBinder.compare(GesSchemaDecimal(integer, 0), upper) > 0 : number > Double(upper.canonical)! {
            throw fail(Self.outOfRange, start)
        }
    }

    // A bound whose parameter has no value is not checked.
    private func bound(_ value: GesResolvedValue?) -> GesSchemaDecimal? {
        guard let value else { return nil }
        return value.isParameter ? parameters?.value(value.parameter)?.number : value.value
    }
}
