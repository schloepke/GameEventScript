// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

extension GesAstValidator {
    func infer(_ expression: GesExpression, _ types: [String: String], _ visiting: Set<String> = []) -> String {
        func child(_ e: GesExpression) -> String { infer(e, types, visiting) }

        func predicate(_ type: String) -> Bool { type == "boolean" || type == "nothing" }

        switch expression.kind {
        case .assembly(let block): return block.predicate ? "boolean" : "other"
        case .literal(let value):
            if value.hasUnit { return "quantity:" + String(describing: value.unit) }
            return String(describing: value.valueKind)
        case .name(let name): return types[name] ?? "unknown"
        case .constant(let name): return constants[name].map(child) ?? "unknown"
        case .cast(_, let type), .constructor(let type, _): return type
        case .check, .predicate, .between: return "boolean"
        case .member(let value, let name): return records[child(value)]?.fields.first { $0.name == name }?.type ?? "unknown"
        case .selector(_, let selection): return ["any", "all", "contains"].contains(selection.operation) ? "boolean" : "unknown"
        case .call(let name, let args):
            let key = callableSignature(name, args.map(\.label))
            guard let d = definitions[key] else { return "unknown" }
            if d.kind == "predicate" { return "boolean" }
            guard !visiting.contains(key), let value = d.expression else { return "unknown" }
            let declared = Dictionary(d.parameters.compactMap { p in p.type.map { (p.name, $0) } }, uniquingKeysWith: { _, last in last })
            return infer(value, declared, visiting.union([key]))
        case .send: return "boolean"
        case .unary(let op, let value):
            if [.predicate, .empty, .hasValue, .chance].contains(op) { return "boolean" }
            if op == .parse { return "unknown" }
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
