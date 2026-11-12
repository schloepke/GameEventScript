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

    var type: GesComputedType { kind == .boolean ? .boolean : kind == .percentage ? .percentage : .number }
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
            if type.count != 1 || ![.number, .quantity, .percentage, .boolean].contains(type[0].kind)
                || constraints.integer || constraints.lower != nil || constraints.step != nil || constraints.delta != nil
            {
                throw scope.error(Code.invalidExpression, entry.offset, "A computed field is a Number, Quantity, Percentage or Boolean without constraints.")
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
            try scope.checkAlone(expression, result, true)
            entry.expression = result
            resolved.append(entry)
        }
        return resolved
    }

    private static func describe(_ node: GesComputedNode) -> String {
        switch node.type {
        case .number: node.unit == .none ? "Number" : "Quantity(\(node.unit.rawValue))"
        case .list: "List"
        case .record: "record"
        default: node.type.rawValue
        }
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
            reached.optional = reached.optional || target.optional
            return reached
        case .negate, .abs:
            let operand = try value(children[0], context)
            if operand.type != .number && operand.type != .percentage { throw mismatch(syntax, "a number or percentage", operand) }
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
        }
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
        return Checked(node: node, record: record, element: element, optional: field.marker == .optional)
    }

    private static func reach(_ field: GesResolvedComputed, _ op: GesComputedOp, _ path: [String]) -> Checked {
        Checked(node: GesComputedNode(op, field.type, unit: field.kind == .quantity ? field.unit : .none, path: path), optional: field.isOptional)
    }

    private static func typeNode(_ type: GesResolvedType, _ op: GesComputedOp, _ path: [String]) -> (GesComputedNode, GesResolvedDeclaration?, [GesResolvedType]?) {
        switch type.kind {
        case .number: return (GesComputedNode(op, .number, path: path), nil, nil)
        case .quantity: return (GesComputedNode(op, .number, unit: type.unit, path: path), nil, nil)
        case .percentage: return (GesComputedNode(op, .percentage, path: path), nil, nil)
        case .boolean: return (GesComputedNode(op, .boolean, path: path), nil, nil)
        case .list where type.element != nil: return (GesComputedNode(op, .list, path: path), nil, type.element)
        case .reference where type.reference?.kind == .schema: return (GesComputedNode(op, .record, path: path), type.reference, nil)
        // Text, tags, spatial values and other types are not read; only has value may name them, and it rejects them.
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
