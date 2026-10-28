// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

enum ConformanceExecutionError: Error { case invalidInput(String) }

extension Input {
    func required(_ key: String) throws -> Input {
        guard let value = self[key] else { throw ConformanceExecutionError.invalidInput("Missing \(key)") }
        return value
    }

    func text(_ key: String) throws -> String {
        guard let value = self[key]?.stringValue ?? self[key]?.numberValue else { throw ConformanceExecutionError.invalidInput("Expected scalar \(key)") }
        return value
    }

    func values(_ key: String) -> [Input] { self[key]?.arrayValue ?? [] }
}

enum RuntimeValues {
    static func unit(_ node: Input) throws -> GesQuantityUnit {
        switch node["unit"]?.stringValue?.replacingOccurrences(of: ":", with: "") {
        case nil, "none": return .none
        case "m": return .meter
        case "s": return .second
        case "°": return .degree
        case let name?:
            guard let unit = GesQuantityUnit(rawValue: name) else { throw ConformanceExecutionError.invalidInput("Unknown unit") }
            return unit
        }
    }

    static func parseNumber(_ text: String) throws -> Double {
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

    static func signature(_ node: Input) throws -> GesMessageSignature { try GesMessageSignature(name: node.text("name"), parameters: node.values("parameters").map { $0.stringValue }) }

    static func decodeMessage(_ node: Input) throws -> GameEventScriptMessage {
        let arguments = try node.values("args").map { argument in try GesMessageArgument(name: argument["name"]?.stringValue, value: decode(argument.required("value"))) }
        return try GameEventScriptMessage(name: node.text("name"), arguments: arguments, tags: node.values("tags").compactMap(\.stringValue))
    }

    static func decode(_ node: Input, mutateSource: Bool = false) throws -> GesValue {
        let type = try node.text("type")
        let unit = try unit(node)
        switch type {
        case ":Nothing": return .nothing
        case ":Boolean": return .boolean(node["value"]?.boolValue == true || node["value"]?.stringValue == "true")
        case ":Number.int64", ":Quantity.int64": return try .integer(integer(node.text("value")), unit: unit)
        case ":Number.binary64", ":Quantity.binary64": return try .float(parseNumber(node.text("value")), unit: unit)
        case ":Percentage": return try .percentage(parseNumber(node.text("value")))
        case ":Text": return try .text(node.text("value"))
        case ":Tag": return try .tag(node.text("value"))
        case ":Vector": return try .vector(x: parseNumber(node.text("x")), y: parseNumber(node.text("y")), z: parseNumber(node.text("z")), unit: unit)
        case ":Point": return try .point(x: parseNumber(node.text("x")), y: parseNumber(node.text("y")), z: parseNumber(node.text("z")), unit: unit)
        case ":List":
            var items = try node.values("items").map { try decode($0) }
            let result = GesValue.list(items)
            if mutateSource && !items.isEmpty { items[0] = .nothing }
            return result
        case ":Dice":
            var rolls = try node.values("rolls").map { value in
                guard let result = Int32(value.numberValue ?? value.stringValue ?? "") else { throw ConformanceExecutionError.invalidInput("Invalid roll") }
                return result
            }
            let result = GesValue.dice(rolls)
            if mutateSource && !rolls.isEmpty { rolls[0] = .min }
            return result
        case ":Range.int64": return try .integerRange(from: integer(node.text("from")), to: integer(node.text("to")), step: integer(node.text("step")))
        case ":Range.binary64": return try .floatRange(from: parseNumber(node.text("from")), to: parseNumber(node.text("to")), step: parseNumber(node.text("step")))
        case ":Message": return try .message(decodeMessage(node.required("message")))
        default:
            let nodes = node.values("entries")
            var keys = try nodes.map { try $0.text("key") }
            var values = try nodes.map { try decode($0.required("value")) }
            let result = type == ":Map" ? GesValue.map(keys: keys, values: values) : GesValue.record(typeName: String(type.dropFirst()), keys: keys, values: values)
            if mutateSource && !keys.isEmpty {
                keys[0] = "mutated"
                values[0] = .nothing
            }
            return result
        }
    }

}

extension RuntimeValues {
    static func numberText(_ value: Double) -> String {
        if value.isNaN { return "NaN" }
        if value == .infinity { return "Infinity" }
        if value == -.infinity { return "-Infinity" }
        return String(value)
    }

    static func encodeMessage(_ value: GameEventScriptMessage) throws -> [String: Any] {
        ["name": value.name, "tags": value.tags, "args": try (0..<value.arguments.count).map { ["name": value.arguments.nameAt($0), "value": try encode(value.arguments[$0])] as [String: Any] }]
    }

    static func encode(_ value: GesValue) throws -> [String: Any] {
        var result: [String: Any]
        switch value.valueKind {
        case .nothing: return ["type": ":Nothing"]
        case .boolean: return ["type": ":Boolean", "value": value.asBoolean ? "true" : "false"]
        case .text, .tag: return ["type": value.valueKind == .text ? ":Text" : ":Tag", "value": value.textValue ?? ""]
        case .integer, .float:
            result = ["type": (value.unit == .none ? ":Number." : ":Quantity.") + (value.valueKind == .integer ? "int64" : "binary64"), "value": value.valueKind == .integer ? String(value.integerValue!) : numberText(value.asNumber)]
        case .percentage: return ["type": ":Percentage", "value": numberText(value.asNumber)]
        case .dice: return ["type": ":Dice", "rolls": value.diceRolls ?? []]
        case .list: return ["type": ":List", "items": try (value.listValue ?? []).map(encode)]
        case .message: return ["type": ":Message", "message": try encodeMessage(value.messageValue!)]
        case .vector, .point:
            let v = (x: value.x, y: value.y, z: value.z)
            result = ["type": value.valueKind == .vector ? ":Vector" : ":Point", "x": numberText(v.x), "y": numberText(v.y), "z": numberText(v.z)]
        case .range:
            if let r = value.integerRangeValue { return ["type": ":Range.int64", "from": String(r.from), "to": String(r.to), "step": String(r.step)] }
            let r = value.floatRangeValue!
            return ["type": ":Range.binary64", "from": numberText(r.from), "to": numberText(r.to), "step": numberText(r.step)]
        default:
            return [
                "type": value.valueKind == .map ? ":Map" : ":" + (value.customTypeName ?? ""),
                "entries": try value.materializedMap().map { map in try (0..<map.length).map { ["key": map.key(at: $0), "value": try encode(map.value(at: $0))] as [String: Any] } } ?? [],
            ]
        }
        if value.unit != .none { result["unit"] = ":" + String(describing: value.unit) }
        return result
    }
}
