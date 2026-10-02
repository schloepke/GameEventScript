// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Reusable, host-private register machine state. All register writes pass through this owner.
final class GesVmState {
    struct Frame {
        var ip = 0, start = 0, length = 0, destination = 0
        var predicate = false
        var literal: GesLiteralEvaluation?
    }

    var linked: GesLinkedProgram?
    var program: GameEventScriptProgram { linked!.program }
    var processing = false
    var error: GameEventScriptDiagnostic?
    var handlerName: String?
    var ip = 0, frameStart = 0, frameLength = 0, stageLength = 0
    private var registers: [GesValue]
    private var frames: [Frame]
    private var depth = 0
    private let maxRegisters: Int
    private var pendingCapacity = 0
    let extensionCall = GesExtensionCall()
    lazy var constructorCall = GesExternalTypeConstructorCall()

    init(maxRegisters: Int, maxCallDepth: Int) {
        self.maxRegisters = maxRegisters
        registers = [GesValue](repeating: .nothing, count: min(32, maxRegisters))
        frames = [Frame](repeating: .init(), count: maxCallDepth)
    }

    func prepareCapacity(_ capacity: Int) {
        if processing {
            pendingCapacity = max(pendingCapacity, capacity)
            return
        }
        ensure(capacity)
    }

    @discardableResult func ensure(_ count: Int) -> Bool {
        if count <= registers.count { return true }
        if count > maxRegisters {
            fail("runtime.registerOverflow")
            return false
        }
        let capacity = min(maxRegisters, max(count, max(1, registers.count) * 2))
        registers.append(contentsOf: repeatElement(.nothing, count: capacity - registers.count))
        return true
    }

    func value(_ index: Int) -> GesValue { registers[frameStart + index].registerValue }

    func slot(_ index: Int) -> GesValue { registers[frameStart + index] }

    // Copy existing values (arguments, callbacks, collection members); computed results use typed setters.
    func setValue(_ index: Int, _ value: GesValue) { registers[frameStart + index] = value }

    func output(_ index: Int) -> GesRegisterOutput { .init(state: self, index: index) }

    func loadText(_ index: Int, constant: UInt16, tag: Bool = false) { registers[frameStart + index].setTextConstant(linked!.textValues[Int(constant)], tag: tag) }

    func stageTextConstant(_ constant: UInt16, tag: Bool = false) { if let index = reserveStage() { loadText(index, constant: constant, tag: tag) } }

    func setText(_ index: Int, _ value: String) { registers[frameStart + index].setText(value) }

    func setTag(_ index: Int, _ value: String) throws { try registers[frameStart + index].setTag(value) }

    func setVector(_ index: Int, x: Double, y: Double, z: Double, unit: GesUnit) { registers[frameStart + index].setVector(x: x, y: y, z: z, unit: unit) }

    func setPoint(_ index: Int, x: Double, y: Double, z: Double, unit: GesUnit) { registers[frameStart + index].setPoint(x: x, y: y, z: z, unit: unit) }

    func setDice(_ index: Int, _ value: [Int32]) { registers[frameStart + index].setDice(value) }

    func setList(_ index: Int, _ value: [GesValue]) { registers[frameStart + index].setList(value) }

    func setMap(_ index: Int, _ value: [GesMapEntry]) { registers[frameStart + index].setMap(value) }

    func setRecord(_ index: Int, typeName: String, entries: [GesMapEntry]) { registers[frameStart + index].setRecord(typeName: typeName, entries: entries) }

    func setRecord(_ index: Int, typeName: String, fields: GesValueMap) { registers[frameStart + index].setRecord(typeName: typeName, fields: fields) }

    func setIntegerRange(_ index: Int, from: Int64, to: Int64, step: Int64) { registers[frameStart + index].setIntegerRange(from: from, to: to, step: step) }

    func setFloatRange(_ index: Int, from: Double, to: Double, step: Double) { registers[frameStart + index].setFloatRange(from: from, to: to, step: step) }

    func setMessage(_ index: Int, _ value: GameEventScriptMessage) { registers[frameStart + index].setMessage(value) }

    func setHandler(_ index: Int, _ value: GameEventScriptMessageSignature) { registers[frameStart + index].setHandler(value) }

    func setSeries(_ index: Int, _ value: GesSeriesValue) { registers[frameStart + index].setSeries(value) }

    func setIterator(_ index: Int, _ iterator: GesIterator) { registers[frameStart + index].setIterator(iterator) }

    func createBuilder(_ index: Int, _ kind: GesCollectionBuilder.Kind) { registers[frameStart + index].setBuilder(GesCollectionBuilder(kind)) }

    // Typed owner-local access keeps full GesValue operands/results out of the dispatch boundary.
    @inline(__always)
    func setInteger(_ index: Int, _ value: Int64, unit: GesUnit = .none) {
        registers[frameStart + index].setInteger(value, unit: unit)
    }

    @inline(__always)
    func setFloat(_ index: Int, _ value: Double, unit: GesUnit = .none) {
        registers[frameStart + index].setFloat(value, unit: unit)
    }

    @inline(__always)
    func setPercentage(_ index: Int, _ value: Double) {
        registers[frameStart + index].setPercentage(value)
    }

    @inline(__always)
    func setBoolean(_ index: Int, _ value: Bool) {
        registers[frameStart + index].setBoolean(value)
    }

    @inline(__always)
    func setNothing(_ index: Int) {
        registers[frameStart + index].setNothing()
    }

    @inline(__always)
    func integer(_ index: Int) -> Int64? { registers[frameStart + index].integerValue }

    @inline(__always)
    func unit(_ index: Int) -> GesUnit { registers[frameStart + index].unit }

    @inline(__always)
    func truth(_ index: Int) -> Bool? { registers[frameStart + index].truth }

    @inline(__always)
    func isNothing(_ index: Int) -> Bool { registers[frameStart + index].isNothing }

    @inline(__always)
    func move(_ destination: Int, _ source: Int) {
        registers[frameStart + destination] = registers[frameStart + source].registerValue
    }

    @inline(__always)
    func setArithmeticResult(_ instruction: GameEventScriptBytecodeInstruction, _ context: GameEventScriptContext) throws {
        if !setIntegerArithmeticResult(instruction) { try GesMath.execute(instruction, self, context, sink: output(Int(instruction.word0))) }
    }

    @inline(__always)
    func setIntegerArithmeticResult(_ instruction: GameEventScriptBytecodeInstruction) -> Bool {
        let start = frameStart
        // One exclusive storage borrow; no callback, frame change or growth occurs in this region.
        // Only scalar inputs survive the destination write, even when source and destination alias.
        return registers.withUnsafeMutableBufferPointer { slots in
            guard let a = slots[start + Int(instruction.word1)].integerOperand,
                let b = slots[start + Int(instruction.word2)].integerOperand
            else { return false }
            let destination = start + Int(instruction.word0)
            switch GesMath.integerArithmeticResult(instruction.opcode, a.value, b.value, a.unit, b.unit) {
            case .nothing: slots[destination].setNothing()
            case .integer(let value, let unit): slots[destination].setInteger(value, unit: unit)
            case .float(let value, let unit): slots[destination].setFloat(value, unit: unit)
            }
            return true
        }
    }

    func staged(_ index: Int) -> GesValue { registers[frameStart + frameLength + index].registerValue }

    func stageValue(_ value: GesValue) {
        let index = frameStart + frameLength + stageLength
        if ensure(index + 1) {
            registers[index] = value
            stageLength += 1
        }
    }

    func stageRegister(_ source: Int) {
        if let index = reserveStage() { registers[frameStart + index] = registers[frameStart + source].registerValue }
    }

    private func reserveStage() -> Int? {
        let index = frameLength + stageLength
        guard ensure(frameStart + index + 1) else { return nil }
        stageLength += 1
        return index
    }

    func stageNothing() { if let index = reserveStage() { setNothing(index) } }

    func stageBoolean(_ value: Bool) { if let index = reserveStage() { setBoolean(index, value) } }

    func stageInteger(_ value: Int64, unit: GesUnit = .none) { if let index = reserveStage() { setInteger(index, value, unit: unit) } }

    func stageFloat(_ value: Double, unit: GesUnit = .none) { if let index = reserveStage() { setFloat(index, value, unit: unit) } }

    func stagePercentage(_ value: Double) { if let index = reserveStage() { setPercentage(index, value) } }

    func stageText(_ value: String) { if let index = reserveStage() { setText(index, value) } }

    func stageTag(_ value: String) throws {
        guard GesText.isLowerName(value) else { throw GesValueError.invalidTag(value) }
        if let index = reserveStage() { try setTag(index, value) }
    }

    func clearStage() {
        clear(frameStart + frameLength, stageLength)
        stageLength = 0
    }

    func modifyLocals(_ delta: Int) {
        if delta >= 0 {
            if ensure(frameStart + frameLength + delta) { frameLength += delta }
        } else if frameLength + delta < 0 {
            fail("runtime.preparationFailed")
        } else {
            clear(frameStart + frameLength + delta, -delta)
            frameLength += delta
        }
    }

    func call(_ address: Int, destination: Int, predicate: Bool = false, literal: GesLiteralEvaluation? = nil) {
        if depth >= frames.count {
            fail("runtime.callStackOverflow")
            return
        }
        if !ensure(frameStart + frameLength + stageLength) { return }
        frames[depth] = .init(ip: ip, start: frameStart, length: frameLength, destination: destination, predicate: predicate, literal: literal)
        depth += 1
        ip = address
        frameStart += frameLength
        frameLength = stageLength
        stageLength = 0
    }

    func returnRegister(_ index: Int) throws { try returnValue(value(index)) }

    func returnValue(_ value: GesValue = .nothing) throws {
        clear(frameStart, frameLength + stageLength)
        stageLength = 0
        if depth == 0 {
            frameLength = 0
            processing = false
            return
        }
        depth -= 1
        let frame = frames[depth]
        frames[depth] = .init()
        ip = frame.ip
        frameStart = frame.start
        frameLength = frame.length
        setValue(frame.destination, frame.predicate && value.kind != .boolean ? .nothing : value)
        try frame.literal?.resume(value)
    }

    func reset() {
        for index in 0..<depth { frames[index] = .init() }
        for index in registers.indices { registers[index].setNothing() }
        linked = nil
        handlerName = nil
        error = nil
        processing = false
        ip = 0
        frameStart = 0
        frameLength = 0
        stageLength = 0
        depth = 0
        extensionCall.end()
        constructorCall.end()
        if pendingCapacity > 0 {
            ensure(pendingCapacity)
            pendingCapacity = 0
        }
    }

    private func clear(_ start: Int, _ count: Int) { for index in start..<(start + count) { registers[index].setNothing() } }

    func fail(_ code: String, symbol: String? = nil, error: (any Error)? = nil) { fail(.init(phase: .runtime, code: code, symbol: symbol, technicalDetails: error.map { String(describing: $0) })) }

    func fail(_ diagnostic: GameEventScriptDiagnostic) {
        var diagnostic = diagnostic
        if diagnostic.programName == nil { diagnostic.programName = linked?.program.moduleName }
        if diagnostic.handlerName == nil { diagnostic.handlerName = handlerName }
        error = diagnostic
        processing = false
    }

    func list(_ index: UInt16) -> [UInt16] { program.uint16IndexLists[Int(index)] }

    func text(_ index: UInt16) -> String { program.stringConstants[Int(index)] }

    func values(_ index: UInt16) -> [GesValue] { list(index).map { value(Int($0)) } }
}
