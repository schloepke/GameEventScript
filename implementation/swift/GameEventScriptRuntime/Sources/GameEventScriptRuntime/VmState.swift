// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Reusable, host-private register machine state. All register writes pass through this owner.
final class GesVmState {
    struct Frame {
        var ip = 0, start = 0, length = 0, destination = 0
        var predicate = false
        var literal: GesLiteralEvaluation?
    }

    enum Slot {
        case value(GesValue)
        case iterator(GesIterator)
        case builder(GesCollectionBuilder)
        var value: GesValue { if case .value(let value) = self { value } else { .nothing } }
    }

    var linked: GesLinkedProgram?
    var program: GameEventScriptProgram { linked!.program }
    var processing = false
    var error: GameEventScriptDiagnostic?
    var handlerName: String?
    var ip = 0, frameStart = 0, frameLength = 0, stageLength = 0
    private var registers: [Slot]
    private var frames: [Frame]
    private var depth = 0
    private let maxRegisters: Int
    private var pendingCapacity = 0
    let extensionCall = GesExtensionCall()
    lazy var constructorCall = GesExternalTypeConstructorCall()

    init(maxRegisters: Int, maxCallDepth: Int) {
        self.maxRegisters = maxRegisters
        registers = [Slot](repeating: .value(.nothing), count: min(32, maxRegisters))
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
        registers.append(contentsOf: repeatElement(.value(.nothing), count: capacity - registers.count))
        return true
    }

    func value(_ index: Int) -> GesValue { registers[frameStart + index].value }

    func slot(_ index: Int) -> Slot { registers[frameStart + index] }

    func set(_ index: Int, _ value: GesValue) { registers[frameStart + index] = .value(value) }

    func setSlot(_ index: Int, _ value: Slot) { registers[frameStart + index] = value }

    func staged(_ index: Int) -> GesValue { registers[frameStart + frameLength + index].value }

    func stage(_ value: GesValue) {
        let index = frameStart + frameLength + stageLength
        if ensure(index + 1) {
            registers[index] = .value(value)
            stageLength += 1
        }
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
        set(frame.destination, frame.predicate && value.kind != .boolean ? .nothing : value)
        try frame.literal?.resume(value)
    }

    func reset() {
        for index in 0..<depth { frames[index] = .init() }
        for index in registers.indices { registers[index] = .value(.nothing) }
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

    private func clear(_ start: Int, _ count: Int) { for index in start..<(start + count) { registers[index] = .value(.nothing) } }

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
