// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func performLessGreaterLessOrEqualGreaterOrEqual<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        if a.isNothing || b.isNothing { return sink.nothing }
        if !a.isNumeric || !b.isNumeric || a.unit != b.unit { return sink.boolean(false) }
        if let av = a.integerValue, let bv = b.integerValue {
            switch i.opcode {
            case .less: return sink.boolean(av < bv)
            case .greater: return sink.boolean(av > bv)
            case .lessOrEqual: return sink.boolean(av <= bv)
            default: return sink.boolean(av >= bv)
            }
        }
        switch i.opcode {
        case .less: return sink.boolean(a.asNumber < b.asNumber)
        case .greater: return sink.boolean(a.asNumber > b.asNumber)
        case .lessOrEqual: return sink.boolean(a.asNumber <= b.asNumber)
        default: return sink.boolean(a.asNumber >= b.asNumber)
        }
    }
}
