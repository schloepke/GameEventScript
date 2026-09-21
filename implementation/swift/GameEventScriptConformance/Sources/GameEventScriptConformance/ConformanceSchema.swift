// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

internal enum ConformanceSchema {
    typealias Node = ConformanceYamlNode
    static let kinds = ["scriptApi", "compileError", "loadError", "messageApi", "valueApi", "externalTypeApi", "compileMetadata", "bytecode", "performance", "bytecodeSnapshot", "programBinary"]
    static let capabilities = ["compiler", "program-binary", "host", "vm", "message-api", "value-api", "external-types", "native-handlers", "publish-sink", "observer", "performance", "bytecode-snapshot"]
    static let limitNames = [
        "maxProcessedEventsPerRun", "maxQueuedMessagesPerRun", "maxExecutionSteps", "maxRegisterValues", "maxLoopIterations", "maxCallDepth", "maxRandomScopeDepth", "maxRangeItems", "maxGeneratedCollectionItems", "maxDiceCount", "maxDiceSides",
    ]
    static let defaultNames = ["kind", "level", "categories", "tags", "requires", "compile", "runtimeLimits", "comparison"]
    static let caseNames =
        ["gesBlock", "id", "sources", "random", "publishSink", "externalTypeRegistry", "hostCount", "deferredPrograms", "nativeHandlers", "stepActions", "messageApi", "valueApi", "externalTypeApi", "binaryFixture", "performance"] + defaultNames

    static func bind(_ syntax: ConformanceMarkdownSyntax, bytes: [UInt8], limits: ConformanceParserLimits) throws -> ConformanceDocument {
        let suite = try ConformanceYamlParser(syntax.frontmatter, limits: limits).parse()
        try closed(suite, ["formatVersion", "suiteId", "title"] + defaultNames)
        guard try integer(required(suite, "formatVersion"), min: Int64(Int32.min), max: Int64(Int32.max)) == 1 else { throw fail("unsupportedVersion", "Only formatVersion 1 is supported.", suite) }
        let suiteID = try identifier(required(suite, "suiteId"))
        let title = try optionalString(suite, "title") ?? suiteID
        try validateDefaults(suite)
        var result: [ConformanceCase] = []
        var ids: Set<String> = []
        for test in syntax.cases {
            var metadata: Node?
            var expectation: Node?
            var metadataRange: ConformanceSourceRange?
            var expectationRange: ConformanceSourceRange?
            for block in test.yaml {
                let yaml = try ConformanceYamlParser(block.lines, limits: limits).parse()
                let discriminator = try string(required(yaml, "gesBlock"))
                if discriminator == "case" {
                    guard metadata == nil else { throw fail("invalidCardinality", "Each test requires exactly one case block.", yaml) }
                    metadata = yaml
                    metadataRange = block.range
                } else if discriminator == "expect" {
                    guard expectation == nil else { throw fail("invalidCardinality", "Each test accepts at most one expect block.", yaml) }
                    expectation = yaml
                    expectationRange = block.range
                } else {
                    throw fail("invalidValue", "gesBlock is case or expect.", yaml)
                }
            }
            guard let node = metadata else { throw ConformanceParseError("conformance.schema.invalidCardinality", "A test requires case metadata.", test.range) }
            try closed(node, caseNames)
            try validateDefaults(node)
            let id = try identifier(required(node, "id"))
            guard ids.insert(id).inserted else { throw fail("duplicateId", "Duplicate case ID '\(id)'.", node) }
            let resolved = resolveDefaults(node.data, suite.data)
            guard let kind = resolved["kind"]?.stringValue, let level = resolved["level"]?.stringValue else { throw fail("missingField", "A case must resolve kind and level.", node) }
            try validateMetadata(node, limits: limits)
            if kind != "scriptApi" {
                if let count = node["hostCount"], try unsigned(count) > 1 { throw fail("invalidValue", "Multiple hosts are supported only by scriptApi.", count) }
                if !(node["deferredPrograms"]?.items ?? []).isEmpty { throw fail("invalidValue", "Deferred programs are supported only by scriptApi.", node) }
            }
            try validateSources(node, test, limits: limits)
            if let expectation { try validateExpectation(expectation, kind: kind) }
            try cardinality(node, expectation, kind: kind, syntax: test)
            let steps = try validateSteps(test.steps, expectation: expectation, actions: node["stepActions"])
            try validateHostReferences(node, test)
            let roundtrip = resolved["compile"]?["binaryRoundTrip"]?.boolValue == true
            if roundtrip && (kind == "programBinary" || (kind == "scriptApi" && test.sources.isEmpty)) { throw fail("invalidValue", "This case cannot request a compiler binary roundtrip.", node) }
            if kind == "messageApi" {
                if (node["messageApi"]?["compareConformanceMessage"] != nil) != (expectation?["message"]?["conformanceEquals"] != nil) { throw fail("invalidValue", "compareConformanceMessage and conformanceEquals must be supplied together.", node) }
                if node["messageApi"]?["message"]?["args"]?.entries != nil && expectation?["message"]?["error"]?.string != "invalidArgumentsShape" { throw fail("invalidValue", "Unordered arguments require expected invalidArgumentsShape.", node) }
            }
            var core = strings(resolved["requires"]?["core"])
            var optional = strings(resolved["requires"]?["optional"])
            switch kind {
            case "scriptApi", "performance": core = unique(core + ["compiler", "host", "vm", "observer", "publish-sink"])
            case "loadError": core = unique(core + ["compiler", "host", "vm"])
            case "messageApi": core = unique(core + ["message-api"])
            case "valueApi": core = unique(core + ["value-api"])
            case "externalTypeApi": core = unique(core + ["external-types"])
            case "programBinary": core = unique(core + ["program-binary"])
            default: core = unique(core + ["compiler"])
            }
            if kind == "performance" { optional = unique(optional + ["performance"]) }
            if test.assembler != nil { optional = unique(optional + ["bytecode-snapshot"]) }
            if kind == "programBinary" && !steps.isEmpty { core = unique(core + ["host", "vm", "observer", "publish-sink"]) }
            if node["binaryFixture"]?["compareCompiledRuntime"]?.boolean == true { core = unique(core + ["compiler"]) }
            if kind == "scriptApi" && test.sources.isEmpty { core.removeAll(where: { ["compiler", "vm", "publish-sink"].contains($0) }) }
            if !(node["nativeHandlers"]?.items ?? []).isEmpty { core = unique(core + ["native-handlers"]) }
            if roundtrip { core = unique(core + ["program-binary"]) }
            guard core.allSatisfy(capabilities.contains), optional.allSatisfy(capabilities.contains), Set(core).isDisjoint(with: optional) else { throw fail("invalidValue", "Capabilities must be known and cannot be both core and optional.", node) }
            let sources = test.sources.enumerated().map { index, block in
                let descriptor = node["sources"]?.items?[index]
                return ConformanceSourceInput(name: descriptor?["name"]?.string ?? suiteID + "." + id + ".ges", programID: descriptor?["program"]?.string ?? "main", text: block.payload, blockRange: block.range, payloadRange: block.payloadRange)
            }
            result.append(
                .init(
                    id: id,
                    fullID: suiteID + "/" + id,
                    title: test.title,
                    kind: kind,
                    level: level,
                    requiredCore: core,
                    requiredOptional: optional,
                    metadata: resolved,
                    expectation: expectation?.data ?? .object([]),
                    sourceBlocks: test.sources.map(\.payload),
                    sources: sources,
                    assembler: test.assembler?.payload,
                    steps: steps,
                    testRange: test.range,
                    caseMetadataRange: metadataRange!,
                    expectationRange: expectationRange,
                    stepsTableRange: test.stepsTableRange,
                    assemblerBlockRange: test.assembler?.range,
                    assemblerPayloadRange: test.assembler?.payloadRange
                )
            )
        }
        return .init(formatVersion: 1, suiteID: suiteID, title: title, cases: result, sourceBytes: bytes, frontmatter: suite.data, frontmatterRange: syntax.frontmatterRange)
    }

    static func validateDefaults(_ node: Node) throws {
        if let kind = node["kind"] { guard kinds.contains(try string(kind)) else { throw fail("unknownKind", "Unknown conformance test kind.", kind) } }
        if let level = node["level"] { try choice(level, ["atomic", "scenario"]) }
        if let categories = node["categories"] { _ = try stringList(categories, ids: true) }
        if let tags = node["tags"] { _ = try stringList(tags) }
        if let requires = node["requires"] {
            try closed(requires, ["core", "optional"])
            for field in ["core", "optional"] { if let list = requires[field] { _ = try stringList(list, ids: true) } }
        }
        if let compile = node["compile"] {
            try closed(compile, ["debugInfo", "binaryRoundTrip"])
            if let info = compile["debugInfo"] {
                let names = try stringList(info)
                guard names.count == Set(names).count, names.allSatisfy(["debugSymbols", "sourceMap", "sourceArchive"].contains) else { throw fail("invalidValue", "Unknown or repeated debugInfo option.", info) }
            }
            try boolFields(compile, ["binaryRoundTrip"])
        }
        if let runtime = node["runtimeLimits"] {
            try closed(runtime, limitNames)
            for field in runtime.entries! { guard try unsigned(field.value) > 0 else { throw fail("invalidValue", "Runtime limits must be positive.", field.value) } }
        }
        if let comparison = node["comparison"] {
            try closed(comparison, ["binary64"])
            let binary = try required(comparison, "binary64")
            try closed(binary, ["mode", "maxUlps"])
            let mode = try string(required(binary, "mode"))
            try choice(required(binary, "mode"), ["exact", "ulp"])
            if mode == "ulp" { _ = try unsigned(required(binary, "maxUlps")) } else if binary["maxUlps"] != nil { throw fail("invalidValue", "maxUlps applies only to ULP comparison.", binary) }
        }
    }

    static func resolveDefaults(_ child: ConformanceData, _ parent: ConformanceData) -> ConformanceData {
        var result = child
        for name in ["kind", "level"] { if child[name] == nil, let value = parent[name] { result = result.replacing(name, with: value) } }
        for name in ["categories", "tags"] { result = result.replacing(name, with: .array(unique(strings(parent[name]) + strings(child[name])).map(ConformanceData.string))) }
        let core = unique(strings(parent["requires"]?["core"]) + strings(child["requires"]?["core"]))
        let optional = unique(strings(parent["requires"]?["optional"]) + strings(child["requires"]?["optional"]))
        result = result.replacing("requires", with: .object([.init(key: "core", value: .array(core.map(ConformanceData.string))), .init(key: "optional", value: .array(optional.map(ConformanceData.string)))]))
        for name in ["compile", "runtimeLimits", "comparison"] {
            var merged: ConformanceData = .object([])
            if name == "compile" { merged = .object([.init(key: "debugInfo", value: .array(["debugSymbols", "sourceMap", "sourceArchive"].map(ConformanceData.string))), .init(key: "binaryRoundTrip", value: .bool(false))]) }
            if name == "comparison" { merged = .object([.init(key: "binary64", value: .object([.init(key: "mode", value: .string("exact"))]))]) }
            for field in (parent[name]?.objectValue ?? []) + (child[name]?.objectValue ?? []) { merged = merged.replacing(field.key, with: field.value) }
            result = result.replacing(name, with: merged)
        }
        return result
    }

    static func validateSources(_ node: Node, _ syntax: ConformanceMarkdownCase, limits: ConformanceParserLimits) throws {
        guard syntax.sources.count <= limits.maxSourcesPerTest, syntax.sources.reduce(0, { $0 + $1.payload.utf8.count }) <= limits.maxSourceBytesPerTest else {
            throw ConformanceParseError("conformance.yaml.limitExceeded", "Sources exceed parser limits.", node.range)
        }
        if let sources = node["sources"] {
            let items = try array(sources)
            guard items.count == syntax.sources.count else { throw fail("invalidCardinality", "sources must contain one descriptor per source fence.", sources) }
            for item in items {
                try closed(item, ["name", "program"])
                guard !(try string(required(item, "name"))).isEmpty else { throw fail("invalidValue", "Source names cannot be empty.", item) }
                if let program = item["program"] { _ = try identifier(program) }
            }
        } else if syntax.sources.count > 1 {
            throw fail("invalidCardinality", "Multiple source fences require source descriptors.", node)
        }
    }

    static func cardinality(_ node: Node, _ expect: Node?, kind: String, syntax: ConformanceMarkdownCase) throws {
        let api = ["messageApi", "valueApi", "externalTypeApi"].contains(kind)
        if api && !syntax.sources.isEmpty { throw fail("invalidCardinality", "API cases do not accept source fences.", node) }
        if !api && kind != "programBinary" && syntax.sources.isEmpty && !(kind == "scriptApi" && !(node["nativeHandlers"]?.items ?? []).isEmpty) { throw fail("invalidCardinality", "This case requires GES source.", node) }
        if kind == "bytecodeSnapshot" {
            guard syntax.assembler != nil && expect == nil else { throw fail("invalidCardinality", "A snapshot requires one gesa block and no expect block.", node) }
        } else if syntax.assembler != nil && kind != "programBinary" {
            throw fail("invalidCardinality", "Only bytecodeSnapshot and valid programBinary cases accept gesa.", node)
        }
        let expectationNames = [
            "compileError": "error", "loadError": "error", "messageApi": "message", "valueApi": "value", "externalTypeApi": "externalType", "compileMetadata": "metadata", "bytecode": "opcodes", "performance": "performance", "programBinary": "binary",
        ]
        if let field = expectationNames[kind] {
            guard let expect else { throw fail("invalidCardinality", "This case requires an expect block.", node) }
            _ = try required(expect, field)
        }
        for (field, owner) in [("messageApi", "messageApi"), ("valueApi", "valueApi"), ("externalTypeApi", "externalTypeApi"), ("binaryFixture", "programBinary"), ("performance", "performance")] {
            if kind == owner { _ = try required(node, field) } else if node[field] != nil { throw fail("unknownField", "\(field) is only valid for \(owner) cases.", node) }
        }
        if node["binaryFixture"]?["compareCompiledRuntime"]?.boolean == true && syntax.sources.isEmpty { throw fail("missingField", "compareCompiledRuntime requires source.", node) }
        if ["scriptApi", "performance"].contains(kind) {
            guard syntax.hasSteps || expect?["initialization"] != nil else { throw fail("invalidCardinality", "Runtime cases require Steps or initialization expectations.", node) }
        } else if kind == "programBinary" {
            if syntax.assembler != nil && expect?["binary"]?["outcome"]?.string != "valid" { throw fail("invalidCardinality", "Only valid programBinary cases accept gesa.", node) }
            if syntax.hasSteps && expect?["binary"]?["outcome"]?.string != "valid" { throw fail("invalidCardinality", "Only valid programBinary cases accept Steps.", node) }
            if !syntax.hasSteps && expect?["initialization"] != nil { throw fail("invalidCardinality", "Binary initialization expectations require Steps.", node) }
        } else if syntax.hasSteps {
            throw fail("invalidCardinality", "This test kind does not accept Steps.", node)
        }
    }

    static func validateSteps(_ steps: [ConformanceMarkdownStep], expectation: Node?, actions: Node?) throws -> [ConformanceStep] {
        var known: Set<String> = []
        var result: [ConformanceStep] = []
        for step in steps {
            try requireID(step.id, step.range)
            guard known.insert(step.id).inserted else { throw ConformanceParseError("conformance.schema.duplicateId", "Duplicate Step ID.", step.range) }
            guard !step.receive.isEmpty && ["completion", "frames", "enqueue", "frame"].contains(step.pump) else { throw ConformanceParseError("conformance.schema.invalidValue", "Invalid Steps receive/pump.", step.range) }
            var budget: Int?
            if ["frames", "frame"].contains(step.pump) {
                guard !step.budget.isEmpty, step.budget.utf8.allSatisfy({ (48...57).contains($0) }), let parsed = UInt32(step.budget), parsed > 0 else {
                    throw ConformanceParseError("conformance.markdown.invalidStepsTable", "Frame budgets must be positive UInt32.", step.range)
                }
                budget = Int(parsed)
            } else if !step.budget.isEmpty {
                throw ConformanceParseError("conformance.markdown.invalidStepsTable", "Completion/enqueue budgets must be empty.", step.range)
            }
            result.append(.init(id: step.id, receive: .string(step.receive), pump: step.pump, budget: budget, range: step.range))
        }
        for mapping in [expectation?["steps"], actions].compactMap({ $0 }) { for entry in try object(mapping) { guard known.contains(entry.key) else { throw fail("unknownReference", "Unknown Step ID '\(entry.key)'.", entry.value) } } }
        return result
    }

    static func fail(_ code: String = "invalidValue", _ message: String, _ node: Node) -> ConformanceParseError { .init("conformance.schema.\(code)", message, node.range) }

    static func required(_ node: Node, _ field: String) throws -> Node {
        guard let value = node[field] else { throw fail("missingField", "Missing required field '\(field)'.", node) }
        return value
    }

    static func object(_ node: Node) throws -> [ConformanceYamlEntry] {
        guard let entries = node.entries else { throw fail("invalidValue", "Expected a mapping.", node) }
        return entries
    }

    static func array(_ node: Node) throws -> [Node] {
        guard let values = node.items else { throw fail("invalidValue", "Expected a sequence.", node) }
        return values
    }

    static func closed(_ node: Node, _ fields: [String]) throws { for entry in try object(node) { if !fields.contains(entry.key) { throw fail("unknownField", "Unknown field '\(entry.key)'.", entry.value) } } }

    static func string(_ node: Node) throws -> String {
        guard let value = node.string else { throw fail("invalidValue", "Expected a string.", node) }
        return value
    }

    static func boolean(_ node: Node) throws -> Bool {
        guard let value = node.boolean else { throw fail("invalidValue", "Expected a Boolean.", node) }
        return value
    }

    static func optionalString(_ node: Node, _ field: String) throws -> String? { try node[field].map(string) }

    static func stringList(_ node: Node, ids: Bool = false) throws -> [String] { try array(node).map { ids ? try identifier($0) : try string($0) } }

    static func boolFields(_ node: Node, _ fields: [String]) throws { for field in fields { if let value = node[field] { _ = try boolean(value) } } }

    static func stringFields(_ node: Node, _ fields: [String]) throws { for field in fields { if let value = node[field] { _ = try string(value) } } }

    static func unsignedFields(_ node: Node, _ fields: [String], max: UInt64 = UInt64(UInt32.max)) throws { for field in fields { if let value = node[field] { _ = try unsigned(value, max: max) } } }

    static func integer(_ node: Node, min: Int64 = .min, max: Int64 = .max) throws -> Int64 {
        guard let text = node.string ?? node.number, validInteger(text), let value = Int64(text), value >= min, value <= max else { throw fail("invalidValue", "Expected a bounded signed integer.", node) }
        return value
    }

    static func unsigned(_ node: Node, max: UInt64 = .max) throws -> UInt64 {
        guard let text = node.string ?? node.number, !text.isEmpty, text.utf8.allSatisfy({ (48...57).contains($0) }), let value = UInt64(text), value <= max else { throw fail("invalidValue", "Expected a bounded unsigned integer.", node) }
        return value
    }

    static func validInteger(_ text: String) -> Bool {
        var bytes = text.utf8[...]
        if bytes.first == 45 { bytes = bytes.dropFirst() }
        return !bytes.isEmpty && bytes.allSatisfy({ (48...57).contains($0) })
    }

    static func choice(_ node: Node, _ choices: [String]) throws { guard choices.contains(try string(node)) else { throw fail("invalidValue", "Expected one of: \(choices.joined(separator: ", ")).", node) } }

    static func identifier(_ node: Node) throws -> String {
        let value = try string(node)
        try requireID(value, node.range)
        return value
    }

    static func requireID(_ value: String, _ range: ConformanceSourceRange) throws {
        let bytes = Array(value.utf8)
        var separator = false
        var valid = !bytes.isEmpty && (97...122).contains(bytes[0])
        for byte in bytes.dropFirst() {
            if byte == 46 || byte == 45 {
                if separator { valid = false }
                separator = true
            } else {
                if !(97...122).contains(byte) && !(48...57).contains(byte) { valid = false }
                separator = false
            }
        }
        if !valid || separator { throw ConformanceParseError("conformance.schema.invalidValue", "Invalid portable identifier '\(value)'.", range) }
    }

    static func unique(_ values: [String]) -> [String] {
        var seen: Set<[UInt8]> = []
        return values.filter { seen.insert(Array($0.utf8)).inserted }
    }

    static func strings(_ value: ConformanceData?) -> [String] { value?.arrayValue?.compactMap(\.stringValue) ?? [] }
}
