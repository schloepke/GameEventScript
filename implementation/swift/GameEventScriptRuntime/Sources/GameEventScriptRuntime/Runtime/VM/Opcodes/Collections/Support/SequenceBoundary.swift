// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performStartsWithEndsWith<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        let b = s.value(Int(i.word2))
        if a.isNothing { return sink.nothing }
        if let at = a.textValue, let bt = b.textValue { return sink.boolean(op == .startsWith ? at.unicodeScalars.starts(with: bt.unicodeScalars) : at.unicodeScalars.reversed().starts(with: bt.unicodeScalars.reversed())) }
        guard sequence(a), sequence(b) else { return sink.boolean(false) }
        let ac = length(a)
        let bc = length(b)
        if bc > ac { return sink.boolean(false) }
        let start = op == .startsWith ? 0 : ac - bc
        for index in 0..<bc { if a.index(start + index + 1) != b.index(index + 1) { return sink.boolean(false) } }
        return sink.boolean(true)
    }
}
