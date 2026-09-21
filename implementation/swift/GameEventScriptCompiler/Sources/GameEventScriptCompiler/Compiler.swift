// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

typealias Op = GameEventScriptBytecodeOpCode
typealias Instruction = GameEventScriptBytecodeInstruction

final class GesRoutine {
    let definition: GesDefinition
    var key: String { definition.kind.hasSuffix("andler") ? definition.signature + "@\(definition.location.sourceID ?? 0):\(definition.location.line ?? 0):\(definition.location.column ?? 0)" : definition.signature }
    var code: [Instruction] = []
    var locations: [GameEventScriptSourceLocation] = []
    var pinned: Set<Int> = []
    var registers: Int
    var maxStage = 0
    var calls: [(Int, String)] = []
    var dependencies: Set<String> = []
    var symbols: [(String, Int, Int, Bool)] = []
    var location: GameEventScriptSourceLocation

    init(_ definition: GesDefinition) {
        self.definition = definition
        registers = definition.parameters.count
        location = definition.location
        pinned = Set(0..<registers)
    }

    func local() -> Int {
        let register = temporary()
        pinned.insert(register)
        return register
    }

    func temporary() -> Int {
        let result = registers
        registers += 1
        return result
    }

    @discardableResult func emit(_ op: Op, _ d: Int = 0, _ x: Int = 0, _ y: Int = 0, payload: UInt64 = 0, flags: UInt8 = 0) -> Int {
        let i = code.count
        code.append(.init(opcode: op, unitAndFlags: flags, word0: UInt16(truncatingIfNeeded: d), word1: UInt16(truncatingIfNeeded: x), word2: UInt16(truncatingIfNeeded: y), payload: payload))
        locations.append(location)
        return i
    }

    func patch(_ i: Int, target: Int) {
        let old = code[i]
        code[i] = .init(opcode: old.opcode, unitAndFlags: old.unitAndFlags, word0: old.word0, word1: old.word1, word2: UInt16(truncatingIfNeeded: target), payload: old.payload)
    }
}

final class GesScope {
    var values: [String: Int] = [:]
    var handlers: [String: [String]] = [:]
    let parent: GesScope?

    init(_ parent: GesScope? = nil) { self.parent = parent }

    func get(_ name: String) -> Int? { values[name] ?? parent?.get(name) }

    func handler(_ name: String) -> [String]? { handlers[name] ?? parent?.handler(name) }
}

final class GesCompiler {
    let modules: [GesModule], sources: [GesSource], options: GameEventScriptCompileOptions
    let catalog: (any GameEventScriptExternalTypeCatalogProtocol)?
    var moduleName = ""
    var constants: [String: GesExpression] = [:], definitions: [String: GesDefinition] = [:], records: [String: GesRecord] = [:]
    var strings: [String] = [], lists: [[UInt16]] = [], bindings: [GameEventScriptBinding] = []
    // Scalar keys preserve the language's identity instead of Swift's canonical equivalence.
    var stringIDs: [[UInt32]: Int] = [:], listIDs: [[UInt16]: Int] = [:]
    var numericLimitExceeded = false
    var imports: [String: Int] = [:], recordIDs: [String: Int] = [:]
    var routines: [GesRoutine] = []
    var sourcePositions: [UInt32: [UInt64: Int]] = [:]

    init(modules: [GesModule], sources: [GesSource], catalog: (any GameEventScriptExternalTypeCatalogProtocol)?, options: GameEventScriptCompileOptions) {
        self.modules = modules
        self.sources = sources
        self.catalog = catalog
        self.options = options
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

    func importBinding(_ kind: GameEventScriptBinaryBindKind, _ name: String, _ labels: [String]) -> Int {
        let key = "\(kind.rawValue):" + signature(name, labels)
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

    func signature(_ name: String, _ labels: [String]) -> String { name + "(" + labels.joined(separator: ",") + ")" }

    func error(_ code: String, _ location: GameEventScriptSourceLocation, symbol: String? = nil, kind: GameEventScriptSymbolKind = .unknown, phase: GameEventScriptDiagnosticPhase = .validate) -> GameEventScriptCompileError {
        compileError(phase, code, code, location, symbol: symbol, kind: kind)
    }

    func compile() throws -> GameEventScriptProgram {
        try validate()
        let callables = definitions.values.sorted { $0.signature < $1.signature }
        for definition in callables { routines.append(try routine(definition)) }
        for (id, record) in records.values.sorted(by: { $0.name < $1.name }).enumerated() { recordIDs[record.name] = id }
        for record in records.values.sorted(by: { $0.name < $1.name }) { routines.append(try recordRoutine(record)) }
        let handlers = modules.flatMap(\.definitions).enumerated().filter { $0.element.kind.hasSuffix("andler") }.sorted { $0.element.name == $1.element.name ? $0.offset < $1.offset : $0.element.name < $1.element.name }
        for entry in handlers { routines.append(try routine(entry.element)) }
        let order = ["handler": 0, "messageNameHandler": 0, "record": 1, "predicate": 2, "function": 3]
        routines = routines.enumerated().sorted { a, b in
            let ak = order[a.element.definition.kind] ?? 4
            let bk = order[b.element.definition.kind] ?? 4
            return ak == bk ? a.offset < b.offset : ak < bk
        }.map(\.element)
        for routine in routines {
            try checkLimits(routine)
            optimize(routine)
            try allocate(routine)
            try checkLimits(routine)
        }
        return try finish()
    }

    func checkLimits(_ routine: GesRoutine? = nil) throws {
        if numericLimitExceeded || (routine?.registers ?? 0) >= 65535 || (routine?.code.count ?? 0) >= 65535 {
            throw error("compile.numericLimitExceeded", routine?.location ?? modules.first?.location ?? .init(sourceName: "UnknownSource"), phase: .compile)
        }
    }

    func routine(_ definition: GesDefinition) throws -> GesRoutine {
        let r = GesRoutine(definition)
        let scope = GesScope()
        r.emit(.registerLocals)
        for (i, parameter) in definition.parameters.enumerated() {
            scope.values[parameter.name] = i
            r.symbols.append((parameter.label, i, 0, true))
            if let type = parameter.type { try cast(i, i, type, r) }
        }
        if let value = definition.expression {
            let result = try expression(value, r, scope)
            r.emit(.returnValue, 0, result)
        } else {
            try statements(definition.statements, r, scope)
            r.location = definition.location
            r.emit(.returnVoid)
        }
        return r
    }

    func recordRoutine(_ record: GesRecord) throws -> GesRoutine {
        let params = record.fields.compactMap { f in f.label.map { GesParameter(label: $0, name: f.name, type: f.type, location: f.location) } }
        let d = GesDefinition(kind: "record", name: record.name, parameters: params, expression: nil, statements: [], required: [], excluded: [], location: record.location)
        let r = GesRoutine(d)
        let scope = GesScope()
        r.emit(.registerLocals)
        for (i, p) in params.enumerated() {
            scope.values[p.name] = i
            r.symbols.append((p.label, i, 0, true))
        }
        for field in record.fields where field.computed != nil {
            let reg = r.local()
            scope.values[field.name] = reg
            r.symbols.append((field.name, reg, 0, false))
        }
        for field in record.fields {
            r.location = field.location
            let reg = scope.get(field.name)!
            if let computed = field.computed {
                let value = try expression(computed, r, scope)
                try cast(reg, value, field.type, r)
            } else {
                try cast(reg, reg, field.type, r)
                if let low = field.minimum, let high = field.maximum {
                    let a = try expression(low, r, scope)
                    let b = try expression(high, r, scope)
                    r.emit(.clamp, reg, reg, a, payload: UInt64(b))
                    try cast(reg, reg, field.type, r)
                }
            }
        }
        r.location = record.location
        stage(record.fields.map { scope.get($0.name)! }, r)
        let map = r.temporary()
        let result = r.temporary()
        r.emit(.createMap, map, textList(record.fields.map(\.name)))
        r.emit(.createRecordValue, result, map, text(record.name))
        r.emit(.returnValue, 0, result)
        return r
    }

    func finish() throws -> GameEventScriptProgram {
        var addresses: [String: Int] = [:]
        var address = 0
        for r in routines {
            addresses[r.key] = address
            address += r.code.count
            if r.registers - r.definition.parameters.count > Int(Int16.max) || r.registers >= 65535 { throw error("compile.numericLimitExceeded", r.location, phase: .compile) }
            r.code[0] = .init(opcode: .registerLocals, word1: UInt16(r.registers - r.definition.parameters.count))
        }
        if address >= 65535 || strings.count >= 65535 || lists.count >= 65535 { throw error("compile.numericLimitExceeded", modules.first?.location ?? .init(sourceName: "UnknownSource"), phase: .compile) }
        var resources: [String: (Int, Int)] = [:]
        let routinesByName = Dictionary(uniqueKeysWithValues: routines.map { ($0.key, $0) })

        func requirements(_ name: String) throws -> (Int, Int) {
            if let result = resources[name] { return result }
            var visiting: Set<String> = []
            var pending = [(name, false)]
            while let (key, expanded) = pending.popLast() {
                if resources[key] != nil { continue }
                guard let r = routinesByName[key] else { throw error("compile.unresolvedSymbol", routines[0].location, symbol: key, phase: .compile) }
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
                    guard visiting.insert(key).inserted else { throw error("compile.cyclicCallGraph", r.location, symbol: key, phase: .compile) }
                    pending.append((key, true))
                    for target in r.dependencies.sorted().reversed() where resources[target] == nil { pending.append((target, false)) }
                }
            }
            return resources[name]!
        }

        var code: [Instruction] = []
        var spans: [GameEventScriptSourceMapEntry] = []
        var symbols: [GameEventScriptDebugSymbol] = []
        var handlerIDs: [String: Int] = [:]
        var maxRegs = 0
        var maxDepth = 0
        var executable: [GameEventScriptBinding] = []
        for r in routines {
            let d = r.definition
            let offset = code.count
            let isHandler = d.kind.hasSuffix("andler")
            for (i, target) in r.calls {
                guard let address = addresses[target] else { throw error("compile.unresolvedSymbol", r.location, symbol: target, phase: .compile) }
                r.patch(i, target: address)
            }
            for i in r.code.indices {
                var instruction = r.code[i]
                if [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorNext, .iteratorCreateOrJump].contains(instruction.opcode) {
                    instruction = .init(opcode: instruction.opcode, unitAndFlags: instruction.unitAndFlags, word0: instruction.word0, word1: instruction.word1, word2: UInt16(offset + Int(instruction.word2)), payload: instruction.payload)
                }
                code.append(instruction)
                if options.debugInfo.contains(.sourceMap), let span = sourceSpan(r.locations[i], address: offset + i) { spans.append(span) }
            }
            let kind: GameEventScriptBinaryBindKind = d.kind == "handler" ? .messageHandler : d.kind == "messageNameHandler" ? .messageNameHandler : d.kind == "function" ? .function : d.kind == "predicate" ? .predicate : .record
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
            if registers > 65535 || depth > 65535 { throw error("compile.numericLimitExceeded", d.location, phase: .compile) }
            if isHandler {
                maxRegs = max(maxRegs, registers)
                maxDepth = max(maxDepth, depth)
            }
            executable.append(
                .init(
                    kind: kind,
                    name: UInt16(text(d.name)),
                    argumentNames: d.parameters.map { UInt16(text($0.label)) },
                    entryAddress: UInt16(offset),
                    id: UInt16(id),
                    requiredTags: d.required.map { UInt16(text($0)) },
                    excludedTags: d.excluded.map { UInt16(text($0)) },
                    requiredRegisterCount: isHandler ? UInt16(registers) : 0,
                    requiredCallStackDepth: isHandler ? UInt16(depth) : 0
                )
            )
            if options.debugInfo.contains(.symbols) {
                for (name, reg, start, parameter) in r.symbols where start < r.code.count {
                    symbols.append(.init(kind: parameter ? .parameter : .local, registerID: UInt16(reg), name: name, codeStart: UInt32(offset), codeLength: UInt32(r.code.count)))
                }
            }
        }
        try checkLimits()
        let materialized = materialize(executable + bindings, code)
        if moduleName.isEmpty { moduleName = anonymousName(materialized.0, materialized.1, maxRegs, maxDepth) }
        _ = text(moduleName)
        try checkLimits()
        let sourceMap = options.debugInfo.contains(.sourceMap) ? GameEventScriptSourceMap(sources: sourceDescriptors(), entries: spans) : nil
        return try GameEventScriptCompilerSupport.program(
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
            sourceArchive: options.debugInfo.contains(.sourceArchive) ? sources.map { .init(sourceID: $0.id, sourceName: $0.name, utf8Content: Array($0.text.utf8)) } : nil
        )
    }

    func sourceDescriptors() -> [GameEventScriptSourceMapSource] {
        sources.map { source in
            let bytes = Array(source.text.utf8)
            var starts: [UInt32] = [0]
            for i in bytes.indices { if bytes[i] == 10 || bytes[i] == 13 && (i + 1 == bytes.count || bytes[i + 1] != 10) { starts.append(UInt32(i + 1)) } }
            return .init(sourceID: source.id, sourceName: source.name, sourceByteLength: UInt32(bytes.count), sha256: GameEventScriptCompilerSupport.sha256(bytes), lineStartByteOffsets: starts)
        }
    }

    func anonymousName(_ bindings: [GameEventScriptBinding], _ code: [Instruction], _ registers: Int, _ depth: Int) -> String {
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

    func sourceSpan(_ location: GameEventScriptSourceLocation, address: Int) -> GameEventScriptSourceMapEntry? {
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

    func scalarConstant(_ expression: GesExpression) -> GesValue? {
        switch expression.kind {
        case .literal(let value): return value
        case .constant(let name): return constants[name].flatMap(scalarConstant)
        case .cast(let source, "number"), .cast(let source, "numeric"):
            guard let value = scalarConstant(source) else { return nil }
            let result = GameEventScriptCompilerSupport.castNumber(value)
            return result.kind == .float && !result.asNumber.isFinite ? nil : result
        case .binary(let op, let left, let right):
            let operations: [String: Op] = ["+": .add, "-": .subtract, "*": .multiply, "/": .divide, "div": .integerDivide, "mod": .modulo, "rem": .remainder, "^": .power]
            guard let opcode = operations[op], let a = scalarConstant(left), let b = scalarConstant(right), let result = GameEventScriptCompilerSupport.arithmetic(opcode, a, b), result.kind != .float || result.asNumber.isFinite else { return nil }
            return result
        case .unary("-", let value):
            guard let value = scalarConstant(value), value.isNumeric else { return nil }
            if let integer = value.integerValue, integer != .min { return .integer(-integer, unit: value.unit) }
            if value.kind == .percentage { return .percentage(-value.asNumber) }
            return .float(-value.asNumber, unit: value.unit)
        default: return nil
        }
    }

    func stageExpressions(_ values: [GesExpression], _ r: GesRoutine, _ scope: GesScope) throws {
        let plans = try values.map { value -> (GesValue?, Int?) in
            if let constant = scalarConstant(value) { return (constant, nil) }
            return (nil, try expression(value, r, scope))
        }
        r.maxStage = max(r.maxStage, plans.count)
        for (constant, register) in plans {
            if let register {
                r.emit(.stageRegister, 0, register)
                continue
            }
            guard let value = constant else { continue }
            let flag: UInt8 = value.unit == .degree ? 1 : value.unit == .meter ? 2 : value.unit == .second ? 3 : 0
            switch value.kind {
            case .nothing: r.emit(.stageNothing)
            case .boolean: r.emit(value.asBoolean ? .stageTrue : .stageFalse)
            case .integer: r.emit(.stageInteger, payload: UInt64(bitPattern: value.integerValue!), flags: flag)
            case .float: r.emit(.stageFloat, payload: value.asNumber.bitPattern, flags: flag)
            case .percentage: r.emit(.stagePercentage, payload: value.asNumber.bitPattern)
            case .text: r.emit(.stageText, 0, text(value.textValue!))
            case .tag: r.emit(.stageTag, 0, text(value.textValue!))
            default: throw error("compile.unsupportedConstruct", r.location, phase: .compile)
            }
        }
    }

    func stage(_ registers: [Int], _ r: GesRoutine) {
        r.maxStage = max(r.maxStage, registers.count)
        for register in registers { r.emit(.stageRegister, 0, register) }
    }

    func cast(_ d: Int, _ value: Int, _ type: String, _ r: GesRoutine, check: Bool = false) throws {
        if ["number", "numeric"].contains(type) {
            r.emit(check ? .checkNumeric : .castNumeric, d, value)
            return
        }
        if type == "integer" {
            r.emit(.checkInteger, d, value)
            return
        }
        if type == "fractional" {
            r.emit(.checkFractional, d, value)
            return
        }
        if type.hasPrefix("quantity:") || ["meter", "second", "degree"].contains(type) {
            let unit = type.split(separator: ":").last!
            guard ["m", "meter", "s", "second", "degree", "°", "none"].contains(String(unit)) else { throw error("compile.unsupportedConstruct", r.location, symbol: type, phase: .compile) }
            let flag: UInt8 = unit == "none" ? 0 : ["m", "meter"].contains(String(unit)) ? 2 : ["s", "second"].contains(String(unit)) ? 3 : 1
            r.emit(check ? .checkUnit : .castUnit, d, value, flags: flag)
            return
        }
        if let kind = GameEventScriptBytecodeTypeKind.allCases.first(where: { String(describing: $0) == type }) {
            r.emit(check ? .checkType : .cast, d, value, Int(kind.rawValue))
            return
        }
        r.emit(check ? .checkCustomType : .castCustom, d, value, text(type))
        if !check, let record = records[type] { r.dependencies.insert(record.name + "(" + record.fields.compactMap(\.label).joined(separator: ",") + ")") }
    }
}
