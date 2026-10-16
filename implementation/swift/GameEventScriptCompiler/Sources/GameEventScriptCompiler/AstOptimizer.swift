// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
@_spi(Compiler) import GameEventScriptRuntime

// Replaces constant references and folds constant subexpressions after validation, before lowering.
// Mirrors GesAstOptimizer of the C# reference compiler rule for rule, so both ports lower the same tree.
struct GesAstOptimizer {
    let constants: [String: GesExpression]

    func definition(_ d: GesDefinition) -> GesDefinition {
        .init(
            kind: d.kind,
            name: d.name,
            parameters: d.parameters,
            expression: d.expression.map(expression),
            statements: statements(d.statements),
            required: d.required,
            excluded: d.excluded,
            location: d.location
        )
    }

    func record(_ r: GesRecord) -> GesRecord {
        .init(
            name: r.name,
            fields: r.fields.map { field in
                .init(
                    name: field.name,
                    type: field.type,
                    label: field.label,
                    minimum: field.minimum.map(expression),
                    maximum: field.maximum.map(expression),
                    computed: field.computed.map(expression),
                    location: field.location
                )
            },
            location: r.location
        )
    }

    func module(_ m: GesModule) -> GesModule {
        .init(name: m.name, definitions: m.definitions.map(definition), constants: m.constants, records: m.records.map(record), location: m.location)
    }

    func statements(_ list: [GesStatement]) -> [GesStatement] { list.map(statement) }

    func statement(_ s: GesStatement) -> GesStatement {
        let kind: GesStatement.Kind
        switch s.kind {
        case .assembly: return s
        case .expression(let e): kind = .expression(expression(e))
        case .letBinding(let name, let e): kind = .letBinding(name, expression(e))
        case .publish(let publish, let message, let tags): kind = .publish(publish, expression(message), tags.map(expression))
        case .condition(let conditions, let then, let otherwise):
            kind = .condition(conditions.map { ($0.0, expression($0.1)) }, statements(then), statements(otherwise))
        case .loop(let names, let source, let flag, let body): kind = .loop(names, expression(source), flag, statements(body))
        case .seeded(let seed, let body): kind = .seeded(expression(seed), statements(body))
        }
        return .init(kind: kind, location: s.location)
    }

    func expression(_ e: GesExpression) -> GesExpression {
        let kind: GesExpression.Kind
        switch e.kind {
        case .assembly, .literal, .name, .handler, .dice, .series: return e
        case .constant(let name):
            guard let value = constants[name] else { return e }
            let substituted = GesExpression(value.kind, e.location, depth: e.depth)
            substituted.decimalLiteral = value.decimalLiteral
            return fold(substituted)
        case .unary(let op, let a): kind = .unary(op, expression(a))
        case .binary(let op, let a, let b): kind = .binary(op, expression(a), expression(b))
        case .between(let a, let b, let c): kind = .between(expression(a), expression(b), expression(c))
        case .call(let name, let args): kind = .call(name, arguments(args))
        case .extensionCall(let ns, let name, let args): kind = .extensionCall(ns, name, arguments(args))
        case .constructor(let type, let args): kind = .constructor(type, arguments(args))
        case .send(let publish, let message, let tags, let delay): kind = .send(publish, expression(message), tags.map(expression), delay.map(expression))
        case .message(let name, let args): kind = .message(name, arguments(args))
        case .list(let items): kind = .list(items.map(expression))
        case .map(let entries): kind = .map(entries.map { ($0.0, expression($0.1)) })
        case .member(let target, let name): kind = .member(expression(target), name)
        case .selector(let target, var selector):
            selector.expressions = selector.operation == "objectMatch" ? selector.expressions.map(pattern) : selector.expressions.map(expression)
            kind = .selector(expression(target), selector)
        case .combined(let mode, let sources): kind = .combined(mode, sources.map(expression))
        case .cast(let value, let type): kind = .cast(expression(value), type)
        case .check(let value, let type): kind = .check(expression(value), type)
        case .predicate(let value, let name): kind = .predicate(value.map(expression), name)
        case .intrinsic(let name, let args): kind = .intrinsic(name, args.map(expression))
        case .range(let from, let to, let step): kind = .range(expression(from), expression(to), step.map(expression))
        case .random(let from, let to): kind = .random(expression(from), expression(to))
        case .seeded(let seed, let body): kind = .seeded(expression(seed), expression(body))
        case .choice(let branches, let otherwise): kind = .choice(branches.map { (expression($0.0), expression($0.1)) }, expression(otherwise))
        case .generated(let a, let b, let source, let flag, let predicate, let projection):
            kind = .generated(a, b, expression(source), flag, predicate.map(expression), expression(projection))
        }
        let optimized = GesExpression(kind, e.location, depth: e.depth)
        optimized.decimalLiteral = e.decimalLiteral
        return fold(optimized)
    }

    // An object pattern is not a map literal: as in C#, only its entry values are optimized and nested patterns recurse.
    func pattern(_ e: GesExpression) -> GesExpression {
        guard case .map(let entries) = e.kind else { return expression(e) }
        let optimized = GesExpression(.map(entries.map { ($0.0, pattern($0.1)) }), e.location, depth: e.depth)
        optimized.decimalLiteral = e.decimalLiteral
        return optimized
    }

    func arguments(_ list: [GesArgument]) -> [GesArgument] { list.map { .init(label: $0.label, value: expression($0.value)) } }

    // Literals keep their source spelling metadata, such as the decimal form that selects continuous random bounds.
    func fold(_ e: GesExpression) -> GesExpression {
        if case .literal = e.kind { return e }
        guard let value = Self.evaluate(e), let literal = Self.literal(value, e.location, depth: e.depth) else { return e }
        return literal
    }

    static func evaluate(_ e: GesExpression) -> GesValue? {
        switch e.kind {
        case .literal(let value): return value
        case .list(let items):
            var values: [GesValue] = []
            for item in items {
                guard let value = evaluate(item) else { return nil }
                values.append(value)
            }
            return .list(values)
        case .map(let entries):
            var values: [GesValue] = []
            for entry in entries {
                guard let value = evaluate(entry.1) else { return nil }
                values.append(value)
            }
            return .map(keys: entries.map(\.0), values: values)
        case .cast(let source, let type):
            guard type == "number" || type == "numeric", let value = evaluate(source) else { return nil }
            return GesCompilerSupport.castNumber(value)
        case .unary(let op, let operand):
            guard let value = evaluate(operand) else { return nil }
            return unary(op, value)
        case .binary(let op, let left, let right):
            guard let a = evaluate(left), let b = evaluate(right) else { return nil }
            return binary(op, a, b)
        default: return nil
        }
    }

    static func indeterminate(_ value: GesValue) -> Bool { [.nothing, .list, .map, .dice].contains(value.valueKind) }

    static func roundable(_ value: GesValue) -> Bool { [.integer, .float, .percentage, .boolean].contains(value.valueKind) }

    static func unary(_ op: GesUnaryOperator, _ operand: GesValue) -> GesValue? {
        let kind = operand.valueKind
        switch op {
        case .not: return indeterminate(operand) ? .nothing : .boolean(!operand.asBoolean)
        case .hasValue: return .boolean(operand.hasValue)
        case .empty: return .boolean(!operand.hasValue)
        case .negate:
            guard kind == .integer, let integer = operand.integerValue, integer != .min else { return nil }
            return .integer(-integer, unit: operand.unit)
        case .abs:
            if kind == .integer, let integer = operand.integerValue {
                return integer == .min ? .float(-Double(integer), unit: operand.unit) : .integer(Swift.abs(integer), unit: operand.unit)
            }
            if kind == .percentage { return .percentage(Swift.abs(operand.asNumber)) }
            if kind == .float { return .float(Swift.abs(operand.asNumber), unit: operand.unit) }
            return nil
        case .naturalLog:
            return !operand.hasUnit && roundable(operand) ? .float(Foundation.log(operand.asNumber)) : .nothing
        case .exp:
            return !operand.hasUnit && roundable(operand) ? .float(Foundation.exp(operand.asNumber)) : .nothing
        case .floor, .ceil, .truncate, .roundHalfEven, .roundHalfUp, .roundHalfDown:
            guard roundable(operand) else { return .nothing }
            let number = operand.asNumber
            let rounded: Double
            switch op {
            case .floor: rounded = number.rounded(.down)
            case .ceil: rounded = number.rounded(.up)
            case .truncate: rounded = number.rounded(.towardZero)
            case .roundHalfEven: rounded = number.rounded(.toNearestOrEven)
            case .roundHalfUp: rounded = number.rounded(.toNearestOrAwayFromZero)
            default: rounded = roundHalfTowardZero(number)
            }
            return .integer(saturated(rounded))
        case .degreeToRadians:
            guard !(operand.hasUnit && operand.unit != .degree), roundable(operand), operand.asNumber.isFinite else { return .nothing }
            return .float(operand.asNumber / 180 * Double.pi)
        case .degreeFromRadians:
            guard !operand.hasUnit, roundable(operand), operand.asNumber.isFinite else { return .nothing }
            return .float(operand.asNumber / Double.pi * 180, unit: .degree)
        case .wrapDegree:
            guard !(operand.hasUnit && operand.unit != .degree), roundable(operand), operand.asNumber.isFinite else { return .nothing }
            var wrapped = operand.asNumber.truncatingRemainder(dividingBy: 360)
            if wrapped < 0 { wrapped += 360 }
            return .float(wrapped == 360 ? 0 : wrapped, unit: .degree)
        default: return nil
        }
    }

    static func binary(_ op: GesBinaryOperator, _ left: GesValue, _ right: GesValue) -> GesValue? {
        switch op {
        case .default: return left.hasValue ? left : right
        case .or:
            if !indeterminate(left) && left.asBoolean || !indeterminate(right) && right.asBoolean { return .boolean(true) }
            return indeterminate(left) || indeterminate(right) ? .nothing : .boolean(false)
        case .and:
            if !indeterminate(left) && !left.asBoolean || !indeterminate(right) && !right.asBoolean { return .boolean(false) }
            return indeterminate(left) || indeterminate(right) ? .nothing : .boolean(true)
        case .xor:
            if indeterminate(left) || indeterminate(right) { return .nothing }
            return .boolean(left.asBoolean != right.asBoolean)
        case .implies:
            if !indeterminate(left) && !left.asBoolean || !indeterminate(right) && right.asBoolean { return .boolean(true) }
            return indeterminate(left) || indeterminate(right) ? .nothing : .boolean(false)
        case .power: return GesCompilerSupport.arithmetic(.power, left, right)
        default: break
        }
        if left.isNothing || right.isNothing {
            // List subtraction treats Nothing as an element to remove, not scalar propagation.
            if op == .subtract && left.valueKind == .list { return nil }
            return .nothing
        }
        let sameUnit = left.unit == right.unit
        let bothIntegers = left.valueKind == .integer && right.valueKind == .integer
        if bothIntegers && sameUnit, let a = left.integerValue, let b = right.integerValue {
            switch op {
            case .integerDivide:
                if let quotient = floorDivideExact(a, b) { return .integer(quotient, unit: divideUnit(left.unit)) }
                return .float((Double(a) / Double(b)).rounded(.down), unit: divideUnit(left.unit))
            case .modulo: return b == 0 ? .nothing : .integer(modulo(a, b), unit: left.unit)
            case .remainder: return b == 0 ? .nothing : .integer(remainder(a, b), unit: left.unit)
            default: break
            }
        }
        guard let l = finite(left), let r = finite(right) else { return nil }
        switch op {
        case .equal, .notEqual, .less, .greater, .lessOrEqual, .greaterOrEqual:
            guard bothIntegers && sameUnit, let a = left.integerValue, let b = right.integerValue else { return nil }
            switch op {
            case .equal: return .boolean(a == b)
            case .notEqual: return .boolean(a != b)
            case .less: return .boolean(a < b)
            case .greater: return .boolean(a > b)
            case .lessOrEqual: return .boolean(a <= b)
            default: return .boolean(a >= b)
            }
        case .add: return add(left, right, l, r, subtract: false)
        case .subtract: return add(left, right, l, r, subtract: true)
        case .multiply: return multiply(left, right, l, r)
        case .divide where r != 0 && sameUnit:
            if left.valueKind == .percentage && [.integer, .float].contains(right.valueKind) { return .percentage(l / r) }
            return number(l / r, divideUnit(left.unit), preferInteger: false)
        case .integerDivide where r != 0 && sameUnit:
            return number((l / r).rounded(.down), divideUnit(left.unit), preferInteger: true)
        case .modulo where r != 0 && sameUnit:
            var rest = l.truncatingRemainder(dividingBy: r)
            if rest != 0 && (rest < 0 && r > 0 || rest > 0 && r < 0) { rest += r }
            return number(rest, left.unit, preferInteger: bothIntegers)
        case .remainder where r != 0 && sameUnit:
            return number(l.truncatingRemainder(dividingBy: r), left.unit, preferInteger: bothIntegers)
        default: return nil
        }
    }

    static func add(_ left: GesValue, _ right: GesValue, _ l: Double, _ r: Double, subtract: Bool) -> GesValue? {
        if left.valueKind == .integer && right.valueKind == .integer && left.unit == right.unit, let a = left.integerValue, let b = right.integerValue {
            let (result, overflow) = subtract ? a.subtractingReportingOverflow(b) : a.addingReportingOverflow(b)
            return overflow ? .float(subtract ? Double(a) - Double(b) : Double(a) + Double(b), unit: left.unit) : .integer(result, unit: left.unit)
        }
        if left.unit == right.unit && left.valueKind != .percentage && right.valueKind != .percentage {
            return number(subtract ? l - r : l + r, left.unit, preferInteger: false)
        }
        if left.valueKind == .percentage && right.valueKind == .percentage { return .percentage(subtract ? l - r : l + r) }
        if right.valueKind == .percentage && left.hasUnit { return number(subtract ? l - l * r : l + l * r, left.unit, preferInteger: false) }
        return nil
    }

    static func multiply(_ left: GesValue, _ right: GesValue, _ l: Double, _ r: Double) -> GesValue? {
        if left.valueKind == .integer && right.valueKind == .integer, let a = left.integerValue, let b = right.integerValue {
            if !left.hasUnit && !right.hasUnit {
                let (result, overflow) = a.multipliedReportingOverflow(by: b)
                return overflow ? .float(Double(a) * Double(b)) : .integer(result)
            }
            guard let unit = productUnit(left.unit, right.unit) else { return nil }
            return number(Double(a) * Double(b), unit, preferInteger: false)
        }
        if left.valueKind == .percentage && right.valueKind == .percentage { return .percentage(l * r) }
        if left.valueKind == .percentage { return number(l * r, right.unit, preferInteger: false) }
        if right.valueKind == .percentage { return number(l * r, left.unit, preferInteger: false) }
        guard let unit = productUnit(left.unit, right.unit) else { return nil }
        return number(l * r, unit, preferInteger: false)
    }

    static func finite(_ value: GesValue) -> Double? {
        switch value.valueKind {
        case .integer, .float, .percentage: return value.asNumber.isFinite ? value.asNumber : nil
        case .boolean: return value.asBoolean ? 1 : 0
        default: return nil
        }
    }

    static func number(_ value: Double, _ unit: GesQuantityUnit, preferInteger: Bool) -> GesValue {
        guard value.isFinite else { return .nothing }
        if preferInteger, value >= -9_223_372_036_854_775_808.0, value < 9_223_372_036_854_775_808.0, value == value.rounded(.towardZero) {
            return .integer(Int64(value), unit: unit)
        }
        return .float(value, unit: unit)
    }

    static func productUnit(_ left: GesQuantityUnit, _ right: GesQuantityUnit) -> GesQuantityUnit? {
        if left != .none && right != .none { return nil }
        return left == .none ? right : left
    }

    static func divideUnit(_ unit: GesQuantityUnit) -> GesQuantityUnit { unit != .none ? .none : unit }

    static func floorDivideExact(_ a: Int64, _ b: Int64) -> Int64? {
        if b == 0 || a == .min && b == -1 { return nil }
        let quotient = a / b
        return a % b != 0 && (a < 0) != (b < 0) ? quotient - 1 : quotient
    }

    static func remainder(_ a: Int64, _ b: Int64) -> Int64 { a == .min && b == -1 ? 0 : a % b }

    static func modulo(_ a: Int64, _ b: Int64) -> Int64 {
        let rest = remainder(a, b)
        return rest != 0 && (rest < 0) != (b < 0) ? rest + b : rest
    }

    static func roundHalfTowardZero(_ value: Double) -> Double {
        let absolute = Swift.abs(value)
        let floor = absolute.rounded(.down)
        let rounded = absolute - floor > 0.5 ? floor + 1 : floor
        return value < 0 ? -rounded : rounded
    }

    static func saturated(_ value: Double) -> Int64 {
        if value.isNaN { return 0 }
        if value >= 9_223_372_036_854_775_808.0 { return .max }
        if value < -9_223_372_036_854_775_808.0 { return .min }
        return Int64(value.rounded(.towardZero))
    }

    // Nested nodes of a folded literal have no source range in C#, so their instructions carry no source mapping.
    static func noRange(_ location: GesSourceLocation) -> GesSourceLocation { .init(sourceName: location.sourceName, moduleName: location.moduleName) }

    // The literal form of a folded value; nil keeps the original expression.
    static func literal(_ value: GesValue, _ location: GesSourceLocation, depth: Int) -> GesExpression? {
        switch value.valueKind {
        case .nothing, .boolean, .integer, .percentage, .text, .tag: return .init(.literal(value), location, depth: depth)
        case .float:
            guard value.asNumber.isFinite else { return nil }
            return .init(.literal(value), location, depth: depth)
        case .vector, .point:
            let components = [value.x, value.y, value.z].map { GesArgument(label: "_", value: .init(.literal(.float($0, unit: value.unit)), Self.noRange(location), depth: depth)) }
            return .init(.constructor(value.valueKind == .vector ? "vector" : "point", components), location, depth: depth)
        case .list:
            var items: [GesExpression] = []
            for item in value.asList {
                guard let literal = literal(item, Self.noRange(location), depth: depth) else { return nil }
                items.append(literal)
            }
            return .init(.list(items), location, depth: depth)
        case .map:
            guard let map = value.asMap else { return nil }
            var entries: [(String, GesExpression)] = []
            for index in 0..<map.length {
                guard let literal = literal(map.value(at: index), Self.noRange(location), depth: depth) else { return nil }
                entries.append((map.key(at: index), literal))
            }
            return .init(.map(entries), location, depth: depth)
        default: return nil
        }
    }
}
