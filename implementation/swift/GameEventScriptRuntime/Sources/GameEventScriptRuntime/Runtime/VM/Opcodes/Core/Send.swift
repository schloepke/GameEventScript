// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptVirtualMachine {
    static func send(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        var micros: Int64 = 0
        if i.opcode == .emitAfter || i.opcode == .publishAfter {
            let delay = s.value(Int(i.b))
            guard delay.unit == .second && (delay.kind == .integer || delay.kind == .float) else {
                s.setBoolean(Int(i.word0), false)
                return
            }
            if let value = delay.integerValue {
                guard value >= 0 && value <= Int64.max / 1_000_000 else {
                    s.setBoolean(Int(i.word0), false)
                    return
                }
                micros = value * 1_000_000
            } else {
                let rounded = (delay.asNumber * 1_000_000).rounded(.up)
                guard delay.asNumber >= 0 && rounded.isFinite && rounded >= 0 && rounded < 9223372036854775808.0 else {
                    s.setBoolean(Int(i.word0), false)
                    return
                }
                micros = Int64(rounded)
            }
        }
        let message: GameEventScriptMessage?
        if i.unitAndFlags & 0x80 != 0 { message = s.value(Int(i.word1)).messageValue } else { message = s.linked!.outbound[i.word1]?.createMessage(s.values(i.word2)) }
        guard var message else {
            s.setBoolean(Int(i.word0), false)
            return
        }
        if i.unitAndFlags & 0x40 != 0 {
            guard let taggedMessage = try? tagged(message, s.values(i.a)) else {
                s.setBoolean(Int(i.word0), false)
                return
            }
            message = taggedMessage
        }
        let accepted = c.send(message, publish: i.opcode == .publishInstant || i.opcode == .publishAfter, microseconds: micros)
        s.setBoolean(Int(i.word0), accepted)
    }
}
