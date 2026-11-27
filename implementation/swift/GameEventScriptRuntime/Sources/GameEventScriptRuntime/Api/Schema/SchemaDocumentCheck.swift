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
    private static let unexpectedVersion = "document.unexpectedVersion"

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

    // A union of several alternatives at the exact path of its value, whose held alternative converting the checked document
    // takes rather than the first one its shape fits. The exact path names each list element by its index, as `items[2].value`,
    // since the values of an explicit form share the form's position.
    struct Choice: Hashable {
        let position: String
        let alternatives: [ObjectIdentifier]

        init(_ position: String, _ alternatives: [GesResolvedType]) {
            self.position = position
            self.alternatives = alternatives.map(ObjectIdentifier.init)
        }
    }

    // A union that chose an alternative for a value at its exact path.
    private struct Made {
        let position: String
        let alternatives: [GesResolvedType]
        let alternative: GesResolvedType
        let attempt: Attempt
    }

    // What each alternative of a union answered, so nested unions stay linear, and how many records of each schema enclose
    // the value being checked, which a recursive List counts against its depth.
    private final class Attempts {
        var holds: [Attempt: Bool] = [:]
        var chosen: [Choice: GesResolvedType] = [:]
        // Every choice in the order it was made, with the alternative it replaced, so that an alternative that fails takes
        // back the choices of the unions inside it.
        var choices: [(union: Choice, replaced: GesResolvedType?)] = []
        // The unions directly inside each alternative that held, which a cached answer repeats. Each records only its
        // direct unions, whose own cached answers hold the deeper ones, so nesting stores every choice once. A value's exact
        // path follows from the document alone, so a repeated choice keeps its recorded position. `direct` collects the
        // direct unions of the alternatives being checked until their alternative answers.
        var made: [Attempt: [Made]] = [:]
        var direct: [Made] = []
        var levels: [ObjectIdentifier: Int] = [:]
        // The fixed List types of fields whose default fills them, which may be shorter.
        var filled: Set<ObjectIdentifier> = []
        // The path of the value being checked, and its exact path with list indices.
        var path = ""
        var position = ""
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

    // The first error of a document against its schema, with its position and field path, or nil. `rootStart` is where an
    // error of the root Map itself points, such as a missing field: a store entry's value rather than the text's start.
    static func failing(_ syntax: GesDocumentSyntax, _ schema: GesResolvedDeclaration, _ parameters: GesSchemaParameters?, rootStart: Int = 0) throws -> GesDocumentError? {
        try choosing(syntax, schema, parameters, rootStart: rootStart).failure
    }

    // The first error, or nil with the alternative that held for each union of several.
    static func choosing(_ syntax: GesDocumentSyntax, _ schema: GesResolvedDeclaration, _ parameters: GesSchemaParameters?, rootStart: Int = 0) throws -> (failure: GesDocumentError?, chosen: [Choice: GesResolvedType]) {
        if let parameters { try GesSchemaBinder.checkParameters(schema, parameters) }
        let check = Self(parameters: parameters)
        do {
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets, _)? = syntax.root else { return (nil, [:]) }
            try check.fields(keys, values, keyOffsets, valueOffsets, start: rootStart, schema)
            return (nil, check.attempts.chosen)
        } catch let failure as Failure {
            let (line, column) = syntax.location(failure.offset)
            return (GesDocumentError(code: failure.code, line: line, column: column, path: failure.path), [:])
        }
    }

    private func fail(_ code: String, _ offset: Int, path: String? = nil) -> Failure { Failure(code: code, offset: offset, path: path ?? attempts.path) }

    static func join(_ path: String, _ name: String) -> String { path.isEmpty ? name : path + "." + name }

    // Checks a nested value under its path segment: a field name, or `[]` for the element at an index or, in a handle Map,
    // for the entry of a key.
    private func under(_ segment: String, index: Int = 0, key: String? = nil, _ check: () throws -> Void) throws {
        let outer = attempts.path
        let outerPosition = attempts.position
        attempts.path = segment == "[]" ? outer + segment : Self.join(outer, segment)
        if segment != "[]" {
            attempts.position = Self.join(outerPosition, segment)
        } else if let key {
            attempts.position = Self.entry(outerPosition, key)
        } else {
            attempts.position = Self.element(outerPosition, index)
        }
        defer {
            attempts.path = outer
            attempts.position = outerPosition
        }
        try check()
    }

    // The exact path of a list element, which converting the checked document follows to the alternatives the check chose.
    static func element(_ position: String, _ index: Int) -> String { position + "[" + String(index) + "]" }

    // The exact path of a handle Map entry, by the canonical text of its key, since a Map orders its keys canonically.
    static func entry(_ position: String, _ key: GesHandle) -> String { entry(position, key.description) }

    private static func entry(_ position: String, _ key: String) -> String { position + "[" + key + "]" }

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
            // A fixed List with a literal default fills a shorter List of its field (specs/Schemas.md#fixed-lists).
            if field.alternatives.count == 1, field.alternatives[0].exact, let fallback = field.defaultValue, fallback.kind != .field {
                attempts.filled.insert(ObjectIdentifier(field.alternatives[0]))
            }
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
                if !holds { continue }
                repeatChoices(attempts.made[attempt]!)
            } else {
                let mark = attempts.choices.count
                let direct = attempts.direct.count
                do {
                    try type(alternative, value, start)
                    attempts.holds[attempt] = true
                    attempts.made[attempt] = Array(attempts.direct[direct...])
                    attempts.direct.removeSubrange(direct...)
                } catch is Failure {
                    // The next alternative may hold; the unions inside this one chose nothing.
                    revert(mark)
                    attempts.direct.removeSubrange(direct...)
                    attempts.holds[attempt] = false
                    continue
                }
            }
            choose(Choice(attempts.position, alternatives), alternative)
            attempts.direct.append(Made(position: attempts.position, alternatives: alternatives, alternative: alternative, attempt: attempt))
            return
        }
        throw fail(Self.fieldType, start)
    }

    // Repeats the choices of a cached alternative and of the unions inside it.
    private func repeatChoices(_ made: [Made]) {
        for union in made {
            choose(Choice(union.position, union.alternatives), union.alternative)
            if let inner = attempts.made[union.attempt] { repeatChoices(inner) }
        }
    }

    private func choose(_ union: Choice, _ alternative: GesResolvedType) {
        attempts.choices.append((union, attempts.chosen[union]))
        attempts.chosen[union] = alternative
    }

    // Takes back the choices made since the mark, latest first.
    private func revert(_ mark: Int) {
        for (union, replaced) in attempts.choices[mark...].reversed() { attempts.chosen[union] = replaced }
        attempts.choices.removeSubrange(mark...)
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
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets, _) = Self.resolved(fields, start) else { return try require(false) }
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
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets, _) = Self.resolved(fields, start) else { return try require(false) }
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
            for (index, item) in items.enumerated() { try under("[]", index: index) { try spatial(type, Self.resolved(item, offsets[index]), offsets[index], point: true) } }
        case .tag:
            guard case .tag(let name) = value else { return try require(false) }
            try require(type.tags?.contains(name) ?? true)
            if let max = type.max, name.utf8.count > max { throw fail(Self.outOfRange, start) }
        case .text:
            guard case .text(let text) = value else { return try require(false) }
            if let max = type.max, text.utf8.count > max { throw fail(Self.outOfRange, start) }
        case .flags:
            // Each key names a member and each value is a Boolean.
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets, _) = value else { return try require(false) }
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
            if type.exact, items.count < type.max!, !attempts.filled.contains(ObjectIdentifier(type)) { throw fail(Self.outOfRange, start) }
            if let element = type.element {
                for (index, item) in items.enumerated() { try under("[]", index: index) { try union(element, item, offsets[index]) } }
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
            guard case .map(let keys, let values, let keyOffsets, let valueOffsets, _) = Self.resolved(fields, start) else { return try require(false) }
            try self.fields(keys, values, keyOffsets, valueOffsets, start: start, type.reference!)
        case .map:
            if let key = type.handle { return try handleMap(type, key, value, start) }
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
        case .handle:
            guard case .handle(let text, _, _) = value else { return try require(false) }
            try handle(type.handle!, versioned: type.versioned, text, start)
        case .nothing:
            if case .nothing = value { return }
            try require(false)
        }
    }

    // A handle of the field's type whose id, part and version have the declared forms. An `@Name~` field requires a version
    // or a ref; an `@Name` field takes neither.
    private func handle(_ type: GesResolvedHandle, versioned: Bool, _ text: String, _ start: Int) throws {
        guard let handle = try? GesHandle(parsing: text), handle.typeName == type.name, handle.isComposite == type.hasPart else { throw fail(Self.fieldType, start) }
        let identity = type.root ?? type
        try handleKey(identity.id, handle, part: false, start)
        if handle.isComposite { try handleKey(type.id, handle, part: true, start) }
        if handle.hasVersion || !handle.ref.isNothing {
            // Only an `@Name~` field holds a version or a ref; a type without versions takes neither.
            if identity.versionKind == .none { throw fail(Self.fieldType, start) }
            if !versioned { throw fail(Self.unexpectedVersion, start) }
            guard handle.hasVersion else { return }
            if let revision = identity.revision {
                guard let number = handle.version.integerValue else { throw fail(Self.fieldType, start) }
                try numeric(revision.constraints, true, number, Double(number), start)
            } else if handle.version.bytesValue?.count != (identity.versionKind == .sha1 ? 20 : 32) {
                throw fail(Self.fieldType, start)
            }
        } else if versioned {
            throw fail(Self.fieldType, start)
        }
    }

    // The id of a handle, or its part, against the declared key type.
    private func handleKey(_ type: GesResolvedType, _ handle: GesHandle, part: Bool, _ start: Int) throws {
        let key = part ? handle.part : handle.id
        if type.kind == .text {
            guard let text = key.textValue else { throw fail(Self.fieldType, start) }
            if let max = type.max, text.utf8.count > max { throw fail(Self.outOfRange, start) }
            return
        }
        guard let integer = key.integerValue else { throw fail(Self.fieldType, start) }
        try numeric(type.constraints, true, integer, Double(integer), start)
    }

    // A handle Map: handle keys of the key type, within the maximum count, and values of the element union.
    private func handleMap(_ type: GesResolvedType, _ key: GesResolvedHandle, _ value: GesDocumentNode, _ start: Int) throws {
        guard case .map(let keys, let values, let keyOffsets, let valueOffsets, let handleKeys) = value else { throw fail(Self.fieldType, start) }
        if let max = type.max, keys.count > max { throw fail(Self.outOfRange, start) }
        for index in keys.indices {
            let offset = index < keyOffsets.count ? keyOffsets[index] : start
            if index >= handleKeys.count || !handleKeys[index] { throw fail(Self.fieldType, offset) }
            let canonical = (try? GesHandle(parsing: keys[index]))?.description ?? keys[index]
            try under("[]", index: index, key: canonical) {
                try under("key") { try handle(key, versioned: false, keys[index], offset) }
                try under("value") { try union(type.element!, values[index], valueOffsets[index]) }
            }
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
