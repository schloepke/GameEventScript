// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

extension GesCompiler {
    func infer(_ expression: GesExpression, _ types: [String: String], _ visiting: Set<String> = []) -> String {
        func child(_ e: GesExpression) -> String { infer(e, types, visiting) }
        func predicate(_ type: String) -> Bool { type == "boolean" || type == "nothing" }
        switch expression.kind {
        case .literal(let value):
            if value.hasUnit { return "quantity:" + String(describing: value.unit) }
            return String(describing: value.kind)
        case .name(let name): return types[name] ?? "unknown"
        case .constant(let name): return constants[name].map(child) ?? "unknown"
        case .cast(_, let type), .constructor(let type, _): return type
        case .check, .predicate: return "boolean"
        case .member(let value, let name):
            return records[child(value)]?.fields.first { $0.name == name }?.type ?? "unknown"
        case .selector(_, let selection):
            return ["any", "all", "contains"].contains(selection.operation) ? "boolean" : "unknown"
        case .call(let name, let args):
            let key = signature(name, args.map(\.label))
            guard let d = definitions[key] else { return "unknown" }
            if d.kind == "predicate" { return "boolean" }
            guard !visiting.contains(key), let value = d.expression else { return "unknown" }
            let declared = Dictionary(
                uniqueKeysWithValues: d.parameters.compactMap { p in p.type.map { (p.name, $0) } })
            return infer(value, declared, visiting.union([key]))
        case .unary(let op, let value):
            if ["predicate", "empty", "hasValue", "chance"].contains(op) { return "boolean" }
            if op == "parse" { return "unknown" }
            if op == "-" { return child(value) }
            if op == "!" { return predicate(child(value)) ? "boolean" : "unknown" }
            return "number"
        case .binary(let op, let a, let b):
            if ["=", "<>", "<", ">", "<=", ">=", "in", "not in", "inValues", "startsWith", "endsWith"].contains(op) {
                return "boolean"
            }
            if ["and", "or", "xor", "->", "default"].contains(op) {
                let left = child(a)
                let right = child(b)
                return predicate(left) && predicate(right)
                    ? left == "nothing" && right == "nothing" && op == "default" ? "nothing" : "boolean" : "unknown"
            }
            return "other"
        case .choice(let branches, let fallback):
            let results = branches.map { child($0.0) } + [child(fallback)]
            return results.allSatisfy(predicate) ? results.contains("boolean") ? "boolean" : "nothing" : "unknown"
        case .seeded(_, let value): return child(value)
        case .intrinsic(let name, let args):
            return name == "normalize" || name == "cross" && [2, 6].contains(args.count) ? "vector" : "number"
        default: return "other"
        }
    }
    func validateSeed(_ expression: GesExpression, _ types: [String: String]) throws {
        if infer(expression, types) == "integer" { return }
        func explicit(_ e: GesExpression) -> Bool {
            switch e.kind {
            case .cast(_, "number"), .constructor("number", _): return true
            case .name(let name): return types[name] == "number"
            case .unary("-", let value): return explicit(value)
            case .literal(let value):
                return !value.hasUnit && value.kind == .float && value.asNumber.isFinite
                    && value.asNumber.rounded(.towardZero) == value.asNumber
                    && value.asNumber >= -9_223_372_036_854_775_808 && value.asNumber < 9_223_372_036_854_775_808
            default: return false
            }
        }
        if !explicit(expression) {
            throw error("validate.invalidTypeConstructor", expression.location, symbol: "Number", kind: .type)
        }
    }
}
