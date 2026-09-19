// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

enum ConformanceExecutionError: Error {
    case invalidInput(String)
}

extension ConformanceData {
    func required(_ key: String) throws -> ConformanceData {
        guard let value = self[key] else { throw ConformanceExecutionError.invalidInput("Missing \(key)") }
        return value
    }

    func text(_ key: String) throws -> String {
        guard let value = self[key]?.stringValue ?? self[key]?.numberValue else {
            throw ConformanceExecutionError.invalidInput("Expected scalar \(key)")
        }
        return value
    }

    func values(_ key: String) -> [ConformanceData] { self[key]?.arrayValue ?? [] }
}

enum ConformanceRuntimeValueCodec {
    static func unit(_ node: ConformanceData) throws -> GesUnit {
        switch node["unit"]?.stringValue?.replacingColonPrefix() {
        case nil, "none": return .none
        case "m", "meter": return .meter
        case "s", "second": return .second
        case "°", "degree": return .degree
        default: throw ConformanceExecutionError.invalidInput("Unknown unit")
        }
    }

    static func number(_ text: String) throws -> Double {
        switch text {
        case "NaN": return .nan
        case "Infinity": return .infinity
        case "-Infinity": return -.infinity
        default:
            guard let value = Double(text) else { throw ConformanceExecutionError.invalidInput("Invalid Binary64") }
            return value
        }
    }

    static func integer(_ text: String) throws -> Int64 {
        guard let value = Int64(text) else { throw ConformanceExecutionError.invalidInput("Invalid Int64") }
        return value
    }

    static func signature(_ node: ConformanceData) throws -> GameEventScriptMessageSignature {
        try GameEventScriptMessageSignature(
            name: node.text("name"), parameters: node.values("parameters").map { $0.stringValue })
    }

    static func message(_ node: ConformanceData) throws -> GameEventScriptMessage {
        let arguments = try node.values("args").map { argument in
            try GameEventScriptMessageArgument(
                name: argument["name"]?.stringValue, value: decode(argument.required("value")))
        }
        return try GameEventScriptMessage(
            name: node.text("name"), arguments: arguments, tags: node.values("tags").compactMap(\.stringValue))
    }

    static func decode(_ node: ConformanceData, mutateSource: Bool = false) throws -> GesValue {
        let type = try node.text("type")
        let unit = try unit(node)
        switch type {
        case ":Nothing": return .nothing
        case ":Boolean": return .boolean(node["value"]?.boolValue == true || node["value"]?.stringValue == "true")
        case ":Number.int64", ":Quantity.int64": return try .integer(integer(node.text("value")), unit: unit)
        case ":Number.binary64", ":Quantity.binary64": return try .float(number(node.text("value")), unit: unit)
        case ":Percentage": return try .percentage(number(node.text("value")))
        case ":Text": return try .text(node.text("value"))
        case ":Tag": return try .tag(node.text("value"))
        case ":Vector":
            return try .vector(
                x: number(node.text("x")), y: number(node.text("y")), z: number(node.text("z")), unit: unit)
        case ":Point":
            return try .point(
                x: number(node.text("x")), y: number(node.text("y")), z: number(node.text("z")), unit: unit)
        case ":List":
            var items = try node.values("items").map { try decode($0) }
            let result = GesValue.list(items)
            if mutateSource && !items.isEmpty { items[0] = .nothing }
            return result
        case ":Dice":
            var rolls = try node.values("rolls").map { value in
                guard let result = Int32(value.numberValue ?? value.stringValue ?? "") else {
                    throw ConformanceExecutionError.invalidInput("Invalid roll")
                }
                return result
            }
            let result = GesValue.dice(rolls)
            if mutateSource && !rolls.isEmpty { rolls[0] = .min }
            return result
        case ":Range.int64":
            return try .integerRange(
                from: integer(node.text("from")), to: integer(node.text("to")), step: integer(node.text("step")))
        case ":Range.binary64":
            return try .floatRange(
                from: number(node.text("from")), to: number(node.text("to")), step: number(node.text("step")))
        case ":Message": return try .message(message(node.required("message")))
        default:
            var entries = try node.values("entries").map {
                try GesMapEntry(key: $0.text("key"), value: decode($0.required("value")))
            }
            let result =
                type == ":Map"
                ? GesValue.map(entries) : GesValue.record(typeName: String(type.dropFirst()), entries: entries)
            if mutateSource && !entries.isEmpty { entries[0] = GesMapEntry(key: "mutated", value: .nothing) }
            return result
        }
    }

    static func binaryEqual(_ left: Double, _ right: Double, comparison: ConformanceData?) -> Bool {
        if left.isNaN || right.isNaN { return left.isNaN && right.isNaN }
        if comparison?["binary64"]?["mode"]?.stringValue != "ulp" { return left.bitPattern == right.bitPattern }
        if left == right { return true }
        if !left.isFinite || !right.isFinite { return false }
        let mask: UInt64 = 0x8000_0000_0000_0000
        func ordered(_ number: Double) -> UInt64 {
            number.bitPattern & mask == 0 ? number.bitPattern | mask : ~number.bitPattern
        }
        let a = ordered(left)
        let b = ordered(right)
        let limit = UInt64(comparison?["binary64"]?["maxUlps"]?.numberValue ?? "0") ?? 0
        return (a >= b ? a - b : b - a) <= limit
    }

    static func messagesEqual(
        _ expected: ConformanceData, _ actual: GameEventScriptMessage, comparison: ConformanceData?
    ) throws -> Bool {
        guard scalarEqual(try expected.text("name"), actual.name) else { return false }
        let tags = expected.values("tags").compactMap(\.stringValue)
        guard tags.count == actual.tags.count, zip(tags, actual.tags).allSatisfy({ scalarEqual($0, $1) }) else {
            return false
        }
        let args = expected.values("args")
        guard args.count == actual.arguments.count else { return false }
        for (index, argument) in args.enumerated() {
            guard scalarEqual(argument["name"]?.stringValue ?? "_", actual.arguments.nameAt(index)) else {
                return false
            }
            if try !equal(argument.required("value"), actual.arguments[index], comparison: comparison) { return false }
        }
        return true
    }

    // Compare authored transport kinds before invoking any normalizing Runtime factory.
    static func equal(_ expected: ConformanceData, _ actual: GesValue, comparison: ConformanceData?) throws -> Bool {
        let type = try expected.text("type")
        let expectedUnit = try unit(expected)
        switch type {
        case ":Nothing": return actual.isNothing
        case ":Boolean": return actual.kind == .boolean && (expected["value"]?.boolValue == true) == actual.asBoolean
        case ":Number.int64", ":Quantity.int64":
            return try actual.kind == .integer && expectedUnit == actual.unit
                && integer(expected.text("value")) == actual.integerValue
        case ":Number.binary64", ":Quantity.binary64", ":Percentage":
            let number = try number(expected.text("value"))
            if number.isNaN { return actual.isNothing }
            return actual.kind == (type == ":Percentage" ? .percentage : .float) && expectedUnit == actual.unit
                && binaryEqual(number, actual.asNumber, comparison: comparison)
        case ":Text", ":Tag":
            return try actual.kind == (type == ":Text" ? .text : .tag)
                && scalarEqual(expected.text("value"), actual.textValue ?? "")
        case ":Vector", ":Point":
            let x = try number(expected.text("x"))
            let y = try number(expected.text("y"))
            let z = try number(expected.text("z"))
            if x.isNaN || y.isNaN || z.isNaN { return actual.isNothing }
            guard actual.kind == (type == ":Vector" ? .vector : .point), expectedUnit == actual.unit,
                let spatial = actual.spatialValue
            else { return false }
            return binaryEqual(x, spatial.x, comparison: comparison)
                && binaryEqual(y, spatial.y, comparison: comparison)
                && binaryEqual(z, spatial.z, comparison: comparison)
        case ":List":
            guard let items = actual.listValue, items.count == expected.values("items").count else { return false }
            for (a, b) in zip(expected.values("items"), items) {
                if try !equal(a, b, comparison: comparison) { return false }
            }
            return true
        case ":Dice":
            let rolls = try expected.values("rolls").map { value in
                guard let roll = Int32(value.numberValue ?? value.stringValue ?? "") else {
                    throw ConformanceExecutionError.invalidInput("Invalid expected roll")
                }
                return roll
            }
            return actual.diceRolls == rolls
        case ":Range.int64":
            guard let range = actual.integerRangeValue else { return false }
            return try range.from == integer(expected.text("from")) && range.to == integer(expected.text("to"))
                && range.step == integer(expected.text("step"))
        case ":Range.binary64":
            let from = try number(expected.text("from"))
            let to = try number(expected.text("to"))
            let step = try number(expected.text("step"))
            if from.isNaN || to.isNaN || step.isNaN { return actual.isNothing }
            guard let range = actual.floatRangeValue else { return false }
            return binaryEqual(from, range.from, comparison: comparison)
                && binaryEqual(to, range.to, comparison: comparison)
                && binaryEqual(step, range.step, comparison: comparison)
        case ":Message":
            guard let message = actual.messageValue else { return false }
            return try messagesEqual(expected.required("message"), message, comparison: comparison)
        default:
            guard actual.kind == (type == ":Map" ? .map : .record), let entries = actual.mapEntries else {
                return false
            }
            if type != ":Map" && !scalarEqual(String(type.dropFirst()), actual.customTypeName ?? "") { return false }
            var normalized: [([UInt32], ConformanceData)] = []
            for entry in expected.values("entries") {
                let key = try entry.text("key").unicodeScalars.map(\.value)
                normalized.removeAll { $0.0 == key }
                normalized.append((key, try entry.required("value")))
            }
            guard normalized.count == entries.count else { return false }
            for (key, value) in normalized {
                guard let match = entries.first(where: { $0.key.unicodeScalars.map(\.value) == key }),
                    try equal(value, match.value, comparison: comparison)
                else { return false }
            }
            return true
        }
    }

    static func scalarEqual(_ a: String, _ b: String) -> Bool { a.unicodeScalars.elementsEqual(b.unicodeScalars) }
}

extension String {
    fileprivate func replacingColonPrefix() -> String { hasPrefix(":") ? String(dropFirst()) : self }
}
