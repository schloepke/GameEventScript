// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// One element of a routine's plan, as C# GesBinaryBuilder.PlanItem: a label, or an instruction with the index of its
// source location. Jumps address labels by ID and calls address their callee by call ID, so passes move and
// remove items without remapping any position; addresses exist only once the plan is encoded.
extension GesBinaryBuilder {
    enum PlanItem: Equatable {
        case label(Int)
        case instruction(GesBytecodeInstruction, location: Int)

        var instruction: GesBytecodeInstruction? {
            if case .instruction(let instruction, _) = self { return instruction }
            return nil
        }

        var label: Int? {
            if case .label(let label) = self { return label }
            return nil
        }
    }
}

final class GesRoutine {
    let definition: GesDefinition
    var key: String { definition.kind.hasSuffix("andler") ? definition.signature + "@\(definition.location.sourceID ?? 0):\(definition.location.line ?? 0):\(definition.location.column ?? 0)" : definition.signature }
    // Starts with the routine entry label, as in C#.
    var items: [GesBinaryBuilder.PlanItem] = [.label(GesRoutine.entryLabel)]
    var labelCount = 1
    // Source locations by index; an instruction item keeps the index of its own.
    var locations: [GesSourceLocation] = []
    // Callee keys by call ID; a call instruction holds its call ID until encoding.
    var calls: [String] = []
    var pinned: Set<Int> = []
    var registers: Int
    var maxStage = 0
    var hasAssembly = false
    var componentRows: [Int: GesComponentRow] = [:]
    var nonListBindings: Set<Int> = []
    var dependencies: Set<String> = []
    // Debug symbols: name, register and whether the register is a parameter.
    var symbols: [(String, Int, Bool)] = []
    var location: GesSourceLocation

    static let entryLabel = 0

    init(_ definition: GesDefinition) {
        self.definition = definition
        registers = definition.parameters.count
        location = definition.location
        pinned = Set(0..<registers)
    }

    var instructionCount: Int {
        var count = 0
        for item in items where item.label == nil { count += 1 }
        return count
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

    // Appends an instruction and returns its item index, which stays valid while lowering appends items.
    @discardableResult func emit(_ op: GesBytecodeOpCode, _ d: Int = 0, _ x: Int = 0, _ y: Int = 0, payload: UInt64 = 0, flags: UInt8 = 0) -> Int {
        let instruction = GesBytecodeInstruction(
            opcode: op,
            unitAndFlags: flags,
            word0: UInt16(truncatingIfNeeded: d),
            word1: UInt16(truncatingIfNeeded: x),
            word2: UInt16(truncatingIfNeeded: y),
            payload: payload
        )
        items.append(.instruction(instruction, location: locations.count))
        locations.append(location)
        return items.count - 1
    }

    // A label that `place` puts into the plan later, for targets that are referenced before they are reached.
    func newLabel() -> Int {
        defer { labelCount += 1 }
        return labelCount
    }

    func place(_ label: Int) { items.append(.label(label)) }

    // Places a new label at the end of the plan, as C# MarkLabel does, and points the given jumps at it. The label
    // stays even when no jump refers to it, so it still bounds basic blocks for the optimizer.
    @discardableResult func mark(_ jumps: [Int] = []) -> Int {
        let label = newLabel()
        place(label)
        for jump in jumps { patch(jump, label: label) }
        return label
    }

    // Points the jump at item index `item` to `label`.
    func patch(_ item: Int, label: Int) {
        guard case .instruction(let old, let location) = items[item] else { preconditionFailure("Only instructions jump.") }
        items[item] = .instruction(
            .init(opcode: old.opcode, unitAndFlags: old.unitAndFlags, word0: old.word0, word1: old.word1, word2: UInt16(truncatingIfNeeded: label), payload: old.payload),
            location: location
        )
    }

    // The call ID of a call to the routine `key`.
    func call(_ key: String) -> Int {
        calls.append(key)
        return calls.count - 1
    }

    // Instruction ordinal of the first instruction at or after each label, by label ID; -1 for an unplaced label.
    func labelAddresses() -> [Int] {
        var addresses = [Int](repeating: -1, count: labelCount)
        var ordinal = 0
        for item in items {
            if let label = item.label { addresses[label] = ordinal } else { ordinal += 1 }
        }
        return addresses
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
            r.symbols.append((definition.argumentName(parameter), i, true))
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
            r.symbols.append((p.label, i, true))
        }
        for field in record.fields where field.computed != nil {
            let reg = r.local()
            scope.values[field.name] = reg
            r.symbols.append((field.name, reg, false))
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
        try stagePlans(plans, r)
    }

    // Stages evaluated registers and constants in order; apart from the values above, this runs in its own frame.
    func stagePlans(_ plans: [(GesValue?, Int?, GesSourceLocation)], _ r: GesRoutine) throws {
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
            let flag = GesCompilerSupport.unitID(value.unit)
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
            let name = type.hasPrefix("quantity:") ? String(type.dropFirst(9)) : type
            guard let unit = GesCompilerSupport.quantityUnit(name) else { throw compileError("compile.unsupportedConstruct", r.location, symbol: type, phase: .compile) }
            r.emit(check ? .checkUnit : .castUnit, d, value, flags: GesCompilerSupport.unitID(unit))
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
