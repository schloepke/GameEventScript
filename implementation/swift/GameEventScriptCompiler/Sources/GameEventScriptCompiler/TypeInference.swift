// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

extension GesAstValidator {
    // The result types of callables inferred during one top-level inference, as CallableResults of the C# validator.
    // A chain of calls is inferred with an explicit stack instead of native recursion: while a callable body is
    // inferred, a call of a callable without a result records it as missing, and the body is inferred again once the
    // missing callable has its result. A call of a callable still on the stack is a cycle and stays unknown.
    final class CallableResults {
        var results: [String: String] = [:]
        var pending: [String] = []
        var pendingKeys: Set<String> = []
        var missing: String?
    }

    func infer(_ expression: GesExpression, _ types: [String: String], _ callables: CallableResults = .init()) -> String {
        func child(_ e: GesExpression) -> String { infer(e, types, callables) }

        func predicate(_ type: String) -> Bool { type == "boolean" || type == "nothing" }

        switch expression.kind {
        case .assembly(let block): return block.predicate ? "boolean" : "other"
        case .literal(let value):
            if value.hasUnit { return "quantity:" + String(describing: value.unit) }
            return String(describing: value.valueKind)
        case .name(let name): return types[name] ?? "unknown"
        case .constant(let name): return constants[name].map(child) ?? "unknown"
        case .constructor("number", let args) where args.count == 2: return numberWithUnit(args)
        case .cast(_, let type), .constructor(let type, _): return type
        case .check, .predicate, .between: return "boolean"
        case .member(let value, let name): return records[child(value)]?.fields.first { $0.name == name }?.type ?? "unknown"
        case .selector(_, let selection): return ["any", "all", "contains"].contains(selection.operation) ? "boolean" : "unknown"
        case .call(let name, let args):
            let key = callableSignature(name, args.map(\.label))
            guard let d = definitions[key] else { return "unknown" }
            if d.kind == "predicate" { return "boolean" }
            return callResult(key, callables)
        case .send: return "boolean"
        case .unary(let op, let value):
            if [.predicate, .empty, .hasValue, .chance].contains(op) { return "boolean" }
            if op == .parse || op == .parseStrict { return "unknown" }
            if op == .negate { return child(value) }
            // abs and rounding keep a scalar Quantity's unit, so a statically unknown operand leaves the result unknown too.
            // Only abs accepts a vector, whose magnitude keeps a unit the nominal type does not expose; rounding a vector
            // is nothing, so it infers as a unitless number.
            if [.abs, .floor, .ceil, .truncate, .roundHalfEven, .roundHalfUp, .roundHalfDown].contains(op) {
                let input = child(value)
                if input.hasPrefix("quantity:") { return input }
                return input == "unknown" || input == "other" || op == .abs && input == "vector" ? "other" : "number"
            }
            if op == .not { return predicate(child(value)) ? "boolean" : "unknown" }
            return "number"
        case .binary(let op, let a, let b):
            if [.equal, .notEqual, .less, .greater, .lessOrEqual, .greaterOrEqual, .contains, .containsValue, .startsWith, .endsWith].contains(op) { return "boolean" }
            if [.and, .or, .xor, .implies, .default].contains(op) {
                let left = child(a)
                let right = child(b)
                return predicate(left) && predicate(right) ? left == "nothing" && right == "nothing" && op == .default ? "nothing" : "boolean" : "unknown"
            }
            return "other"
        case .choice(let branches, let fallback):
            let results = branches.map { child($0.0) } + [child(fallback)]
            return results.allSatisfy(predicate) ? results.contains("boolean") ? "boolean" : "nothing" : "unknown"
        case .seeded(_, let value): return child(value)
        case .intrinsic(let name, let args):
            if ["hypot", "distance", "clamp", "min", "max"].contains(name) { return "other" }
            return name == "normalize" || name == "cross" && [2, 6].contains(args.count) ? "vector" : "number"
        default: return "other"
        }
    }

    // The inferred result type of a function; see CallableResults.
    private func callResult(_ key: String, _ callables: CallableResults) -> String {
        if let known = callables.results[key] { return known }
        if definitions[key]?.expression == nil || callables.pendingKeys.contains(key) { return "unknown" }
        if !callables.pending.isEmpty {
            if callables.missing == nil { callables.missing = key }
            return "unknown"
        }
        callables.pending.append(key)
        callables.pendingKeys.insert(key)
        while let current = callables.pending.last {
            let d = definitions[current]!
            let declared = Dictionary(d.parameters.compactMap { p in p.type.map { (p.name, $0) } }, uniquingKeysWith: { _, last in last })
            callables.missing = nil
            let result = infer(d.expression!, declared, callables)
            if let missing = callables.missing {
                callables.missing = nil
                callables.pending.append(missing)
                callables.pendingKeys.insert(missing)
                continue
            }
            callables.results[current] = result
            callables.pending.removeLast()
            callables.pendingKeys.remove(current)
        }
        return callables.results[key] ?? "unknown"
    }

    // `:Number(value, unit)` has the quantity type of a literal unit text; an unknown unit name or a literal value with
    // a conflicting unit yields nothing, and a computed unit leaves the type unknown, as in C#.
    private func numberWithUnit(_ args: [GesArgument]) -> String {
        guard let positions = GesLiteralForms.positions("number", args.map(\.label)) else { return "unknown" }
        let valueIndex = positions[0] == 0 ? 0 : 1
        guard case .literal(let text) = args[1 - valueIndex].value.kind, text.valueKind == .text, let name = text.textValue else { return "unknown" }
        guard let unit = name.isEmpty ? GesQuantityUnit.none : GesQuantityUnit(quantityName: name) else { return "nothing" }
        if case .literal(let value) = args[valueIndex].value.kind, value.hasUnit, value.unit != unit { return "nothing" }
        return unit == .none ? "number" : "quantity:" + unit.rawValue
    }

    func validateSeed(_ expression: GesExpression, _ types: [String: String]) {
        if infer(expression, types) == "integer" { return }

        func explicit(_ e: GesExpression) -> Bool {
            switch e.kind {
            case .cast(_, "number"), .constructor("number", _): return true
            case .name(let name): return types[name] == "number"
            case .unary(.negate, let value): return explicit(value)
            case .literal(let value):
                return !value.hasUnit && value.valueKind == .float && value.asNumber.isFinite && value.asNumber.rounded(.towardZero) == value.asNumber && value.asNumber >= -9_223_372_036_854_775_808 && value.asNumber < 9_223_372_036_854_775_808
            default: return false
            }
        }

        if !explicit(expression) { report("validate.invalidTypeConstructor", expression.location, symbol: "Number", kind: .type, message: "A dynamically typed random seed requires an explicit as :Number conversion.") }
    }
}
