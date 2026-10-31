// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptCompiler
import GameEventScriptRuntime

// Binds the syntax tree of every valid source and compares the result with the parser's semantic module, including
// source ranges and depths. Each source reports `equal`, `invalid` for sources with syntax errors, or its first
// difference.
enum SyntaxBinding {
    static func run(_ input: Input) throws -> [String: Any] {
        var sources: [String: Any] = [:]
        for test in input.values("cases") {
            let id = try test.text("id")
            for (index, source) in test.values("sources").enumerated() {
                let key = id + "#" + String(index)
                guard let (parsed, bound) = GesProgramBuilder.syntaxBinding(try source.text("text"), sourceID: UInt32(index)) else {
                    sources[key] = "invalid"
                    continue
                }
                switch bound {
                case .failure(let error): sources[key] = "binder failed: \(error)"
                case .success(let module): sources[key] = firstDifference(dump(parsed), dump(module)) ?? "equal"
                }
            }
        }
        return sources
    }

    private static func firstDifference(_ expected: [String], _ actual: [String]) -> String? {
        for index in 0..<max(expected.count, actual.count) {
            let left = index < expected.count ? expected[index] : "<end>"
            let right = index < actual.count ? actual[index] : "<end>"
            if left != right { return "parser \(left) | binder \(right)" }
        }
        return nil
    }

    private static func dump(_ root: Any) -> [String] {
        var lines: [String] = []
        dump(root, "module", &lines)
        return lines
    }

    // Stored properties, enum payloads and collections in order; values print with their kind.
    private static func dump(_ value: Any, _ path: String, _ lines: inout [String]) {
        if let value = value as? GesValue {
            lines.append("\(path) = \(value.valueKind) \(value)")
            return
        }
        let mirror = Mirror(reflecting: value)
        switch mirror.displayStyle {
        case .optional:
            if let some = mirror.children.first { dump(some.value, path, &lines) } else { lines.append(path + " = nil") }
        case .collection, .set, .dictionary:
            for (index, child) in mirror.children.enumerated() { dump(child.value, path + "[\(index)]", &lines) }
            lines.append(path + ".count = \(mirror.children.count)")
        case .enum:
            lines.append(path + " : " + (mirror.children.first?.label ?? String(describing: value)))
            for child in mirror.children { dump(child.value, path + "." + (child.label ?? "_"), &lines) }
        case .struct, .class, .tuple:
            lines.append(path + " : " + String(describing: mirror.subjectType))
            for (index, child) in mirror.children.enumerated() { dump(child.value, path + "." + (child.label ?? String(index)), &lines) }
        default:
            lines.append(path + " = " + String(reflecting: value))
        }
    }
}
