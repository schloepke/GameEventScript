// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension ConformanceSchema {
    static func validateValue(_ node: Node) throws {
        let type = try string(required(node, "type"))
        guard type.hasPrefix(":"), type.utf8.count >= 2 else {
            throw fail("invalidValue", "Portable value types begin with ':'.", node)
        }
        let fields: [String]
        switch type {
        case ":Nothing": fields = []
        case ":Text", ":Tag", ":Boolean", ":Percentage", ":Number.int64", ":Number.binary64": fields = ["value"]
        case ":Quantity.int64", ":Quantity.binary64": fields = ["value", "unit"]
        case ":Vector", ":Point": fields = ["x", "y", "z", "unit"]
        case ":List": fields = ["items"]
        case ":Dice": fields = ["rolls"]
        case ":Range.int64", ":Range.binary64": fields = ["from", "to", "step"]
        case ":Message": fields = ["message"]
        default: fields = ["entries"]
        }
        try closed(node, ["type"] + fields)
        for field in fields where field != "unit" { _ = try required(node, field) }
        if let unit = node["unit"] { _ = try string(unit) }
        switch type {
        case ":Text", ":Tag": _ = try string(required(node, "value"))
        case ":Boolean": _ = try boolean(required(node, "value"))
        case ":Number.int64", ":Quantity.int64":
            _ = try string(required(node, "value"))
            _ = try integer(required(node, "value"))
        case ":Number.binary64", ":Quantity.binary64", ":Percentage": _ = try binary64(required(node, "value"))
        case ":Vector", ":Point": for field in ["x", "y", "z"] { _ = try binary64(required(node, field)) }
        case ":Range.int64":
            for field in ["from", "to", "step"] {
                _ = try string(required(node, field))
                _ = try integer(required(node, field))
            }
        case ":Range.binary64": for field in ["from", "to", "step"] { _ = try binary64(required(node, field)) }
        case ":List": for item in try array(required(node, "items")) { try validateValue(item) }
        case ":Dice":
            for roll in try array(required(node, "rolls")) {
                _ = try integer(roll, min: Int64(Int32.min), max: Int64(Int32.max))
            }
        case ":Message": try validateMessage(required(node, "message"))
        case ":Nothing": break
        default:
            for item in try array(required(node, "entries")) {
                try closed(item, ["key", "value"])
                _ = try string(required(item, "key"))
                try validateValue(required(item, "value"))
            }
        }
    }
    static func binary64(_ node: Node, finite: Bool = false) throws -> Double {
        let text = try string(node)
        if !finite {
            if text == "NaN" { return .nan }
            if text == "Infinity" { return .infinity }
            if text == "-Infinity" { return -.infinity }
        }
        guard let value = Double(text), value.isFinite, canonicalBinary64(value) == text else {
            throw fail("invalidValue", "Expected finite Binary64 or NaN/Infinity/-Infinity.", node)
        }
        return value
    }
    /// Conformance transport follows shortest digits with the V1 fixed/scientific cutoffs.
    /// Swift and .NET both supply shortest roundtrip digits; presentation is normalized here.
    static func canonicalBinary64(_ value: Double) -> String {
        if value == 0 { return "0" }
        let negative = value < 0
        let components = String(value.magnitude).split(separator: "e", omittingEmptySubsequences: false)
        let sourceExponent = components.count == 2 ? Int(components[1])! : 0
        let significand = components[0].split(separator: ".", omittingEmptySubsequences: false)
        let before = significand[0].utf8.count
        var digits = Array(significand.joined().utf8)
        let leading = digits.prefix(while: { $0 == 48 }).count
        digits.removeFirst(leading)
        while digits.last == 48 { digits.removeLast() }
        let exponent = sourceExponent + before - leading - 1
        let sign = negative ? "-" : ""
        if exponent < -4 || exponent >= 17 {
            let head = String(decoding: digits.prefix(1), as: UTF8.self)
            let tail = digits.count > 1 ? "." + String(decoding: digits.dropFirst(), as: UTF8.self) : ""
            return sign + head + tail + "e" + String(exponent)
        }
        let point = exponent + 1
        if point <= 0 {
            return sign + "0." + String(repeating: "0", count: -point) + String(decoding: digits, as: UTF8.self)
        }
        if point >= digits.count {
            return sign + String(decoding: digits, as: UTF8.self) + String(repeating: "0", count: point - digits.count)
        }
        return sign + String(decoding: digits.prefix(point), as: UTF8.self) + "."
            + String(decoding: digits.dropFirst(point), as: UTF8.self)
    }
    static func validateArguments(_ node: Node) throws {
        var names: Set<[UInt8]> = []
        for item in try array(node) {
            try closed(item, ["name", "value"])
            let name = try string(required(item, "name"))
            guard name == "_" || names.insert(Array(name.utf8)).inserted else {
                throw fail("duplicateId", "Duplicate argument name '\(name)'.", item)
            }
            try validateValue(required(item, "value"))
        }
    }
    static func validateMessage(_ node: Node, allowMapping: Bool = false) throws {
        try closed(node, ["name", "tags", "args"])
        _ = try string(required(node, "name"))
        if let tags = node["tags"] { _ = try stringList(tags) }
        if let args = node["args"] {
            if allowMapping, let entries = args.entries {
                for entry in entries { try validateValue(entry.value) }
            } else {
                try validateArguments(args)
            }
        }
    }
    static func validateSignature(_ node: Node) throws {
        try closed(node, ["name", "parameters"])
        _ = try string(required(node, "name"))
        if let parameters = node["parameters"] { _ = try stringList(parameters) }
    }
    static func validateMessageAPI(_ node: Node) throws {
        try closed(
            node,
            [
                "signature", "message", "compareSignature", "compareMessage", "compareConformanceMessage",
                "compareHandler", "createArguments",
            ])
        try validateSignature(required(node, "signature"))
        try validateMessage(required(node, "message"), allowMapping: true)
        for field in ["compareSignature", "compareHandler"] {
            if let value = node[field] { try validateSignature(value) }
        }
        for field in ["compareMessage", "compareConformanceMessage"] {
            if let value = node[field] { try validateMessage(value) }
        }
        if let arguments = node["createArguments"] { for value in try array(arguments) { try validateValue(value) } }
    }
    static func validateValueAPI(_ node: Node) throws {
        try closed(node, ["value", "equalTo", "notEqualTo", "mutateSourceAfterCreate"])
        try validateValue(required(node, "value"))
        for field in ["equalTo", "notEqualTo"] { if let value = node[field] { try validateValue(value) } }
        try boolFields(node, ["mutateSourceAfterCreate"])
    }
    static func validateMessageExpectation(_ node: Node) throws {
        let booleans = [
            "matches", "signatureEquals", "signatureHashEquals", "messageEquals", "messageHashEquals",
            "conformanceEquals", "handlerEquals", "handlerHashEquals",
        ]
        let strings = ["name", "signatureId", "messageSignatureId", "createdMessageSignatureId", "error"]
        try closed(node, strings + booleans + ["argumentCount"])
        guard !node.entries!.isEmpty else {
            throw fail("missingField", "Message expectations need at least one constraint.", node)
        }
        if node["error"] != nil && node.entries!.count != 1 {
            throw fail("invalidValue", "Error expectations cannot include success constraints.", node)
        }
        try stringFields(node, strings)
        try boolFields(node, booleans)
        try unsignedFields(node, ["argumentCount"])
    }
    static func validateValueExpectation(_ node: Node) throws {
        let booleans = ["isNumeric", "hasValue", "isNothing", "hasUnit", "asBoolean", "equal", "equalHash", "notEqual"]
        try closed(node, ["normalized", "length", "customTypeName"] + booleans)
        try validateValue(required(node, "normalized"))
        try boolFields(node, booleans)
        try unsignedFields(node, ["length"])
        try stringFields(node, ["customTypeName"])
    }
    static func validateDiagnostic(_ node: Node) throws {
        let strings = ["phase", "code", "symbol", "symbolKind", "sourceName"]
        let positions = ["line", "column", "endLine", "endColumn"]
        try closed(node, strings + positions + ["programName", "handlerName"])
        _ = try string(required(node, "phase"))
        _ = try string(required(node, "code"))
        try stringFields(node, strings)
        try unsignedFields(node, positions)
        for key in ["programName", "handlerName"] {
            if let value = node[key], value.scalar != .null { _ = try string(value) }
        }
    }
    static func validateRuntimeLimit(_ node: Node) throws {
        try closed(node, ["any", "name", "detailContains", "limit"])
        try boolFields(node, ["any"])
        try stringFields(node, ["name", "detailContains"])
        try unsignedFields(node, ["limit"], max: .max)
        let any = node["any"]?.boolean == true
        let constrained = ["name", "detailContains", "limit"].contains(where: { node[$0] != nil })
        if !any && !constrained {
            throw fail("missingField", "Runtime limits require a constraint or any: true.", node)
        }
        if any && constrained { throw fail("invalidValue", "Wildcard limits cannot contain other constraints.", node) }
    }
    static func validateTrace(_ node: Node) throws {
        for item in try array(node) {
            let event = try string(required(item, "event"))
            switch event {
            case "emit":
                try closed(item, ["event", "message", "accepted"])
                try validateMessage(required(item, "message"))
                _ = try boolean(required(item, "accepted"))
            case "publish":
                try closed(item, ["event", "message", "result"])
                try validateMessage(required(item, "message"))
                let result = try required(item, "result")
                let fields = ["localAccepted", "outboundAttempted", "outboundAccepted", "anyAccepted"]
                try closed(result, fields)
                for field in fields { _ = try boolean(required(result, field)) }
                let local = result["localAccepted"]!.boolean!
                let attempted = result["outboundAttempted"]!.boolean!
                let outbound = result["outboundAccepted"]!.boolean!
                let any = result["anyAccepted"]!.boolean!
                guard (!outbound || attempted) && any == (local || outbound) else {
                    throw fail("invalidValue", "Inconsistent publish result.", result)
                }
            case "dispatchStarted", "dispatchCompleted":
                try closed(item, ["event", "message", "signatureId"])
                try validateMessage(required(item, "message"))
                _ = try string(required(item, "signatureId"))
            case "runtimeLimit":
                try closed(item, ["event", "runtimeLimit"])
                try validateRuntimeLimit(required(item, "runtimeLimit"))
            case "diagnostic":
                try closed(item, ["event", "diagnostic"])
                try validateDiagnostic(required(item, "diagnostic"))
            default: throw fail("invalidValue", "Unknown observer event '\(event)'.", item)
            }
        }
    }
    static func validateObservations(_ node: Node) throws {
        if let limits = node["runtimeLimits"] {
            try closed(limits, ["include", "exclude"])
            for field in ["include", "exclude"] {
                if let list = limits[field] { for item in try array(list) { try validateRuntimeLimit(item) } }
            }
        }
        if let diagnostics = node["diagnostics"] { for item in try array(diagnostics) { try validateDiagnostic(item) } }
        if let trace = node["trace"] { try validateTrace(trace) }
        for field in ["local", "outbound"] {
            if let list = node[field] { for item in try array(list) { try validateMessage(item) } }
        }
    }
    static func validateStepExpectation(_ node: Node) throws {
        try closed(node, ["input", "accepted", "local", "outbound", "paused", "runtimeLimits", "diagnostics", "trace"])
        try boolFields(node, ["accepted", "paused"])
        try validateObservations(node)
        if let input = node["input"] {
            try closed(input, ["tags", "args"])
            if let tags = input["tags"] { _ = try stringList(tags) }
            if let args = input["args"] { try validateArguments(args) }
        }
    }
    static func validateExpectation(_ node: Node, kind: String) throws {
        let fields: [String]
        switch kind {
        case "scriptApi": fields = ["steps", "initialization"]
        case "performance": fields = ["steps", "initialization", "performance"]
        case "compileError", "loadError": fields = ["error"]
        case "messageApi": fields = ["message"]
        case "valueApi": fields = ["value"]
        case "externalTypeApi": fields = ["externalType"]
        case "compileMetadata": fields = ["metadata"]
        case "bytecode": fields = ["opcodes"]
        case "programBinary": fields = ["binary", "steps", "initialization"]
        default: fields = []
        }
        try closed(node, ["gesBlock"] + fields)
        if let error = node["error"] { try validateDiagnostic(error) }
        if let message = node["message"] { try validateMessageExpectation(message) }
        if let value = node["value"] { try validateValueExpectation(value) }
        if let external = node["externalType"] {
            try closed(external, ["typeCount", "error"])
            try unsignedFields(external, ["typeCount"])
            try stringFields(external, ["error"])
            guard !external.entries!.isEmpty else {
                throw fail("missingField", "External type expectations require constraints.", external)
            }
            if external["error"] != nil && external.entries!.count > 1 {
                throw fail("invalidValue", "Error expectations cannot constrain success.", external)
            }
        }
        if let initialization = node["initialization"] {
            try closed(initialization, ["local", "outbound", "runtimeLimits", "diagnostics", "trace"])
            try validateObservations(initialization)
        }
        if let steps = node["steps"] { for entry in try object(steps) { try validateStepExpectation(entry.value) } }
        if let opcodes = node["opcodes"] { try validateOpcodes(opcodes) }
        if let metadata = node["metadata"] { try validateCompileMetadata(metadata) }
        if let binary = node["binary"] { try validateBinaryExpectation(binary) }
        if let performance = node["performance"] { try validatePerformance(performance) }
    }
}
