// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performHasAnyHasAll<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        let all = op == .hasAll
        let iterator: GesIterator?
        if let source = slot.iteratorValue { iterator = source } else { iterator = GesIterator(a) }
        if a.isNothing, slot.isRegisterData { return sink.nothing }
        guard let iterator else { return sink.boolean(false) }
        defer { iterator.close() }
        while let value = iterator.next() {
            let truth = a.spatialValue != nil ? value.asNumber.isFinite && value.asNumber != 0 : value.asBoolean
            if truth != all { return sink.boolean(!all) }
        }
        return sink.boolean(all)
    }
}
