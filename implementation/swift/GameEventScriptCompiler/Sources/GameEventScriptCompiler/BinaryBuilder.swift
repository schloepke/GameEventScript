// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// Collects the lowered routines and the string, list and binding pools of one compilation, then optimizes,
// allocates, analyzes and encodes them into a Program, as GesBinaryBuilder of the C# builder.
final class GesBinaryBuilder {
    let sources: [GesSource], options: GesCompileOptions
    // Location of program-wide limit diagnostics.
    let fallbackLocation: GesSourceLocation
    var strings: [String] = [], lists: [[UInt16]] = [], bindings: [GesBindingSegment.Entry] = []
    // Scalar keys preserve the language's identity instead of Swift's canonical equivalence.
    var stringIDs: [[UInt32]: Int] = [:], listIDs: [[UInt16]: Int] = [:]
    var numericLimitExceeded = false
    var imports: [String: Int] = [:], recordIDs: [String: Int] = [:]
    var routines: [GesRoutine] = []
    // Operand slot layouts per opcode shape, shared by every routine of this compilation.
    var operandLayouts: [Int: OperandSlots] = [:]
    var sourcePositions: [UInt32: [UInt64: Int]] = [:]

    init(sources: [GesSource], options: GesCompileOptions, fallbackLocation: GesSourceLocation) {
        self.sources = sources
        self.options = options
        self.fallbackLocation = fallbackLocation
    }

    func text(_ value: String) -> Int {
        let key = value.unicodeScalars.map(\.value)
        if let i = stringIDs[key] { return i }
        guard strings.count < 65535 else {
            numericLimitExceeded = true
            return 0
        }
        let id = strings.count
        strings.append(value)
        stringIDs[key] = id
        return id
    }

    func list(_ values: [Int]) -> Int {
        guard values.count < 65535, values.allSatisfy({ $0 >= 0 && $0 < 65535 }) else {
            numericLimitExceeded = true
            return 0
        }
        let values = values.map { UInt16($0) }
        if let i = listIDs[values] { return i }
        guard lists.count < 65535 else {
            numericLimitExceeded = true
            return 0
        }
        let id = lists.count
        lists.append(values)
        listIDs[values] = id
        return id
    }

    func textList(_ names: [String]) -> Int { list(names.map(text)) }

    func importBinding(_ kind: GesBinaryBindKind, _ name: String, _ labels: [String]) -> Int {
        let key = "\(kind.rawValue):" + callableSignature(name, labels)
        if let id = imports[key] { return id }
        guard bindings.count < 65535 else {
            numericLimitExceeded = true
            return 0
        }
        let id = bindings.filter { $0.kind == kind }.count
        bindings.append(.init(kind: kind, name: UInt16(truncatingIfNeeded: text(name)), argumentNames: labels.map { UInt16(truncatingIfNeeded: text($0)) }, id: UInt16(truncatingIfNeeded: id)))
        imports[key] = id
        return id
    }

    func build(moduleName: String) throws -> GameEventScriptProgram {
        let effects = EffectAnalysis(self)
        let context = routines.first.map { RewriteContext(self, $0, effects) }
        for routine in routines {
            try checkLimits(routine)
            if let context { optimize(routine, context) }
            try allocate(routine)
            try checkLimits(routine)
            analyzeResources(routine, effects)
        }
        return try finish(moduleName)
    }

    // Calls and argument staging of the final code, as the C# resource analysis reads them after optimization.
    private static let stages: Set<GesBytecodeOpCode> = [.stageRegister, .stageNothing, .stageTrue, .stageFalse, .stageInteger, .stageFloat, .stageText, .stageTag, .stagePercentage]

    private static let clears: Set<GesBytecodeOpCode> = [.createVector, .createPoint, .createList, .createMap, .createExternalType, .returnVoid, .returnValue]

    func analyzeResources(_ r: GesRoutine, _ effects: EffectAnalysis) {
        var dependencies: Set<String> = []
        var stage = 0
        var maxStage = 0
        for item in r.items {
            guard let instruction = item.instruction else { continue }
            if Self.stages.contains(instruction.opcode) {
                stage += 1
                maxStage = max(maxStage, stage)
            }
            switch instruction.opcode {
            case .call:
                dependencies.insert(r.calls[Int(instruction.word2)])
                stage = 0
            case .createRecord:
                if let key = effects.recordKeysByID[Int(instruction.word1)] { dependencies.insert(key) }
                stage = 0
            case .castCustom:
                if Int(instruction.word2) < strings.count, let key = effects.recordKeys[strings[Int(instruction.word2)]] { dependencies.insert(key) }
            default:
                if Self.clears.contains(instruction.opcode) { stage = 0 }
            }
        }
        r.dependencies = dependencies
        r.maxStage = maxStage
    }

    func checkLimits(_ routine: GesRoutine? = nil) throws {
        if numericLimitExceeded || (routine?.registers ?? 0) >= 65535 || (routine?.instructionCount ?? 0) >= 65535 {
            throw compileError("compile.numericLimitExceeded", routine?.location ?? fallbackLocation, phase: .compile)
        }
    }

    private func finish(_ moduleName: String) throws -> GameEventScriptProgram {
        var moduleName = moduleName
        var addresses: [String: Int] = [:]
        var address = 0
        for r in routines {
            addresses[r.key] = address
            address += r.instructionCount
            if r.registers - r.definition.parameters.count > Int(Int16.max) || r.registers >= 65535 { throw compileError("compile.numericLimitExceeded", r.location, phase: .compile) }
            // The prolog is the first instruction; it reserves the allocated locals.
            let prolog = r.items.firstIndex { $0.instruction != nil }!
            guard case .instruction(_, let location) = r.items[prolog] else { preconditionFailure() }
            r.items[prolog] = .instruction(.init(opcode: .registerLocals, word1: UInt16(r.registers - r.definition.parameters.count)), location: location)
        }
        if address >= 65535 || strings.count >= 65535 || lists.count >= 65535 { throw compileError("compile.numericLimitExceeded", fallbackLocation, phase: .compile) }
        var resources: [String: (Int, Int)] = [:]
        let routinesByName = Dictionary(uniqueKeysWithValues: routines.map { ($0.key, $0) })

        func requirements(_ name: String) throws -> (Int, Int) {
            if let result = resources[name] { return result }
            var visiting: Set<String> = []
            var pending = [(name, false)]
            while let (key, expanded) = pending.popLast() {
                if resources[key] != nil { continue }
                guard let r = routinesByName[key] else { throw compileError("compile.unresolvedSymbol", routines[0].location, symbol: key, phase: .compile) }
                if expanded {
                    var regs = r.registers + r.maxStage
                    var depth = 0
                    for target in r.dependencies {
                        let (childRegs, childDepth) = resources[target]!
                        regs = max(regs, r.registers + childRegs)
                        depth = max(depth, childDepth + 1)
                    }
                    visiting.remove(key)
                    resources[key] = (regs, depth)
                } else {
                    guard visiting.insert(key).inserted else { throw compileError("compile.cyclicCallGraph", r.location, symbol: key, phase: .compile) }
                    pending.append((key, true))
                    for target in r.dependencies.sorted().reversed() where resources[target] == nil { pending.append((target, false)) }
                }
            }
            return resources[name]!
        }

        var code: [GesBytecodeInstruction] = []
        var spans: [GesSourceMapEntry] = []
        var symbols: [GesDebugSymbol] = []
        var routineSymbols: [String: [GesDebugSymbol]] = [:]
        var handlerIDs: [String: Int] = [:]
        var maxRegs = 0
        var maxDepth = 0
        var executable: [GesBindingSegment.Entry] = []
        for r in routines {
            let d = r.definition
            let offset = code.count
            let isHandler = d.kind.hasSuffix("andler")
            // Calls resolve before the routine is encoded, so an unknown callee fails before any of its code.
            for target in r.calls where addresses[target] == nil { throw compileError("compile.unresolvedSymbol", r.location, symbol: target, phase: .compile) }
            let labels = r.labelAddresses()
            for item in r.items {
                guard case .instruction(var instruction, let location) = item else { continue }
                // Labels and call IDs become code addresses only here.
                let target: Int? =
                    Self.isBranch(instruction.opcode) ? offset + labels[Int(instruction.word2)] : instruction.opcode == .call ? addresses[r.calls[Int(instruction.word2)]]! : nil
                if let target {
                    instruction = .init(opcode: instruction.opcode, unitAndFlags: instruction.unitAndFlags, word0: instruction.word0, word1: instruction.word1, word2: UInt16(target), payload: instruction.payload)
                }
                let address = code.count
                code.append(instruction)
                if options.debugInfo.contains(.sourceMap), let span = sourceSpan(r.locations[location], address: address) {
                    if let previous = spans.last, previous.codeStart + previous.codeLength == span.codeStart, previous.sourceID == span.sourceID,
                        previous.sourceStartByteOffset == span.sourceStartByteOffset, previous.sourceByteLength == span.sourceByteLength
                    {
                        spans[spans.count - 1] = .init(
                            codeStart: previous.codeStart,
                            codeLength: previous.codeLength + span.codeLength,
                            sourceID: previous.sourceID,
                            sourceStartByteOffset: previous.sourceStartByteOffset,
                            sourceByteLength: previous.sourceByteLength
                        )
                    } else {
                        spans.append(span)
                    }
                }
            }
            let kind: GesBinaryBindKind = d.kind == "handler" ? .messageHandler : d.kind == "messageNameHandler" ? .messageNameHandler : d.kind == "function" ? .function : d.kind == "predicate" ? .predicate : .record
            let id: Int
            if isHandler {
                id = handlerIDs[d.name] ?? 0
                handlerIDs[d.name] = id + 1
            } else if d.kind == "record" {
                id = recordIDs[d.name]!
            } else {
                id = executable.filter { $0.kind == kind }.count
            }
            let (registers, depth) = try requirements(r.key)
            if registers > 65535 || depth > 65535 { throw compileError("compile.numericLimitExceeded", d.location, phase: .compile) }
            if isHandler {
                maxRegs = max(maxRegs, registers)
                maxDepth = max(maxDepth, depth)
            }
            executable.append(
                .init(
                    kind: kind,
                    name: UInt16(text(d.name)),
                    argumentNames: d.parameters.map { UInt16(text(d.argumentName($0))) },
                    entryAddress: UInt16(offset),
                    id: UInt16(id),
                    requiredTags: d.required.map { UInt16(text($0)) },
                    excludedTags: d.excluded.map { UInt16(text($0)) },
                    requiredRegisterCount: isHandler ? UInt16(registers) : 0,
                    requiredCallStackDepth: isHandler ? UInt16(depth) : 0
                )
            )
            if options.debugInfo.contains(.symbols) {
                for (name, reg, parameter) in r.symbols {
                    routineSymbols[r.key, default: []].append(.init(kind: parameter ? .parameter : .local, registerID: UInt16(reg), name: name, codeStart: UInt32(offset), codeLength: UInt32(code.count - offset)))
                }
            }
        }
        // C# register creation order: routines are emitted callables first, then records, then handlers, and each
        // creates its arguments when it begins, followed by its locals.
        let callables = routines.filter { ["function", "predicate"].contains($0.definition.kind) }.sorted { $0.definition.signature < $1.definition.signature }
        let recordRoutines = routines.filter { $0.definition.kind == "record" }.sorted { $0.definition.name < $1.definition.name }
        for r in callables + recordRoutines + routines.filter({ $0.definition.kind.hasSuffix("andler") }) { symbols += routineSymbols[r.key, default: []] }
        try checkLimits()
        let materialized = materialize(executable + bindings, code)
        if moduleName.isEmpty { moduleName = anonymousName(materialized.0, materialized.1, maxRegs, maxDepth) }
        _ = text(moduleName)
        try checkLimits()
        let sourceMap = options.debugInfo.contains(.sourceMap) ? GesSourceMapSegment(sources: sourceDescriptors(), entries: spans) : nil
        return try GesCompilerSupport.program(
            moduleName: moduleName,
            programVersion: options.programVersion,
            requiredRegisterCount: UInt16(maxRegs),
            requiredCallStackDepth: UInt16(maxDepth),
            strings: strings,
            indexLists: lists,
            bindings: materialized.0,
            code: materialized.1,
            debugSymbols: options.debugInfo.contains(.symbols) ? symbols : nil,
            sourceMap: sourceMap,
            sourceArchive: options.debugInfo.contains(.sourceArchive) ? sources.map { .init(sourceID: $0.id, sourceName: $0.name, utf8Content: Array($0.text.utf8)) } : nil,
            buildMetadata: .init(compilerID: GesCompilerMetadata.compilerID, compilerVersion: GesCompilerMetadata.compilerVersion)
        )
    }

    func sourceDescriptors() -> [GesSourceMapSource] {
        sources.map { source in
            let bytes = Array(source.text.utf8)
            var starts: [UInt32] = [0]
            for i in bytes.indices { if bytes[i] == 10 || bytes[i] == 13 && (i + 1 == bytes.count || bytes[i + 1] != 10) { starts.append(UInt32(i + 1)) } }
            return .init(sourceID: source.id, sourceName: source.name, sourceByteLength: UInt32(bytes.count), sha256: GesCompilerSupport.sha256(bytes), lineStartByteOffsets: starts)
        }
    }

    func anonymousName(_ bindings: [GesBindingSegment.Entry], _ code: [GesBytecodeInstruction], _ registers: Int, _ depth: Int) -> String {
        var hash: UInt32 = 2_166_136_261

        func mix(_ value: UInt64, _ count: Int) { for shift in 0..<count { hash = (hash ^ UInt32(UInt8(truncatingIfNeeded: value >> (shift * 8)))) &* 16_777_619 } }

        mix(1, 2)
        mix(options.programVersion, 8)
        mix(UInt64(registers), 2)
        mix(UInt64(depth), 2)
        for string in strings {
            mix(UInt64(string.utf16.count), 4)
            for c in string.utf16 { mix(UInt64(c), 2) }
        }
        for list in lists {
            mix(UInt64(list.count), 4)
            for value in list { mix(UInt64(value), 2) }
        }
        for b in bindings {
            mix(UInt64(b.kind.rawValue), 4)
            mix(UInt64(b.name), 2)
            mix(UInt64(b.argumentNames.count), 4)
            for value in b.argumentNames { mix(UInt64(value), 2) }
            for value in [b.entryAddress, b.id, b.requiredRegisterCount, b.requiredCallStackDepth] { mix(UInt64(value), 2) }
            for list in [b.requiredTags, b.excludedTags] {
                mix(UInt64(list.count), 4)
                for value in list { mix(UInt64(value), 2) }
            }
        }
        for i in code {
            mix(UInt64(i.opcode.rawValue), 1)
            mix(UInt64(i.unitAndFlags), 1)
            for value in [i.word0, i.word1, i.word2] { mix(UInt64(value), 2) }
            mix(i.payload, 8)
        }
        let hex = String(hash, radix: 16)
        return "anonymous.m" + String(repeating: "0", count: 8 - hex.count) + hex
    }

    func sourceSpan(_ location: GesSourceLocation, address: Int) -> GesSourceMapEntry? {
        guard let id = location.sourceID, Int(id) < sources.count else { return nil }

        func key(_ line: Int?, _ column: Int?) -> UInt64 { UInt64(line ?? 0) << 32 | UInt64(column ?? 0) }

        if sourcePositions[id] == nil {
            var needed: Set<UInt64> = []
            for r in routines {
                for location in r.locations where location.sourceID == id {
                    needed.insert(key(location.line, location.column))
                    needed.insert(key(location.endLine, location.endColumn))
                }
            }
            var positions: [UInt64: Int] = [:]
            var line = 1
            var column = 1
            var offset = 0
            var previousCR = false
            for c in sources[Int(id)].text.unicodeScalars {
                let k = key(line, column)
                if needed.contains(k) { positions[k] = offset }
                // Scalar UTF8View requires newer Apple deployment targets; the encoded width is platform-independent.
                offset += c.value < 0x80 ? 1 : c.value < 0x800 ? 2 : c.value < 0x10000 ? 3 : 4
                if c == "\r" {
                    line += 1
                    column = 1
                    previousCR = true
                } else if c == "\n" {
                    if !previousCR { line += 1 }
                    column = 1
                    previousCR = false
                } else {
                    column += 1
                    previousCR = false
                }
            }
            positions[key(line, column)] = offset
            sourcePositions[id] = positions
        }
        guard let start = sourcePositions[id]?[key(location.line, location.column)], let end = sourcePositions[id]?[key(location.endLine, location.endColumn)], end >= start else { return nil }
        return .init(codeStart: UInt32(address), codeLength: 1, sourceID: id, sourceStartByteOffset: UInt32(start), sourceByteLength: UInt32(end - start))
    }
}
