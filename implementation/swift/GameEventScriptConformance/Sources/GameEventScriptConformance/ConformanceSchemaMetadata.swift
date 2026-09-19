// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension ConformanceSchema {
    static func validateMetadata(_ node: Node, limits: ConformanceParserLimits) throws {
        if let random = node["random"] {
            try closed(random, ["seed", "sequence", "entropy"])
            guard random.entries!.count == 1 else {
                throw fail("invalidValue", "random requires exactly one configuration.", random)
            }
            if let seed = random["seed"] { _ = try integer(seed) }
            if let sequence = random["sequence"] {
                let values = try array(sequence)
                guard !values.isEmpty else { throw fail("invalidValue", "random.sequence cannot be empty.", sequence) }
                for value in values { _ = try binary64(value, finite: true) }
            }
            if let entropy = random["entropy"] {
                let text = try string(entropy)
                guard !text.isEmpty, text.utf8.count % 2 == 0,
                    text.utf8.allSatisfy({ (48...57).contains($0) || (65...70).contains($0) })
                else {
                    throw fail("invalidValue", "Entropy must contain uppercase hexadecimal byte pairs.", entropy)
                }
            }
        }
        if let sink = node["publishSink"] { try choice(sink, ["accept", "absent", "reject", "throw"]) }
        if let registry = node["externalTypeRegistry"] { try choice(registry, ["environment", "absent", "mismatch"]) }
        if let count = node["hostCount"] {
            let value = try unsigned(count, max: UInt64(UInt32.max))
            guard value > 0, limits.maxHostsPerTest > 0, value <= UInt64(limits.maxHostsPerTest) else {
                throw fail("invalidValue", "hostCount exceeds parser limits.", count)
            }
        }
        if let deferred = node["deferredPrograms"] { _ = try stringList(deferred, ids: true) }
        if let handlers = node["nativeHandlers"] { try validateNativeHandlers(handlers) }
        if let actions = node["stepActions"] { for entry in try object(actions) { try validateActions(entry.value) } }
        if let message = node["messageApi"] { try validateMessageAPI(message) }
        if let value = node["valueApi"] { try validateValueAPI(value) }
        if let external = node["externalTypeApi"] {
            try closed(external, ["typeNames"])
            _ = try stringList(required(external, "typeNames"))
        }
        if let binary = node["binaryFixture"] { try validateBinaryFixture(binary) }
        if let performance = node["performance"] {
            try closed(performance, ["iterations", "warmupIterations", "compileWarmupIterations", "observeRuntime"])
            guard try unsigned(required(performance, "iterations"), max: UInt64(UInt32.max)) > 0 else {
                throw fail("invalidValue", "Performance iterations must be positive.", performance)
            }
            try unsignedFields(performance, ["warmupIterations", "compileWarmupIterations"])
            try boolFields(performance, ["observeRuntime"])
        }
    }
    static func validateNativeHandlers(_ node: Node) throws {
        var ids: Set<String> = []
        for (index, item) in try array(node).enumerated() {
            try closed(
                item,
                [
                    "id", "message", "parameters", "messageName", "priority", "initiallySubscribed", "throw",
                    "faultCode", "faultContext", "actions", "emit",
                ])
            let id = try item["id"].map(identifier) ?? nativeID(index)
            guard ids.insert(id).inserted else {
                throw fail("duplicateId", "Duplicate native handler ID '\(id)'.", item)
            }
            _ = try string(required(item, "message"))
            if let parameters = item["parameters"] {
                let values = try stringList(parameters)
                if item["messageName"]?.boolean == true && !values.isEmpty {
                    throw fail("invalidValue", "Name-only handlers cannot constrain parameters.", item)
                }
            }
            try boolFields(item, ["messageName", "initiallySubscribed", "throw"])
            if let priority = item["priority"] {
                _ = try integer(priority, min: Int64(Int32.min), max: Int64(Int32.max))
            }
            if let code = item["faultCode"] {
                let text = try string(code)
                guard !text.unicodeScalars.allSatisfy(\.properties.isWhitespace), !text.hasPrefix("runtime."),
                    item["throw"]?.boolean != true
                else {
                    throw fail(
                        "invalidValue",
                        "faultCode must be a nonblank caller code outside runtime. without throw: true.", code)
                }
            }
            if let context = item["faultContext"] {
                guard item["faultCode"] != nil else {
                    throw fail("invalidValue", "faultContext requires faultCode.", context)
                }
                try closed(context, ["programName", "handlerName"])
                for field in ["programName", "handlerName"] {
                    if let value = context[field], value.scalar != .null { _ = try string(value) }
                }
            }
            if let actions = item["actions"] { try validateActions(actions) }
            if let emits = item["emit"] {
                for emit in try array(emits) {
                    try closed(emit, ["name", "forwardArguments", "args"])
                    _ = try string(required(emit, "name"))
                    try boolFields(emit, ["forwardArguments"])
                    guard (emit["forwardArguments"]?.boolean == true) != (emit["args"] != nil) else {
                        throw fail("invalidValue", "Emit requires forwardArguments: true or args.", emit)
                    }
                    if let args = emit["args"] { try validateArguments(args) }
                }
            }
        }
    }
    static func nativeID(_ index: Int) -> String {
        let digits = String(index + 1)
        return "native-" + String(repeating: "0", count: max(0, 4 - digits.count)) + digits
    }
    static func validateActions(_ node: Node) throws {
        let operations = ["loadProgram", "detachProgram", "subscribeHandler", "unsubscribeHandler"]
        for item in try array(node) {
            try closed(item, operations + ["expectResult", "expectError"])
            let supplied = operations.filter { item[$0] != nil }
            guard supplied.count == 1 else {
                throw fail("invalidCardinality", "Host actions require exactly one operation.", item)
            }
            _ = try identifier(required(item, supplied[0]))
            try boolFields(item, ["expectResult"])
            if let error = item["expectError"] {
                try validateDiagnostic(error)
                guard supplied[0] == "loadProgram", item["expectResult"] == nil, error["phase"]?.string == "link" else {
                    throw fail(
                        "invalidValue", "Only loadProgram accepts a link expectError, without expectResult.", error)
                }
            }
        }
    }
    static func validateHostReferences(_ node: Node, _ syntax: ConformanceMarkdownCase) throws {
        var programs: Set<String> = []
        if let descriptors = node["sources"]?.items {
            for source in descriptors { programs.insert(source["program"]?.string ?? "main") }
        } else if syntax.sources.count == 1 {
            programs.insert("main")
        }
        let deferred = try node["deferredPrograms"].map { try stringList($0, ids: true) } ?? []
        guard deferred.count == Set(deferred).count else {
            throw fail("duplicateId", "Duplicate deferred program ID.", node)
        }
        guard Set(deferred).isSubset(of: programs) else {
            throw fail("unknownReference", "Unknown deferred program ID.", node)
        }
        var handlerIDs: Set<String> = []
        let handlers = node["nativeHandlers"]?.items ?? []
        for (index, handler) in handlers.enumerated() { handlerIDs.insert(handler["id"]?.string ?? nativeID(index)) }
        var actions = handlers.compactMap { $0["actions"] }
        actions.append(contentsOf: (node["stepActions"]?.entries ?? []).map(\.value))
        for sequence in actions {
            for action in sequence.items ?? [] {
                let entry = action.entries!.first(where: {
                    ["loadProgram", "detachProgram", "subscribeHandler", "unsubscribeHandler"].contains($0.key)
                })!
                let target = entry.value.string!
                let valid =
                    entry.key == "loadProgram"
                    ? deferred.contains(target)
                    : entry.key == "detachProgram" ? programs.contains(target) : handlerIDs.contains(target)
                guard valid else {
                    throw fail("unknownReference", "Unknown or invalid host action target '\(target)'.", entry.value)
                }
            }
        }
    }
    static func validateBinaryFixture(_ node: Node) throws {
        try closed(
            node,
            [
                "id", "resourceId", "relativePath", "sha256", "compilerId", "compilerVersion", "programVersion",
                "compareCompiledRuntime", "derivation",
            ])
        for field in ["id", "resourceId", "compilerId"] { _ = try identifier(required(node, field)) }
        let path = try string(required(node, "relativePath"))
        let components = path.split(separator: "/", omittingEmptySubsequences: false)
        guard !path.isEmpty, !path.contains("\\"), !path.contains(":"),
            components.allSatisfy({ !$0.isEmpty && $0 != "." && $0 != ".." })
        else {
            throw fail("invalidValue", "Binary fixture paths must be portable relative paths without traversal.", node)
        }
        try sha256(required(node, "sha256"))
        guard !(try string(required(node, "compilerVersion"))).isEmpty else {
            throw fail("invalidValue", "Compiler versions cannot be empty.", node)
        }
        _ = try unsigned(required(node, "programVersion"))
        try boolFields(node, ["compareCompiledRuntime"])
        try stringFields(node, ["derivation"])
    }
    static func sha256(_ node: Node) throws {
        let text = try string(node)
        guard text.utf8.count == 64, text.utf8.allSatisfy({ (48...57).contains($0) || (65...70).contains($0) }) else {
            throw fail("invalidValue", "SHA-256 requires 64 uppercase hexadecimal digits.", node)
        }
    }
    static func validateBinaryExpectation(_ node: Node) throws {
        let successFields = [
            "rewriteByteExact", "rewriteSha256", "moduleName", "requiredRegisterCount", "requiredCallStackDepth",
            "opaqueSectionCount",
        ]
        try closed(node, ["outcome", "errorCode", "byteOffset", "sectionType", "entryIndex"] + successFields)
        try choice(required(node, "outcome"), ["valid", "readError", "validationError"])
        let valid = node["outcome"]!.string == "valid"
        guard valid == (node["errorCode"] == nil) else {
            throw fail("invalidValue", "Binary errorCode is required exactly for error outcomes.", node)
        }
        if !valid && successFields.contains(where: { node[$0] != nil }) {
            throw fail("invalidValue", "Binary error outcomes cannot constrain success fields.", node)
        }
        if let code = node["errorCode"] {
            let names = [
                "InvalidMagic", "UnsupportedFormatVersion", "InvalidHeaderFlags", "InvalidHeaderSize",
                "FileSizeMismatch", "FileTooLarge", "TooManySections", "TruncatedSectionHeader",
                "TruncatedSectionPayload", "SectionTooLarge", "InvalidSectionType", "InvalidSectionFlags",
                "InvalidSectionReserved", "UnsupportedSectionVersion", "UnsupportedCompression",
                "MissingRequiredSection",
                "DuplicateSection", "UnknownRequiredSection", "InvalidPayloadLength", "InvalidUtf8",
                "TooManyEntries", "InvalidStringIndex", "InvalidListIndex", "InvalidBindingKind",
                "DuplicateBindingId", "InvalidEntryAddress", "InvalidOpcode", "InvalidOperand",
                "InvalidJumpAddress", "InvalidCallAddress", "CyclicCallGraph", "InvalidResourceMetadata",
                "InvalidDebugSymbol", "InvalidSourceMap", "InvalidSourceArchive", "SourceMetadataMismatch",
                "ReaderLimitExceeded", "InvalidProgram",
            ]
            guard names.contains(try string(code)) else {
                throw fail("invalidValue", "Unknown binary format error code.", code)
            }
        }
        try stringFields(node, ["errorCode", "moduleName"])
        try boolFields(node, ["rewriteByteExact"])
        if let sha = node["rewriteSha256"] { try sha256(sha) }
        if let offset = node["byteOffset"] { _ = try integer(offset, min: 0) }
        if let section = node["sectionType"] { _ = try unsigned(section, max: UInt64(UInt16.max)) }
        if let entry = node["entryIndex"] { _ = try unsigned(entry, max: UInt64(Int32.max)) }
        try unsignedFields(node, ["requiredRegisterCount", "requiredCallStackDepth", "opaqueSectionCount"])
    }
    static func validateOpcodes(_ node: Node) throws {
        try closed(node, ["contains", "excludes", "counts", "minimumCounts"])
        var count = 0
        for key in ["contains", "excludes"] { if let list = node[key] { count += try stringList(list).count } }
        for key in ["counts", "minimumCounts"] {
            if let mapping = node[key] {
                for entry in try object(mapping) {
                    _ = try unsigned(entry.value)
                    count += 1
                }
            }
        }
        if count == 0 { throw fail("missingField", "Opcode expectations require constraints.", node) }
    }
    static func validateCompileMetadata(_ node: Node) throws {
        try closed(node, ["messageDefinitions", "programResources", "handlerResources"])
        guard !node.entries!.isEmpty else { throw fail("missingField", "Metadata requires a constraint.", node) }
        if let messages = node["messageDefinitions"] {
            for item in try array(messages) {
                try closed(item, ["name", "count", "signatureIds"])
                _ = try string(required(item, "name"))
                _ = try unsigned(required(item, "count"), max: UInt64(UInt32.max))
                _ = try stringList(required(item, "signatureIds"))
            }
        }
        if let resources = node["programResources"] {
            try closed(resources, ["requiredRegisterCount", "requiredCallStackDepth"])
            guard !resources.entries!.isEmpty else {
                throw fail("missingField", "Program resources require a constraint.", resources)
            }
            try unsignedFields(resources, ["requiredRegisterCount", "requiredCallStackDepth"])
        }
        if let handlers = node["handlerResources"] {
            for item in try array(handlers) {
                try closed(item, ["name", "signatureId", "requiredRegisterCount", "requiredCallStackDepth"])
                _ = try string(required(item, "name"))
                try stringFields(item, ["signatureId"])
                try unsignedFields(item, ["requiredRegisterCount", "requiredCallStackDepth"])
                guard item.entries!.count >= 2 else {
                    throw fail("missingField", "Handler resources require a constraint.", item)
                }
            }
        }
    }
    static func validatePerformance(_ node: Node) throws {
        try closed(node, ["profiles"])
        let profiles = try object(required(node, "profiles"))
        guard !profiles.isEmpty else { throw fail("invalidCardinality", "Performance profiles cannot be empty.", node) }
        for profile in profiles {
            try requireID(profile.key, profile.range)
            try closed(profile.value, ["metrics"])
            let metrics = try object(required(profile.value, "metrics"))
            guard !metrics.isEmpty else {
                throw fail("invalidCardinality", "Performance profiles require metrics.", profile.value)
            }
            for metric in metrics {
                try requireID(metric.key, metric.range)
                let item = metric.value
                try closed(item, ["reference", "maximum", "unit", "toleranceRelative", "toleranceAbsolute"])
                _ = try required(item, "reference")
                for field in ["reference", "maximum", "toleranceRelative", "toleranceAbsolute"] {
                    if let number = item[field] {
                        guard let text = number.number ?? number.string, let value = Double(text), value.isFinite,
                            value >= 0
                        else {
                            throw fail("invalidValue", "Performance values must be finite and nonnegative.", number)
                        }
                    }
                }
                guard ["maximum", "toleranceRelative", "toleranceAbsolute"].contains(where: { item[$0] != nil }) else {
                    throw fail("missingField", "Performance requires a maximum or tolerance.", item)
                }
                let unit = try string(required(item, "unit"))
                let base = unit.hasSuffix("/iteration") ? String(unit.dropLast(10)) : unit
                guard ["ns", "us", "ms", "s", "B", "KiB", "count"].contains(base) else {
                    throw fail("invalidValue", "Unknown performance unit.", item)
                }
            }
        }
    }
}
