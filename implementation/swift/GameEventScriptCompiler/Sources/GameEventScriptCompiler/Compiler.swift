// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

final class GesRoutine {
    let definition: GesDefinition
    var key: String { definition.kind.hasSuffix("andler") ? definition.signature + "@\(definition.location.sourceID ?? 0):\(definition.location.line ?? 0):\(definition.location.column ?? 0)" : definition.signature }
    var code: [GesBytecodeInstruction] = []
    var locations: [GesSourceLocation] = []
    var pinned: Set<Int> = []
    // Label positions as in the C# builder: a label stays in place when the jumps to it disappear.
    var labels: Set<Int> = []
    var registers: Int
    var maxStage = 0
    var hasAssembly = false
    var componentRows: [Int: GesComponentRow] = [:]
    var nonListBindings: Set<Int> = []
    var calls: [(Int, String)] = []
    var dependencies: Set<String> = []
    var symbols: [(String, Int, Int, Bool)] = []
    var location: GesSourceLocation

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

    @discardableResult func emit(_ op: GesBytecodeOpCode, _ d: Int = 0, _ x: Int = 0, _ y: Int = 0, payload: UInt64 = 0, flags: UInt8 = 0) -> Int {
        let i = code.count
        code.append(.init(opcode: op, unitAndFlags: flags, word0: UInt16(truncatingIfNeeded: d), word1: UInt16(truncatingIfNeeded: x), word2: UInt16(truncatingIfNeeded: y), payload: payload))
        locations.append(location)
        return i
    }

    // Marks a label at the next instruction, as C# MarkLabel does, and resolves the given jumps to it.
    // The label exists even when no jump refers to it, so it still bounds basic blocks for the optimizer.
    func mark(_ jumps: [Int] = []) {
        labels.insert(code.count)
        for jump in jumps { patch(jump, target: code.count) }
    }

    func patch(_ i: Int, target: Int) {
        labels.insert(target)
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
    var modules: [GesModule]
    let sources: [GesSource], options: GesCompileOptions
    let catalog: (any GesExternalTypeCatalog)?
    var moduleName: String
    let constants: [String: GesExpression]
    var definitions: [String: GesDefinition], records: [String: GesRecord]
    let builder: GesBinaryBuilder

    // Lowers the modules and declarations of a completed validation.
    init(_ validated: GesAstValidator, sources: [GesSource], options: GesCompileOptions) {
        modules = validated.modules
        catalog = validated.catalog
        moduleName = validated.moduleName
        constants = validated.constants
        definitions = validated.definitions
        records = validated.records
        self.sources = sources
        self.options = options
        builder = GesBinaryBuilder(sources: sources, options: options, fallbackLocation: validated.modules.first?.location ?? .init(sourceName: "UnknownSource"))
    }

    func compile() throws -> GameEventScriptProgram {
        let optimizer = GesAstOptimizer(constants: constants)
        // Rewrites in place: unchanged entries keep their storage.
        for index in modules.indices { if let module = optimizer.module(modules[index]) { modules[index] = module } }
        for index in definitions.values.indices { if let definition = optimizer.definition(definitions.values[index]) { definitions.values[index] = definition } }
        for index in records.values.indices { if let record = optimizer.record(records.values[index]) { records.values[index] = record } }
        var routines: [GesRoutine] = []
        let callables = definitions.values.sorted { $0.signature < $1.signature }
        for definition in callables { routines.append(try routine(definition)) }
        for (id, record) in records.values.sorted(by: { $0.name < $1.name }).enumerated() { builder.recordIDs[record.name] = id }
        for record in records.values.sorted(by: { $0.name < $1.name }) { routines.append(try recordRoutine(record)) }
        let handlers = modules.flatMap(\.definitions).enumerated().filter { $0.element.kind.hasSuffix("andler") }.sorted { $0.element.name == $1.element.name ? $0.offset < $1.offset : $0.element.name < $1.element.name }
        for entry in handlers { routines.append(try routine(entry.element)) }
        let order = ["handler": 0, "messageNameHandler": 0, "record": 1, "predicate": 2, "function": 3]
        routines = routines.enumerated().sorted { a, b in
            let ak = order[a.element.definition.kind] ?? 4
            let bk = order[b.element.definition.kind] ?? 4
            return ak == bk ? a.offset < b.offset : ak < bk
        }.map(\.element)
        builder.routines = routines
        return try builder.build(moduleName: moduleName)
    }

    func routine(_ definition: GesDefinition) throws -> GesRoutine {
        let r = GesRoutine(definition)
        let scope = GesScope()
        r.emit(.registerLocals)
        for (i, parameter) in definition.parameters.enumerated() {
            scope.values[parameter.name] = i
            r.symbols.append((definition.argumentName(parameter), i, 0, true))
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
                let value: Int
                if case .assembly(let block) = computed.kind {
                    value = scope.get(field.name)!
                    try assembly(block, r, scope, destination: value)
                    // The cast after the block maps to the field again, as the C# field range scope resumes.
                    r.location = field.location
                } else {
                    value = try expression(computed, r, scope)
                }
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
        r.emit(.createMap, map, builder.textList(record.fields.map(\.name)))
        r.emit(.createRecordValue, result, map, builder.text(record.name))
        r.emit(.returnValue, 0, result)
        return r
    }

    // The value of a literal. GesAstOptimizer has already folded every constant expression the C# compiler folds,
    // so lowering never evaluates other expression forms.
    func scalarConstant(_ expression: GesExpression) -> GesValue? {
        if case .literal(let value) = expression.kind { return value }
        return nil
    }

    func stageExpressions(_ values: [GesExpression], _ r: GesRoutine, _ scope: GesScope) throws {
        let plans = try values.map { value -> (GesValue?, Int?, GesSourceLocation) in
            if let constant = scalarConstant(value) { return (constant, nil, value.location) }
            return (nil, try expression(value, r, scope), value.location)
        }
        r.maxStage = max(r.maxStage, plans.count)
        for (constant, register, location) in plans {
            if let register {
                r.emit(.stageRegister, 0, register)
                continue
            }
            guard let value = constant else { continue }
            // A staged constant maps to its own literal, as C# EmitStageConstant scopes its source range.
            let outer = r.location
            r.location = location
            defer { r.location = outer }
            let flag: UInt8 = value.unit == .degree ? 1 : value.unit == .meter ? 2 : value.unit == .second ? 3 : 0
            switch value.valueKind {
            case .nothing: r.emit(.stageNothing)
            case .boolean: r.emit(value.asBoolean ? .stageTrue : .stageFalse)
            case .integer: r.emit(.stageInteger, payload: UInt64(bitPattern: value.integerValue!), flags: flag)
            case .float: r.emit(.stageFloat, payload: value.asNumber.bitPattern, flags: flag)
            case .percentage: r.emit(.stagePercentage, payload: value.asNumber.bitPattern)
            case .text: r.emit(.stageText, 0, builder.text(value.textValue!))
            case .tag: r.emit(.stageTag, 0, builder.text(value.textValue!))
            default: throw compileError("compile.unsupportedConstruct", r.location, phase: .compile)
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
            guard ["m", "meter", "s", "second", "degree", "°", "none"].contains(String(unit)) else { throw compileError("compile.unsupportedConstruct", r.location, symbol: type, phase: .compile) }
            let flag: UInt8 = unit == "none" ? 0 : ["m", "meter"].contains(String(unit)) ? 2 : ["s", "second"].contains(String(unit)) ? 3 : 1
            r.emit(check ? .checkUnit : .castUnit, d, value, flags: flag)
            return
        }
        if let kind = GesBytecodeTypeKind.allCases.first(where: { String(describing: $0) == type }) {
            r.emit(check ? .checkType : .cast, d, value, Int(kind.rawValue))
            return
        }
        r.emit(check ? .checkCustomType : .castCustom, d, value, builder.text(type))
        if !check, let record = records[type] { r.dependencies.insert(record.name + "(" + record.fields.compactMap(\.label).joined(separator: ",") + ")") }
    }
}
