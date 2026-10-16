// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation

enum Validation {
    static func require(_ condition: Bool) throws {
        if !condition { throw ConformanceExecutionError.invalidInput("Invalid adapter request") }
    }

    static func fields(_ input: Input, _ names: [String], optional: [String] = []) throws {
        guard let entries = input.objectValue else { throw ConformanceExecutionError.invalidInput("Expected object") }
        let keys = Set(entries.map(\.key))
        try require(Set(names).isSubset(of: keys) && keys.isSubset(of: Set(names + optional)))
    }

    static func array(_ input: Input) throws -> [Input] {
        guard let value = input.arrayValue else { throw ConformanceExecutionError.invalidInput("Expected array") }
        return value
    }

    static func strings(_ input: Input) throws -> [String] {
        try array(input).map { value in
            guard let text = value.stringValue else { throw ConformanceExecutionError.invalidInput("Expected text") }
            return text
        }
    }

    static func batch(_ input: Input, measuring: Bool) throws {
        try fields(input, ["formatVersion", "requestDigest", "cases"])
        let digest = try input.text("requestDigest")
        try require(input["formatVersion"]?.integerValue == 1 && digest.utf8.count == 64 && digest.utf8.allSatisfy { (48...57).contains($0) || (65...70).contains($0) || (97...102).contains($0) })
        let cases = try array(input.required("cases"))
        try require((1...10000).contains(cases.count))
        var ids = Set<String>()
        for test in cases {
            let id = try test.text("id")
            try require(!id.isEmpty && ids.insert(id).inserted)
            let kind = try test.text("kind")
            try require(kinds.contains(kind) && (!measuring || kind == "scriptApi"))
            var fields = ["id", "kind", "sources", "compile", "requires"]
            if ["loadError", "messageApi", "valueApi", "externalTypeApi"].contains(kind) { fields.append("api") }
            if kind == "scriptApi" { fields.append("runtime") }
            if kind == "programBinary" { fields.append("binary") }
            if measuring { fields.append("measurement") }
            try self.fields(test, fields)
            var programs = Set<String>()
            for source in try array(test.required("sources")) {
                try self.fields(source, ["name", "program", "text"])
                try require(source["text"]?.stringValue != nil && source["name"]?.stringValue?.isEmpty == false && source["program"]?.stringValue?.isEmpty == false)
                programs.insert(try source.text("program"))
            }
            try require(kind == "scriptApi" || programs.count <= 1)
            let options = try test.required("compile")
            try self.fields(options, ["debugInfo", "binaryRoundTrip"])
            try require(options["binaryRoundTrip"]?.boolValue != nil)
            let debug = try strings(options.required("debugInfo"))
            try require(Set(debug).count == debug.count && Set(debug).isSubset(of: ["debugSymbols", "sourceMap", "sourceArchive"]))
            let requires = try test.required("requires")
            try self.fields(requires, ["core", "optional"])
            for category in ["core", "optional"] { try require(Set(strings(requires.required(category))).isSubset(of: Set(capabilities))) }
            if kind == "scriptApi" { try runtime(test.required("runtime"), programs: programs) }
            if kind == "programBinary" {
                let binary = try test.required("binary")
                try self.fields(binary, ["data", "includeDump", "compareCompiledRuntime", "programVersion", "runtime"])
                try require(Data(base64Encoded: binary.text("data")) != nil && binary["includeDump"]?.boolValue != nil && binary["compareCompiledRuntime"]?.boolValue != nil)
                try require(UInt64(binary.text("programVersion")) != nil && options["binaryRoundTrip"]?.boolValue == false)
                if binary["runtime"] != .null { try runtime(binary.required("runtime"), programs: ["main"]) }
            }
            if let api = test["api"] {
                switch kind {
                case "loadError":
                    try self.fields(api, ["limits", "externalTypeRegistry"])
                    try limits(api.required("limits"))
                    try require(["environment", "absent", "mismatch"].contains(api.text("externalTypeRegistry")))
                case "externalTypeApi":
                    try self.fields(api, ["typeNames"])
                    _ = try strings(api.required("typeNames"))
                case "valueApi":
                    try self.fields(api, ["value", "equalTo", "notEqualTo", "mutateSourceAfterCreate"])
                    try require(api["mutateSourceAfterCreate"]?.boolValue != nil)
                    for key in ["value", "equalTo", "notEqualTo"] where api[key] != .null { try value(api.required(key)) }
                default:
                    try self.fields(api, ["signature", "message"], optional: ["compareSignature", "compareMessage", "compareHandler", "createArguments", "json", "roundTripJson", "includeNormalized"])
                    for key in ["signature", "compareSignature", "compareHandler"] {
                        if let sig = api[key] {
                            try self.fields(sig, ["name", "parameters"])
                            _ = try strings(sig.required("parameters"))
                            try require(sig["name"]?.stringValue != nil)
                        }
                    }
                    for key in ["message", "compareMessage"] { if let msg = api[key] { try message(msg, allowObject: true) } }
                    if let arguments = api["createArguments"] { for v in try array(arguments) { try value(v) } }
                    for key in ["roundTripJson", "includeNormalized"] { if let flag = api[key] { try require(flag.boolValue != nil) } }
                    if let json = api["json"] { try require(json.stringValue != nil) }
                }
            }
            if measuring {
                let scenario = try test.required("runtime")
                try require(programs.count == 1 && scenario["hostCount"]?.integerValue == 1)
                try require(scenario.values("nativeHandlers").isEmpty && scenario.values("deferredPrograms").isEmpty && scenario["stepActions"]?.objectValue?.isEmpty == true)
                try require(scenario["initializationPump"]?.stringValue == "completion" && scenario["publishSink"]?.stringValue != "throw")
                try require(!scenario.values("steps").isEmpty && scenario.values("steps").allSatisfy { ["completion", "frames"].contains($0["pump"]?.stringValue ?? "") })
                let measurement = try test.required("measurement")
                try self.fields(measurement, ["profile", "iterations", "warmupIterations", "compileWarmupIterations", "observeRuntime", "metrics"])
                try require(measurement["profile"]?.stringValue == SwiftPerformanceProvider.profile && measurement["observeRuntime"]?.boolValue != nil)
                try require((1...Int(UInt32.max)).contains(measurement["iterations"]?.integerValue ?? 0))
                for field in ["warmupIterations", "compileWarmupIterations"] { try require((0...Int(UInt32.max)).contains(measurement[field]?.integerValue ?? -1)) }
                let metrics = try array(measurement.required("metrics"))
                try require(!metrics.isEmpty)
                var metricIDs = Set<String>()
                for metric in metrics {
                    try self.fields(metric, ["id", "unit"])
                    let id = try metric.text("id")
                    let expectedUnit: String
                    switch id {
                    case "compile.elapsed", "program-load.elapsed", "run.elapsed": expectedUnit = "ms"
                    case "compile.allocated", "program-load.allocated", "run.allocated": expectedUnit = "B"
                    case "run.per-invoke-elapsed": expectedUnit = "ms/iteration"
                    case "run.per-invoke-allocated": expectedUnit = "B/iteration"
                    case "run.allocations": expectedUnit = "count"
                    default: throw ConformanceExecutionError.invalidInput("Unknown metric")
                    }
                    try require(metricIDs.insert(id).inserted && metric["unit"]?.stringValue == expectedUnit)
                }
            }
        }
    }

    static func limits(_ input: Input) throws {
        let allowed = [
            "maxProcessedEventsPerRun", "maxQueuedMessagesPerRun", "maxExecutionSteps", "maxRegisterValues", "maxLoopIterations", "maxCallDepth", "maxRandomScopeDepth", "maxRangeItems", "maxGeneratedCollectionItems", "maxDiceCount", "maxDiceSides",
        ]
        try fields(input, [], optional: allowed)
        for entry in input.objectValue! {
            try require((1...Int(Int32.max)).contains(entry.value.integerValue ?? 0))
            if entry.key == "maxRandomScopeDepth" { try require(entry.value.integerValue! <= 65535) }
        }
    }

    static func runtime(_ input: Input, programs: Set<String>) throws {
        try fields(input, ["limits", "steps", "random", "hostCount", "nativeHandlers", "deferredPrograms", "stepActions", "initializationPump", "publishSink"])
        try limits(input.required("limits"))
        try require((1...256).contains(input["hostCount"]?.integerValue ?? 0))
        try require(["accept", "absent", "reject", "throw"].contains(input.text("publishSink")))
        try require(["start", "completion"].contains(input.text("initializationPump")))
        let deferred = try strings(input.required("deferredPrograms"))
        try require(Set(deferred).isSubset(of: programs) && Set(deferred).count == deferred.count)
        var handlers = Set<String>()
        for handler in try array(input.required("nativeHandlers")) {
            try fields(handler, ["id", "message", "parameters", "messageName", "priority", "initiallySubscribed", "throw", "faultCode", "faultContext", "emit", "actions"])
            try require(handlers.insert(handler.text("id")).inserted)
            _ = try strings(handler.required("parameters"))
            try require(handler["message"]?.stringValue != nil && handler["priority"]?.integerValue.map { (Int(Int32.min)...Int(Int32.max)).contains($0) } == true)
            try require(handler["faultCode"] == .null || handler["faultCode"]?.stringValue != nil)
            let context = try handler.required("faultContext")
            try fields(context, ["programName", "handlerName"])
            for key in ["programName", "handlerName"] { try require(context[key] == .null || context[key]?.stringValue != nil) }
            for key in ["messageName", "initiallySubscribed", "throw"] { try require(handler[key]?.boolValue != nil) }
            for emit in try array(handler.required("emit")) {
                try fields(emit, ["message", "forwardArguments"])
                try require(emit["forwardArguments"]?.boolValue != nil)
                try message(emit.required("message"))
            }
        }
        var steps = Set<String>()
        for step in try array(input.required("steps")) {
            try fields(step, ["id", "input", "pump", "budget"])
            try require(steps.insert(step.text("id")).inserted)
            let pump = try step.text("pump")
            try require(["completion", "enqueue", "frame", "frames"].contains(pump))
            if ["frame", "frames"].contains(pump) { try require((step["budget"]?.integerValue ?? 0) > 0) } else { try require(step["budget"] == .null) }
            try message(step.required("input"))
        }

        func actions(_ values: [Input]) throws {
            for action in values {
                try fields(action, ["operation", "target"])
                let target = try action.text("target")
                switch try action.text("operation") {
                case "advanceMicroseconds": try require(Int64(target).map { $0 >= 0 } == true)
                case "loadProgram": try require(deferred.contains(target))
                case "detachProgram": try require(programs.contains(target))
                case "subscribeHandler", "unsubscribeHandler": try require(handlers.contains(target))
                default: try require(false)
                }
            }
        }

        try fields(input.required("stepActions"), [], optional: Array(steps))
        for group in input["stepActions"]!.objectValue! { try actions(array(group.value)) }
        for handler in input.values("nativeHandlers") { try actions(array(handler.required("actions"))) }
        let random = try input.required("random")
        try fields(random, [], optional: ["seed", "sequence", "entropy"])
        try require(random.objectValue!.count == 1)
        if let seed = random["seed"] { try require(Int64(seed.stringValue ?? "") != nil) }
        if let sequence = random["sequence"] {
            let values = try strings(sequence)
            try require(!values.isEmpty)
            for value in values { try require(RuntimeValues.parseNumber(value).isFinite) }
        }
        if let entropy = random["entropy"] {
            let text = entropy.stringValue ?? ""
            try require(!text.isEmpty && text.count % 2 == 0 && text.utf8.allSatisfy { (48...57).contains($0) || (65...70).contains($0) })
        }
    }

    static func message(_ input: Input, allowObject: Bool = false) throws {
        try fields(input, ["name", "args", "tags"])
        try require(input["name"]?.stringValue != nil)
        _ = try strings(input.required("tags"))
        if allowObject, let args = input["args"]?.objectValue {
            for item in args { try value(item.value) }
            return
        }
        for arg in try array(input.required("args")) {
            try fields(arg, ["name", "value"])
            try require(arg["name"]?.stringValue != nil)
            try value(arg.required("value"))
        }
    }

    static func value(_ input: Input) throws {
        let type = try input.text("type")
        switch type {
        case ":Nothing": try fields(input, ["type"])
        case ":List":
            try fields(input, ["type", "items"])
            for item in try array(input.required("items")) { try value(item) }
        case ":Map": fallthrough
        default:
            if input["entries"] != nil {
                try require(type.first == ":" && type.dropFirst().first?.isUppercase == true)
                try fields(input, ["type", "entries"])
                for entry in try array(input.required("entries")) {
                    try fields(entry, ["key", "value"])
                    try require(entry["key"]?.stringValue != nil)
                    try value(entry.required("value"))
                }
            } else if type == ":Message" {
                try fields(input, ["type", "message"])
                try message(input.required("message"))
            } else if [":Vector", ":Point"].contains(type) {
                try fields(input, ["type", "x", "y", "z"], optional: ["unit"])
                for key in ["x", "y", "z"] { _ = try RuntimeValues.parseNumber(input.text(key)) }
            } else if [":Range.int64", ":Range.binary64"].contains(type) {
                try fields(input, ["type", "from", "to", "step"])
                _ = try RuntimeValues.decode(input)
            } else if type == ":Dice" {
                try fields(input, ["type", "rolls"])
                for roll in try array(input.required("rolls")) { try require((1...Int(Int32.max)).contains(roll.integerValue ?? 0)) }
            } else {
                try require([":Number.int64", ":Number.binary64", ":Quantity.int64", ":Quantity.binary64", ":Percentage", ":Boolean", ":Text", ":Tag"].contains(type))
                try fields(input, type.hasPrefix(":Quantity.") ? ["type", "value", "unit"] : ["type", "value"])
                try require(input["value"]?.stringValue != nil)
                if type == ":Boolean" { try require(["true", "false"].contains(input.text("value"))) }
            }
        }
    }
}

// JSONDecoder otherwise discards duplicate properties before schema validation.
struct JSONAudit {
    let bytes: [UInt8]
    var index = 0

    mutating func whitespace() { while index < bytes.count && [9, 10, 13, 32].contains(bytes[index]) { index += 1 } }

    mutating func string() throws -> Data {
        let start = index
        try Validation.require(index < bytes.count && bytes[index] == 34)
        index += 1
        while index < bytes.count {
            let byte = bytes[index]
            index += 1
            if byte == 92 {
                index += 1
            } else if byte == 34 {
                let text = try JSONDecoder().decode(String.self, from: Data(bytes[start..<index]))
                return Data(text.utf8)
            }
        }
        throw ConformanceExecutionError.invalidInput("Unterminated JSON string")
    }

    mutating func value(depth: Int = 0) throws {
        try Validation.require(depth < 64)
        whitespace()
        try Validation.require(index < bytes.count)
        let opening = bytes[index]
        if opening == 34 {
            _ = try string()
            return
        }
        if opening == 123 || opening == 91 {
            index += 1
            let closing: UInt8 = opening == 123 ? 125 : 93
            var keys = Set<Data>()
            whitespace()
            if index < bytes.count && bytes[index] == closing {
                index += 1
                return
            }
            while true {
                whitespace()
                if opening == 123 {
                    try Validation.require(keys.insert(string()).inserted)
                    whitespace()
                    try Validation.require(index < bytes.count && bytes[index] == 58)
                    index += 1
                }
                try value(depth: depth + 1)
                whitespace()
                try Validation.require(index < bytes.count)
                if bytes[index] == closing {
                    index += 1
                    return
                }
                try Validation.require(bytes[index] == 44)
                index += 1
            }
        }
        let start = index
        while index < bytes.count && ![9, 10, 13, 32, 44, 93, 125].contains(bytes[index]) { index += 1 }
        try Validation.require(index > start)
    }

    static func check(_ data: Data) throws {
        var audit = JSONAudit(bytes: Array(data))
        try audit.value()
        audit.whitespace()
        try Validation.require(audit.index == audit.bytes.count)
    }
}
