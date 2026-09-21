// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime

// Executable-only interoperability adapter. Parsing and formatting run through the VM.
enum NumberTextProbe {
    private struct Request: Decodable {
        let kind: String
        let payload: String
        let unit: String
        let text: String?
    }
    private struct Response: Encodable {
        let source: String
        let text: String
        let number: String
        let percentage: String
    }
    private final class Capture: GameEventScriptNativeMessageHandler {
        var message: GameEventScriptMessage?
        var count = 0
        func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) {
            self.message = message
            count += 1
        }
    }
    static func run(input: String, output: String) throws {
        let requests = try JSONDecoder().decode([Request].self, from: Data(contentsOf: URL(fileURLWithPath: input)))
        let program = try GameEventScriptBuilder().addScript(
            "on Probe(value, text) { emit Result(formatted: value as :Text, number: text as :Number, percentage: text as :Percentage) }"
        ).compile()
        let host = try GameEventScriptHost(seed: 0)
        let capture = Capture()
        _ = try host.subscribe(
            .init(name: "Result", parameters: ["formatted", "number", "percentage"]), handler: capture)
        _ = try host.load(program)
        guard try host.start().state == .ready else { throw ToolError.invalidFixture("Probe host failed to start") }
        var responses: [Response] = []
        for row in requests {
            let unit: GesUnit
            switch row.unit {
            case "none": unit = .none
            case "m": unit = .meter
            case "s": unit = .second
            case "degree": unit = .degree
            default: throw ToolError.invalidFixture("Unknown unit")
            }
            let value: GesValue
            if row.kind == "integer", let integer = Int64(row.payload) {
                value = .integer(integer, unit: unit)
            } else if let bits = UInt64(row.payload, radix: 16) {
                let number = Double(bitPattern: bits)
                if row.kind == "float" {
                    value = .float(number, unit: unit)
                } else if row.kind == "percentage" && unit == .none {
                    value = .percentage(number)
                } else {
                    throw ToolError.invalidFixture("Unknown numeric input kind")
                }
            } else {
                throw ToolError.invalidFixture("Invalid numeric payload")
            }
            capture.message = nil
            capture.count = 0
            host.receive(
                try .init(
                    name: "Probe",
                    arguments: [
                        .init(name: "value", value: value), .init(name: "text", value: .text(row.text ?? "nothing")),
                    ]))
            guard try host.runToCompletion().state == .completed, capture.count == 1, let result = capture.message
            else {
                throw ToolError.invalidFixture("Probe did not produce exactly one result")
            }
            responses.append(
                try Response(
                    source: describe(value), text: result.arguments[0].asText,
                    number: describe(result.arguments[1]), percentage: describe(result.arguments[2])))
        }
        try JSONEncoder().encode(responses).write(to: URL(fileURLWithPath: output))
    }
    private static func describe(_ value: GesValue) throws -> String {
        if value.isNothing { return "nothing" }
        let unit: String
        switch value.unit {
        case .none: unit = "none"
        case .meter: unit = "m"
        case .second: unit = "s"
        case .degree: unit = "degree"
        }
        if let integer = value.integerValue { return "integer:\(integer):\(unit)" }
        guard value.kind == .float || value.kind == .percentage else {
            throw ToolError.invalidFixture("Non-numeric result")
        }
        let kind = value.kind == .percentage ? "percentage" : "float"
        let hex = String(value.asNumber.bitPattern, radix: 16)
        return kind + ":" + String(repeating: "0", count: 16 - hex.count) + hex + ":" + unit
    }
}
