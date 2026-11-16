// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

private typealias Code = GesSchemaDiagnostic

/// A field written `computed by`: its type, its checked expression, and where it was written, which a schema that takes it
/// keeps for its constants and positions.
final class GesResolvedComputed {
    let name: String
    let kind: GesSchemaTypeKind
    let unit: GesQuantityUnit
    let isOptional: Bool
    var expression: GesComputedNode!
    let offset: Int
    let scope: GesFileScope
    let syntax: GesSchemaFieldSyntax
    /// The lets of the schema that wrote it, which its expression may name.
    let lets: [GesResolvedLet]

    init(name: String, kind: GesSchemaTypeKind, unit: GesQuantityUnit, isOptional: Bool, offset: Int, scope: GesFileScope, syntax: GesSchemaFieldSyntax, lets: [GesResolvedLet]) {
        self.name = name
        self.kind = kind
        self.unit = unit
        self.isOptional = isOptional
        self.offset = offset
        self.scope = scope
        self.syntax = syntax
        self.lets = lets
    }

    var type: GesComputedType { kind == .boolean ? .boolean : kind == .percentage ? .percentage : Self.spatial(kind) ?? .number }

    /// The computed type of a spatial schema type, or nil for another one.
    static func spatial(_ kind: GesSchemaTypeKind) -> GesComputedType? {
        switch kind {
        case .vector: .vector
        case .point: .point
        case .box: .box
        case .disc: .disc
        case .poly: .poly
        default: nil
        }
    }

    var isSpatial: Bool { Self.spatial(kind) != nil }
}

extension GesFileScope {
    /// A checked subexpression and, for a path, what it reaches: the schema of a record, or the alternatives of a list's
    /// elements.
    private struct Checked {
        let node: GesComputedNode
        var record: GesResolvedDeclaration?
        var element: [GesResolvedType]?
        /// Whether a field on the path is optional.
        var optional = false
    }

    /// What a name inside an expression can mean: the schema's fields and earlier computed fields, the lets of the schema
    /// that wrote the expression, the constants of its file, and the variable of the projection being checked.
    private final class Context {
        let declaration: GesResolvedDeclaration
        let earlier: [GesResolvedComputed]
        let current: GesResolvedComputed
        var variable = ""
        var element: Checked?

        init(_ declaration: GesResolvedDeclaration, _ earlier: [GesResolvedComputed], _ current: GesResolvedComputed) {
            self.declaration = declaration
            self.earlier = earlier
            self.current = current
        }
    }

    /// Checks the computed fields of a composed schema in order: own ones where they stand and taken ones where their `with`
    /// stands. Every expression is checked against this schema's fields, with the lets and constants of where it was written.
    static func resolveComputed(_ declaration: GesResolvedDeclaration, _ entries: [GesResolvedComputed], _ names: inout Set<String>) throws -> [GesResolvedComputed] {
        var resolved: [GesResolvedComputed] = []
        for entry in entries {
            let scope = entry.scope
            let syntax = entry.syntax
            if !names.insert(entry.name).inserted { throw scope.error(Code.duplicateName, entry.offset, "'\(entry.name)' is declared twice in '\(declaration.name)'.") }
            let type = syntax.alternatives
            let constraints = type[0].constraints
            if type.count != 1 || ![.number, .quantity, .percentage, .boolean, .vector, .point, .box, .disc, .poly].contains(type[0].kind)
                || constraints.integer || constraints.lower != nil || constraints.step != nil || constraints.delta != nil
                || type[0].axes != nil || type[0].max != nil || !type[0].maxName.isEmpty
            {
                throw scope.error(
                    Code.invalidExpression,
                    entry.offset,
                    "A computed field is a Number, Quantity, Percentage, Boolean, Vector, Point, Box, Disc or Poly without constraints, axes or max."
                )
            }
            if syntax.marker == .key { throw scope.error(Code.invalidExpression, entry.offset, "A computed field is no key.") }
            if entry.isOptional && entry.kind == .boolean {
                throw scope.error(Code.invalidExpression, entry.offset, "A computed Boolean is never absent, as a record reads nothing as false; it is not optional.")
            }
            let context = Context(declaration, resolved, entry)
            let expression = syntax.computed!
            let result = try scope.check(expression, context).node
            if result.type != entry.type {
                throw scope.error(Code.invalidExpression, expression.offset, "The expression gives a \(describe(result)), but '\(entry.name)' is a \(entry.kind.rawValue).")
            }
            if entry.type == .number && result.unit != entry.unit {
                let declared = entry.unit == .none ? "Number" : "Quantity(\(entry.unit.rawValue))"
                throw scope.error(Code.unitMismatch, expression.offset, "The expression gives a \(describe(result)), but '\(entry.name)' is a \(declared).")
            }
            if entry.isSpatial && result.unit != entry.unit {
                throw scope.error(
                    Code.unitMismatch,
                    expression.offset,
                    "The expression gives a \(describe(result)), but '\(entry.name)' is a \(describeSpatial(entry.type, entry.unit))."
                )
            }
            try scope.checkAlone(expression, result, true)
            guardReads(result, [])
            if result.mayBeAbsent && !entry.isOptional {
                throw scope.error(
                    Code.computedMayBeAbsent,
                    expression.offset,
                    entry.kind == .boolean
                        ? "'\(entry.name)' reads an optional field that may be absent; guard it with has value or give a fallback with default."
                        : "'\(entry.name)' reads an optional field that may be absent; mark it optional, guard the field with has value or give a fallback with default."
                )
            }
            entry.expression = result
            resolved.append(entry)
        }
        return resolved
    }

    /// Which reads `has value` proves present, and which nodes may give nothing for an absent optional field. A `has value`
    /// of a path proves it and every leading part of it present on the right of `and` and in the value its `when` condition
    /// selects.
    private static func guardReads(_ node: GesComputedNode, _ present: [(op: GesComputedOp, path: [String])]) {
        let children = node.children
        switch node.op {
        case .field, .item:
            for (op, path) in present where op == node.op && path.count > node.present && node.path.starts(with: path) { node.present = path.count }
            if node.present < node.optionalSteps.count { node.mayBeAbsent = node.optionalSteps[node.present...].contains(true) }
        case .hasValue:
            guardReads(children[0], present)
        case .and:
            guardReads(children[0], present)
            guardReads(children[1], present + proven(children[0]))
            node.mayBeAbsent = children[0].mayBeAbsent || children[1].mayBeAbsent
        case .choice:
            var index = 0
            while index + 1 < children.count {
                guardReads(children[index + 1], present)
                guardReads(children[index], present + proven(children[index + 1]))
                node.mayBeAbsent = node.mayBeAbsent || children[index].mayBeAbsent
                index += 2
            }
            guardReads(children[children.count - 1], present)
            node.mayBeAbsent = node.mayBeAbsent || children[children.count - 1].mayBeAbsent
        case .default:
            guardReads(children[0], present)
            guardReads(children[1], present)
            node.mayBeAbsent = children[1].mayBeAbsent
        case .sum, .polyOf:
            // A projection reads the element afresh, so no proof about an element carries into it.
            guardReads(children[0], present)
            if children.count > 1 {
                guardReads(children[1], present.filter { $0.op == .field })
                node.mayBeAbsent = children[1].mayBeAbsent
            }
        case .count:
            guardReads(children[0], present)
        default:
            for child in children {
                guardReads(child, present)
                node.mayBeAbsent = node.mayBeAbsent || child.mayBeAbsent
            }
        }
    }

    /// The paths that a true condition proves present: its `has value` conjuncts.
    private static func proven(_ node: GesComputedNode) -> [(op: GesComputedOp, path: [String])] {
        if node.op == .and { return proven(node.children[0]) + proven(node.children[1]) }
        if node.op == .hasValue, node.children[0].op == .field || node.children[0].op == .item { return [(node.children[0].op, node.children[0].path)] }
        return []
    }

    private static func describe(_ node: GesComputedNode) -> String {
        switch node.type {
        case .number: node.unit == .none ? "Number" : "Quantity(\(node.unit.rawValue))"
        case .list: "List"
        case .record: "record"
        case .vector, .point, .box, .disc, .poly: describeSpatial(node.type, node.unit)
        default: node.type.rawValue
        }
    }

    private static func describeSpatial(_ type: GesComputedType, _ unit: GesQuantityUnit) -> String {
        type.rawValue + (unit == .none ? "" : "(\(unit.rawValue))")
    }

    /// A sum of quantities or percentages is the unitless 0 for an empty list, which only the final conversion of the field
    /// turns into a value of the field's type. So such a sum stands alone: as the expression, a value of a choice, or a side
    /// of `default`.
    private func checkAlone(_ syntax: GesSchemaExpressionSyntax, _ node: GesComputedNode, _ alone: Bool) throws {
        if node.op == .sum && !alone && (node.type == .percentage || node.unit != .none) {
            throw error(
                Code.invalidExpression,
                syntax.offset,
                "A sum of quantities or percentages is 0 without a unit for an empty list, so it stands alone: as the expression, a value of a choice or a side of default."
            )
        }
        for index in 0..<Swift.min(node.children.count, syntax.children.count) {
            let passes: Bool
            switch node.op {
            case .choice: passes = index % 2 == 0 || index == node.children.count - 1
            case .default: passes = true
            default: passes = false
            }
            try checkAlone(syntax.children[index], node.children[index], alone && passes)
        }
    }

    private func check(_ syntax: GesSchemaExpressionSyntax, _ context: Context) throws -> Checked {
        let children = syntax.children
        switch syntax.kind {
        case .literal:
            return Checked(node: Self.literal(syntax.literal!.value, syntax.literal!.unit, syntax.literal!.isPercent))
        case .boolean:
            return Checked(node: GesComputedNode(.literal, .boolean, literal: .of(boolean: syntax.boolean)))
        case .constant:
            guard let constant = constants[syntax.name] else { throw error(Code.unknownName, syntax.offset, "Constant '$\(syntax.name)' is not declared in this file.") }
            return Checked(node: Self.literal(constant.value, constant.unit, constant.isPercent))
        case .name:
            return try name(syntax, context)
        case .member:
            let target = try check(children[0], context)
            if target.node.type.isSpatial { return try part(syntax, target.node) }
            guard target.node.op == .field || target.node.op == .item, let record = target.record else {
                throw error(Code.invalidExpression, syntax.offset, "'.\(syntax.name)' reads a field of a schema, which this is not.")
            }
            let path = target.node.path + [syntax.name]
            var reached: Checked
            if let field = record.fields.first(where: { $0.name == syntax.name }) {
                reached = try reach(field, target.node.op, path, syntax.offset)
            } else if let computed = record.computed.first(where: { $0.name == syntax.name }) {
                reached = Self.reach(computed, target.node.op, path)
            } else {
                throw error(Code.unknownName, syntax.offset, "'\(syntax.name)' is no field of '\(record.name)'.")
            }
            reached.node.optionalSteps = target.node.optionalSteps + [reached.optional]
            reached.optional = reached.optional || target.optional
            return reached
        case .negate, .abs:
            let operand = try value(children[0], context)
            if operand.type == .vector {
                return syntax.kind == .negate ? Self.node(.negate, .vector, operand.unit, [operand]) : Self.node(.abs, .number, operand.unit, [operand])
            }
            if operand.type != .number && operand.type != .percentage { throw mismatch(syntax, "a number, percentage or vector", operand) }
            return Self.node(syntax.kind == .negate ? .negate : .abs, operand.type, operand.unit, [operand])
        case .not:
            let operand = try value(children[0], context)
            if operand.type != .boolean { throw mismatch(syntax, "a Boolean", operand) }
            return Self.node(.not, .boolean, .none, [operand])
        case .binary:
            return try binary(syntax, try value(children[0], context), try value(children[1], context))
        case .choice:
            var parts: [GesComputedNode] = []
            for child in children { parts.append(try value(child, context)) }
            for index in stride(from: 1, to: parts.count, by: 2) where parts[index].type != .boolean {
                throw mismatch(children[index], "a Boolean condition", parts[index])
            }
            let first = parts[0]
            for index in stride(from: 2, to: parts.count, by: 2) where !Self.sameType(first, parts[index]) {
                throw different(children[index], first, parts[index])
            }
            return Self.node(.choice, first.type, first.unit, parts)
        case .min, .max, .clamp:
            var parts: [GesComputedNode] = []
            for child in children {
                let part = try value(child, context)
                if part.type != .number { throw mismatch(child, "a number", part) }
                if let first = parts.first, part.unit != first.unit { throw different(child, first, part) }
                parts.append(part)
            }
            let op: GesComputedOp = syntax.kind == .min ? .min : syntax.kind == .max ? .max : .clamp
            return Checked(node: GesComputedNode(op, .number, unit: parts[0].unit, children: parts, first: true))
        case .default:
            let fallen = try value(children[0], context)
            let fallback = try value(children[1], context)
            if !Self.sameType(fallen, fallback) { throw different(children[1], fallen, fallback) }
            return Self.node(.default, fallen.type, fallen.unit, [fallen, fallback])
        case .hasValue:
            let operand = try check(children[0], context)
            if operand.node.type == .list || (operand.node.type == .record && operand.record == nil) {
                throw error(Code.invalidExpression, children[0].offset, "has value takes a number, percentage, Boolean or schema field; a list or text is never read by a computed field.")
            }
            return Self.node(.hasValue, .boolean, .none, [operand.node])
        case .sum, .count:
            return try aggregate(syntax, context)
        case .function:
            return try function(syntax, context)
        case .construct:
            return try construct(syntax, context)
        case .list:
            throw error(Code.invalidExpression, syntax.offset, "A list in a computed expression holds the points of a :Poly.")
        case .select:
            throw error(Code.invalidExpression, syntax.offset, "A projection with :select gives the points of a :Poly.")
        }
    }

    /// A number or percentage, which functions and powers read through its numeric view: a percentage as its ratio.
    private func numeric(_ syntax: GesSchemaExpressionSyntax, _ node: GesComputedNode) throws {
        if node.type != .number && node.type != .percentage { throw mismatch(syntax, "a number or percentage", node) }
    }

    /// The unit of a number, none for a percentage, which functions read as its ratio.
    private static func numericUnit(_ node: GesComputedNode) -> GesQuantityUnit { node.type == .number ? node.unit : .none }

    /// The functions of GES with the unit rules of the virtual machine, each giving a number: roots take the unit of the root
    /// in the derivation table, trigonometry takes radians as a plain number or degrees, the inverse functions and `atan2`
    /// give radians, rounding keeps the unit, and the others read plain numbers.
    private func function(_ syntax: GesSchemaExpressionSyntax, _ context: Context) throws -> Checked {
        let children = syntax.children
        var arguments: [GesComputedNode] = []
        for child in children {
            let argument = try value(child, context)
            try numeric(child, argument)
            arguments.append(argument)
        }
        let unit = Self.numericUnit(arguments[0])
        switch syntax.name {
        case "sqrt", "cbrt":
            let square = syntax.name == "sqrt"
            guard let root = GesQuantityUnit.power(unit, square ? 0.5 : 1.0 / 3.0) else {
                throw error(Code.unitMismatch, syntax.offset, "A \(Self.describe(arguments[0])) has no \(square ? "square" : "cube") root in the closed unit set.")
            }
            return Self.node(square ? .sqrt : .cbrt, .number, root, arguments)
        case "sin", "cos", "tan":
            if unit != .none && unit != .degree { throw angle(syntax, arguments[0]) }
            let op: GesComputedOp = syntax.name == "sin" ? .sin : syntax.name == "cos" ? .cos : .tan
            return Checked(node: GesComputedNode(op, .number, children: arguments, first: unit == .degree))
        case "rad":
            if unit != .none && unit != .degree { throw angle(syntax, arguments[0]) }
            return Self.node(.rad, .number, .none, arguments)
        case "atan2", "hypot":
            for index in 1..<arguments.count where Self.numericUnit(arguments[index]) != unit {
                throw error(
                    Code.unitMismatch,
                    children[index].offset,
                    "The arguments of \(syntax.name) have one unit, but a \(Self.describe(arguments[index])) meets a \(Self.describe(arguments[0]))."
                )
            }
            return syntax.name == "atan2" ? Self.node(.atan2, .number, .none, arguments) : Self.node(.hypot, .number, unit, arguments)
        case "floor", "ceil", "truncate", "round half even", "round half up", "round half down":
            let op: GesComputedOp
            switch syntax.name {
            case "floor": op = .floor
            case "ceil": op = .ceil
            case "truncate": op = .truncate
            case "round half even": op = .roundHalfEven
            case "round half up": op = .roundHalfUp
            default: op = .roundHalfDown
            }
            return Self.node(op, .number, unit, arguments)
        default:
            if unit != .none { throw error(Code.unitMismatch, syntax.offset, "\(syntax.name) reads a plain number, but this is a \(Self.describe(arguments[0])).") }
            let op: GesComputedOp
            switch syntax.name {
            case "asin": op = .asin
            case "acos": op = .acos
            case "atan": op = .atan
            case "exp": op = .exp
            case "ln": op = .ln
            default: op = .deg
            }
            return Self.node(op, .number, op == .deg ? .degree : .none, arguments)
        }
    }

    private func angle(_ syntax: GesSchemaExpressionSyntax, _ argument: GesComputedNode) -> GesSchemaError {
        error(Code.unitMismatch, syntax.offset, "\(syntax.name) reads an angle in radians as a plain number or in degrees, but this is a \(Self.describe(argument)).")
    }

    /// A value that arithmetic, comparisons and choices read: a number, percentage or Boolean.
    private func value(_ syntax: GesSchemaExpressionSyntax, _ context: Context) throws -> GesComputedNode {
        let node = try check(syntax, context).node
        if node.type == .record || node.type == .list {
            throw error(Code.invalidExpression, syntax.offset, "A \(Self.describe(node)) is read only through its fields, an aggregate or has value.")
        }
        return node
    }

    private func aggregate(_ syntax: GesSchemaExpressionSyntax, _ context: Context) throws -> Checked {
        let children = syntax.children
        let source = try check(children[0], context)
        if source.node.op == .part {
            if syntax.kind != .count { throw error(Code.invalidExpression, syntax.offset, "The points of a Poly take only [:count].") }
            return Self.node(.pointCount, .number, .none, [source.node.children[0]])
        }
        if source.node.type != .list { throw error(Code.invalidExpression, children[0].offset, "An aggregate reads a list field.") }
        if source.optional { throw error(Code.invalidExpression, children[0].offset, "An aggregate reads a list that is no optional field and lies in none.") }
        if syntax.kind == .count { return Self.node(.count, .number, .none, [source.node]) }
        let element = source.element!
        if element.count != 1 { throw error(Code.invalidExpression, children[0].offset, "A sum reads a list without a union.") }
        let item = try self.element(element[0], children[0].offset)
        if children.count == 1 {
            if item.node.type != .number && item.node.type != .percentage {
                throw error(Code.invalidExpression, children[0].offset, "A sum without a projection reads a list of numbers or percentages.")
            }
            return Self.node(.sum, item.node.type, item.node.unit, [source.node])
        }
        if !context.variable.isEmpty { throw error(Code.invalidExpression, syntax.offset, "A projection does not contain another one.") }
        if Self.isTaken(context, syntax.name) { throw error(Code.duplicateName, syntax.offset, "'\(syntax.name)' already names a field or let; the variable needs another name.") }
        context.variable = syntax.name
        context.element = item
        defer {
            context.variable = ""
            context.element = nil
        }
        let projection = try value(children[1], context)
        if projection.type != .number && projection.type != .percentage { throw mismatch(children[1], "a number or percentage", projection) }
        return Self.node(.sum, projection.type, projection.unit, [source.node, projection])
    }

    private static func isTaken(_ context: Context, _ name: String) -> Bool {
        context.declaration.fields.contains { $0.name == name } || context.earlier.contains { $0.name == name } || context.current.name == name
            || context.current.lets.contains { $0.name == name }
    }

    /// An element of a list as the projection variable: a record of a schema or a number.
    private func element(_ type: GesResolvedType, _ offset: Int) throws -> Checked {
        let (node, record, element) = Self.typeNode(type, .item, [])
        return Checked(node: node, record: record, element: element)
    }

    private func name(_ syntax: GesSchemaExpressionSyntax, _ context: Context) throws -> Checked {
        let name = syntax.name
        if !context.variable.isEmpty && name == context.variable { return context.element! }
        if let stored = context.declaration.fields.first(where: { $0.name == name }) { return try reach(stored, .field, [name], syntax.offset) }
        if let earlier = context.earlier.first(where: { $0.name == name }) { return Self.reach(earlier, .field, [name]) }
        if name == context.current.name || context.declaration.computed.contains(where: { $0.name == name })
            || context.declaration.syntax.computed.contains(where: { $0.name == name })
        {
            throw error(Code.invalidExpression, syntax.offset, "'\(name)' is computed after '\(context.current.name)'; a computed field reads only fields and the computed fields before it.")
        }
        if let declared = context.current.lets.first(where: { $0.name == name }) {
            return Checked(node: Self.literal(declared.value, declared.unit, declared.kind == .percentage))
        }
        throw error(Code.unknownName, syntax.offset, "'\(name)' is neither a field nor a let of '\(context.declaration.name)'.")
    }

    private func reach(_ field: GesResolvedField, _ op: GesComputedOp, _ path: [String], _ offset: Int) throws -> Checked {
        if field.alternatives.count != 1 { throw error(Code.invalidExpression, offset, "'\(field.name)' is a union, which a computed field does not read.") }
        let (node, record, element) = Self.typeNode(field.alternatives[0], op, path)
        node.optionalSteps = [field.marker == .optional]
        return Checked(node: node, record: record, element: element, optional: field.marker == .optional)
    }

    private static func reach(_ field: GesResolvedComputed, _ op: GesComputedOp, _ path: [String]) -> Checked {
        let node = GesComputedNode(op, field.type, unit: field.kind == .quantity || field.isSpatial ? field.unit : .none, path: path)
        node.optionalSteps = [field.isOptional]
        return Checked(node: node, optional: field.isOptional)
    }

    private static func typeNode(_ type: GesResolvedType, _ op: GesComputedOp, _ path: [String]) -> (GesComputedNode, GesResolvedDeclaration?, [GesResolvedType]?) {
        switch type.kind {
        case .number: return (GesComputedNode(op, .number, path: path), nil, nil)
        case .quantity: return (GesComputedNode(op, .number, unit: type.unit, path: path), nil, nil)
        case .percentage: return (GesComputedNode(op, .percentage, path: path), nil, nil)
        case .boolean: return (GesComputedNode(op, .boolean, path: path), nil, nil)
        case .vector, .point, .box, .disc, .poly: return (GesComputedNode(op, GesResolvedComputed.spatial(type.kind)!, unit: type.unit, path: path), nil, nil)
        case .list where type.element != nil: return (GesComputedNode(op, .list, path: path), nil, type.element)
        case .reference where type.reference?.kind == .schema: return (GesComputedNode(op, .record, path: path), type.reference, nil)
        // Text, tags and other types are not read; only has value may name them, and it rejects them.
        default: return (GesComputedNode(op, .record, path: path), nil, nil)
        }
    }

    private static func literal(_ value: GesSchemaDecimal, _ unit: GesQuantityUnit, _ percent: Bool) -> GesComputedNode {
        let number = Double("\(value.significand)e\(value.exponent)")!
        let literal: GesComputedValue
        if percent {
            literal = .of(percentage: number)
        } else if let integer = exact(value) {
            literal = .of(integer: integer)
        } else {
            literal = .of(float: number)
        }
        return GesComputedNode(.literal, percent ? .percentage : .number, unit: percent ? .none : unit, literal: literal, decimal: value)
    }

    /// A whole decimal within Int64, which a literal keeps exactly.
    private static func exact(_ value: GesSchemaDecimal) -> Int64? {
        if value.exponent < 0 { return nil }
        var result = value.significand
        for _ in 0..<value.exponent {
            let (next, overflow) = result.multipliedReportingOverflow(by: 10)
            if overflow { return nil }
            result = next
        }
        return result
    }

    private func binary(_ syntax: GesSchemaExpressionSyntax, _ left: GesComputedNode, _ right: GesComputedNode) throws -> Checked {
        let a = left.unit
        let b = right.unit
        let numeric = { (node: GesComputedNode) in node.type == .number || node.type == .percentage }
        if left.type.isSpatial || right.type.isSpatial { return try spatialBinary(syntax, left, right) }
        switch syntax.binary {
        case .add, .subtract:
            if left.type == .number && right.type == .number && a != b { throw units(syntax, left, right) }
            if left.type == .number && right.type == .percentage && !a.acceptsPercentage { throw error(Code.unitMismatch, syntax.offset, "A percentage does not scale a decibel level.") }
            if !((left.type == .number && numeric(right)) || (left.type == .percentage && right.type == .percentage)) {
                throw error(
                    Code.invalidExpression,
                    syntax.offset,
                    "\(Self.describe(left)) \(GesSchemaFormatter.symbol(syntax.binary)) \(Self.describe(right)) has no value; a percentage stands right of a number or beside another percentage."
                )
            }
            return Self.flags(syntax.binary == .add ? .add : .subtract, left.type, a, left, right, a == b, a.acceptsPercentage, false)
        case .multiply:
            let product = Self.productUnit(a, b)
            if left.type == .number && right.type == .number && product == nil { throw units(syntax, left, right) }
            if (left.type == .number && right.type == .percentage && !a.acceptsPercentage) || (left.type == .percentage && right.type == .number && !b.acceptsPercentage) {
                throw error(Code.unitMismatch, syntax.offset, "A percentage does not scale a decibel level.")
            }
            if !numeric(left) || !numeric(right) { throw error(Code.invalidExpression, syntax.offset, "\(Self.describe(left)) * \(Self.describe(right)) has no value.") }
            let type: GesComputedType = left.type == .percentage && right.type == .percentage ? .percentage : .number
            let unit = left.type == .percentage ? b : right.type == .percentage ? a : product!
            return Self.flags(.multiply, type, unit, left, right, product != nil, a.acceptsPercentage, b.acceptsPercentage)
        case .divide:
            let quotient = Self.quotientUnit(a, b)
            if left.type == .number && right.type == .number && quotient == nil { throw units(syntax, left, right) }
            if left.type == .percentage && right.type == .number && b != .none { throw units(syntax, left, right) }
            if left.type == .number && right.type == .percentage && !a.acceptsPercentage { throw error(Code.unitMismatch, syntax.offset, "A percentage does not scale a decibel level.") }
            if !numeric(left) || !numeric(right) { throw error(Code.invalidExpression, syntax.offset, "\(Self.describe(left)) / \(Self.describe(right)) has no value.") }
            let type: GesComputedType = left.type == .percentage && right.type == .number ? .percentage : .number
            let unit = left.type == .percentage ? GesQuantityUnit.none : right.type == .percentage ? a : quotient!
            return Self.flags(.divide, type, unit, left, right, quotient != nil, a.acceptsPercentage, b == .none)
        case .power:
            // A quantity takes the unit of its power from the derivation table, which needs the exponent at binding.
            try self.numeric(syntax.children[0], left)
            try self.numeric(syntax.children[1], right)
            if right.type == .number && b != .none {
                throw error(Code.unitMismatch, syntax.children[1].offset, "An exponent has no unit, but this one is a \(Self.describe(right)).")
            }
            var unit = GesQuantityUnit.none
            if left.type == .number && a != .none {
                if right.op != .literal {
                    throw error(Code.unitMismatch, syntax.children[1].offset, "The power of a \(Self.describe(left)) takes a literal exponent, which gives the unit of the result.")
                }
                guard let powered = GesQuantityUnit.power(a, right.literal.numeric) else {
                    throw error(Code.unitMismatch, syntax.offset, "A \(Self.describe(left)) to the power \(right.decimal.canonical) has no unit of the closed unit set.")
                }
                unit = powered
            }
            return Self.node(.power, .number, unit, [left, right])
        case .and, .or:
            if left.type != .boolean { throw mismatch(syntax.children[0], "a Boolean", left) }
            if right.type != .boolean { throw mismatch(syntax.children[1], "a Boolean", right) }
            return Self.node(syntax.binary == .and ? .and : .or, .boolean, .none, [left, right])
        default:
            let equality = syntax.binary == .equal || syntax.binary == .notEqual
            if left.type == .boolean && !equality { throw mismatch(syntax.children[0], "a number or percentage", left) }
            if left.type == .number && right.type == .number && a != b { throw units(syntax, left, right) }
            if !Self.sameType(left, right) { throw different(syntax.children[1], left, right) }
            let op: GesComputedOp
            switch syntax.binary {
            case .less: op = .less
            case .lessOrEqual: op = .lessOrEqual
            case .greater: op = .greater
            case .greaterOrEqual: op = .greaterOrEqual
            case .equal: op = .equal
            default: op = .notEqual
            }
            return Self.flags(op, .boolean, .none, left, right, true, false, false)
        }
    }

    /// The arithmetic of vectors and points as the virtual machine has it: vectors add and subtract, a point moves by a vector,
    /// two points subtract to a vector, and a vector scales by a number, with the product or quotient unit, or a percentage.
    private func spatialBinary(_ syntax: GesSchemaExpressionSyntax, _ left: GesComputedNode, _ right: GesComputedNode) throws -> Checked {
        let spelled = "\(Self.describe(left)) \(GesSchemaFormatter.symbol(syntax.binary)) \(Self.describe(right)) has no value"
        switch syntax.binary {
        case .add, .subtract:
            let subtract = syntax.binary == .subtract
            let result: GesComputedType?
            switch (left.type, right.type) {
            case (.vector, .vector): result = .vector
            case (.point, .vector): result = .point
            case (.point, .point) where subtract: result = .vector
            default: result = nil
            }
            guard let result else {
                throw error(
                    Code.invalidExpression,
                    syntax.offset,
                    "\(spelled); vectors add and subtract, a point moves by a vector and two points subtract to a vector."
                )
            }
            if left.unit != right.unit { throw units(syntax, left, right) }
            return Self.node(subtract ? .subtract : .add, result, left.unit, [left, right])
        case .multiply, .divide:
            let divide = syntax.binary == .divide
            let scaled = left.type == .vector ? left : right
            let scalar = left.type == .vector ? right : left
            if scaled.type != .vector || (scalar.type != .number && scalar.type != .percentage) || (divide && left.type != .vector) {
                throw error(Code.invalidExpression, syntax.offset, "\(spelled); a vector scales by a number or percentage, and a point does not scale.")
            }
            let unit: GesQuantityUnit
            if scalar.type == .percentage {
                if !scaled.unit.acceptsPercentage { throw error(Code.unitMismatch, syntax.offset, "A percentage does not scale a decibel level.") }
                unit = scaled.unit
            } else {
                let derived =
                    divide
                    ? Self.quotientUnit(scaled.unit, scalar.unit) : left.type == .vector ? Self.productUnit(scaled.unit, scalar.unit) : Self.productUnit(scalar.unit, scaled.unit)
                guard let derived else { throw units(syntax, left, right) }
                unit = derived
            }
            return Self.node(divide ? .divide : .multiply, .vector, unit, [left, right])
        default:
            throw error(Code.invalidExpression, syntax.offset, "\(spelled); spatial values are not compared.")
        }
    }

    /// A part of a spatial value: an axis of a vector or point, the corners, centre and size of a box, the centre and radius
    /// of a disc, or the points of a polygon, which only [:count] reads.
    private func part(_ syntax: GesSchemaExpressionSyntax, _ target: GesComputedNode) throws -> Checked {
        let name = syntax.name
        let type: GesComputedType
        switch (target.type, name) {
        case (.vector, "x"), (.vector, "y"), (.vector, "z"), (.point, "x"), (.point, "y"), (.point, "z"): type = .number
        case (.box, "lower"), (.box, "upper"), (.box, "center"): type = .point
        case (.box, "size"): type = .vector
        case (.disc, "center"): type = .point
        case (.disc, "radius"): type = .number
        case (.poly, "points"): type = .list
        default:
            let parts: String
            switch target.type {
            case .box: parts = "a Box has lower, upper, center and size."
            case .disc: parts = "a Disc has center and radius."
            case .poly: parts = "a Poly has points."
            default: parts = "it has x, y and z."
            }
            throw error(Code.unknownName, syntax.offset, "'\(name)' is no part of a \(Self.describe(target)); " + parts)
        }
        return Checked(node: GesComputedNode(.part, type, unit: target.unit, children: [target], path: [name]))
    }

    /// A spatial value built with the constructor of its type, as a script record builds it.
    private func construct(_ syntax: GesSchemaExpressionSyntax, _ context: Context) throws -> Checked {
        let children = syntax.children
        let labels = syntax.labels.joined(separator: ",")
        switch syntax.name {
        case "Vector", "Point":
            let axes = ["x", "y", "z"]
            var start = 0
            if !children.isEmpty && !syntax.labels[0].isEmpty {
                start = axes.firstIndex(of: syntax.labels[0]) ?? -1
                for index in 0..<children.count where start < 0 || start + index >= 3 || syntax.labels[index] != axes[start + index] {
                    throw error(
                        Code.invalidExpression,
                        children[index].offset,
                        "The components of :\(syntax.name) are labeled as a run of x, y and z without gaps, or all positional."
                    )
                }
            } else {
                for index in 0..<children.count where !syntax.labels[index].isEmpty {
                    throw error(Code.invalidExpression, children[index].offset, "The components of :\(syntax.name) are all labeled or all positional.")
                }
                if children.count == 1 || children.count > 3 {
                    throw error(Code.invalidExpression, syntax.offset, ":\(syntax.name) takes two or three positional components.")
                }
            }
            var components: [GesComputedNode?] = [nil, nil, nil]
            var unit: GesQuantityUnit?
            for index in 0..<children.count {
                let component = try value(children[index], context)
                if component.type != .number { throw mismatch(children[index], "a number", component) }
                if let first = unit, component.unit != first {
                    throw error(
                        Code.unitMismatch,
                        children[index].offset,
                        "The components of :\(syntax.name) have one unit, but a \(Self.describe(component)) meets a \(Self.describe(components[start]!))."
                    )
                }
                unit = component.unit
                components[start + index] = component
            }
            let resolved = unit ?? GesQuantityUnit.none
            let parts = components.map { $0 ?? Self.zero(resolved) }
            let point = syntax.name == "Point"
            return Self.node(point ? .point : .vector, point ? .point : .vector, resolved, parts)
        case "Box" where labels == "lower,upper" || labels == "center,size":
            let around = labels == "center,size"
            let first = try value(children[0], context)
            let second = try value(children[1], context)
            if first.type != .point { throw mismatch(children[0], "a Point", first) }
            if second.type != (around ? .vector : .point) { throw mismatch(children[1], around ? "a Vector" : "a Point", second) }
            if first.unit != second.unit {
                throw error(Code.unitMismatch, children[1].offset, "The parts of a :Box have one unit, but a \(Self.describe(second)) meets a \(Self.describe(first)).")
            }
            return Self.node(around ? .boxAround : .box, .box, first.unit, [first, second])
        case "Disc" where labels == "center,radius" || labels == "radius":
            let radius = try value(children[children.count - 1], context)
            if radius.type != .number { throw mismatch(children[children.count - 1], "a number", radius) }
            if children.count == 1 { return Self.node(.discAround, .disc, radius.unit, [radius]) }
            let center = try value(children[0], context)
            if center.type != .point { throw mismatch(children[0], "a Point", center) }
            if center.unit != radius.unit {
                throw error(
                    Code.unitMismatch,
                    children[1].offset,
                    "The centre and radius of a :Disc have one unit, but a \(Self.describe(radius)) meets a \(Self.describe(center))."
                )
            }
            return Self.node(.disc, .disc, center.unit, [center, radius])
        case "Poly" where labels == "points":
            return try poly(children[0], context)
        default:
            let message: String
            switch syntax.name {
            case "Box": message = ":Box takes lower: and upper:, or center: and size:."
            case "Disc": message = ":Disc takes center: and radius:, or radius:."
            default: message = ":Poly takes points:."
            }
            throw error(Code.invalidExpression, syntax.offset, message)
        }
    }

    /// The points of a :Poly: a list of point expressions, a list field of Points, or a projection of a list field to Points.
    private func poly(_ syntax: GesSchemaExpressionSyntax, _ context: Context) throws -> Checked {
        if syntax.kind == .list {
            var points: [GesComputedNode] = []
            for child in syntax.children {
                let point = try value(child, context)
                if point.type != .point { throw mismatch(child, "a Point", point) }
                if let first = points.first, point.unit != first.unit {
                    throw error(Code.unitMismatch, child.offset, "The points of a :Poly have one unit, but a \(Self.describe(point)) meets a \(Self.describe(first)).")
                }
                points.append(point)
            }
            return Self.node(.poly, .poly, points[0].unit, points)
        }
        let select = syntax.kind == .select
        let sourceSyntax = select ? syntax.children[0] : syntax
        let source = try check(sourceSyntax, context)
        guard source.node.type == .list, let elements = source.element else {
            throw error(
                Code.invalidExpression,
                sourceSyntax.offset,
                "The points of a :Poly are a list of points, a list field of Points or a projection of a list field with :select."
            )
        }
        if source.optional { throw error(Code.invalidExpression, sourceSyntax.offset, "The points of a :Poly read a list that is no optional field and lies in none.") }
        if elements.count != 1 { throw error(Code.invalidExpression, sourceSyntax.offset, "The points of a :Poly read a list without a union.") }
        let item = try element(elements[0], sourceSyntax.offset)
        if !select {
            if item.node.type != .point {
                throw error(
                    Code.invalidExpression,
                    sourceSyntax.offset,
                    "A list field gives the points of a :Poly when it holds Points; project other elements with :select."
                )
            }
            return Self.node(.polyOf, .poly, item.node.unit, [source.node])
        }
        if !context.variable.isEmpty { throw error(Code.invalidExpression, syntax.offset, "A projection does not contain another one.") }
        if Self.isTaken(context, syntax.name) { throw error(Code.duplicateName, syntax.offset, "'\(syntax.name)' already names a field or let; the variable needs another name.") }
        context.variable = syntax.name
        context.element = item
        defer {
            context.variable = ""
            context.element = nil
        }
        let projection = try value(syntax.children[1], context)
        if projection.type != .point { throw mismatch(syntax.children[1], "a Point", projection) }
        return Self.node(.polyOf, .poly, projection.unit, [source.node, projection])
    }

    /// The zero of an omitted component, in the unit of the others.
    private static func zero(_ unit: GesQuantityUnit) -> GesComputedNode {
        GesComputedNode(.literal, .number, unit: unit, literal: .of(integer: 0), decimal: GesSchemaDecimal(0, 0))
    }

    private static func productUnit(_ a: GesQuantityUnit, _ b: GesQuantityUnit) -> GesQuantityUnit? {
        a == .none ? b : b == .none ? a : GesQuantityUnit.product(a, b)
    }

    private static func quotientUnit(_ a: GesQuantityUnit, _ b: GesQuantityUnit) -> GesQuantityUnit? {
        b == .none ? a : a == b ? GesQuantityUnit.none : GesQuantityUnit.quotient(a, b)
    }

    private static func sameType(_ a: GesComputedNode, _ b: GesComputedNode) -> Bool { a.type == b.type && a.unit == b.unit }

    private static func node(_ op: GesComputedOp, _ type: GesComputedType, _ unit: GesQuantityUnit, _ children: [GesComputedNode]) -> Checked {
        Checked(node: GesComputedNode(op, type, unit: unit, children: children))
    }

    private static func flags(
        _ op: GesComputedOp,
        _ type: GesComputedType,
        _ unit: GesQuantityUnit,
        _ left: GesComputedNode,
        _ right: GesComputedNode,
        _ first: Bool,
        _ second: Bool,
        _ third: Bool
    ) -> Checked {
        Checked(node: GesComputedNode(op, type, unit: unit, children: [left, right], first: first, second: second, third: third))
    }

    private func mismatch(_ syntax: GesSchemaExpressionSyntax, _ expected: String, _ found: GesComputedNode) -> GesSchemaError {
        error(Code.invalidExpression, syntax.offset, "Expected \(expected), found a \(Self.describe(found)).")
    }

    private func different(_ syntax: GesSchemaExpressionSyntax, _ first: GesComputedNode, _ other: GesComputedNode) -> GesSchemaError {
        let code = first.type == .number && other.type == .number ? Code.unitMismatch : Code.invalidExpression
        return error(code, syntax.offset, "A \(Self.describe(other)) meets a \(Self.describe(first)).")
    }

    private func units(_ syntax: GesSchemaExpressionSyntax, _ left: GesComputedNode, _ right: GesComputedNode) -> GesSchemaError {
        error(Code.unitMismatch, syntax.offset, "\(Self.describe(left)) \(GesSchemaFormatter.symbol(syntax.binary)) \(Self.describe(right)) has no unit of the closed unit set.")
    }
}
