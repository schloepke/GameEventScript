// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func send(_ i: GameEventScriptBytecodeInstruction, _ c: GameEventScriptContext) throws {
        var micros: Int64 = 0
        if i.opcode == .emitAfter || i.opcode == .publishAfter {
            let delay = value(Int(i.b))
            guard delay.unit == .second && (delay.kind == .integer || delay.kind == .float) else {
                setBoolean(Int(i.word0), false)
                return
            }
            if let value = delay.integerValue {
                guard value >= 0 && value <= Int64.max / 1_000_000 else {
                    setBoolean(Int(i.word0), false)
                    return
                }
                micros = value * 1_000_000
            } else {
                let rounded = (delay.asNumber * 1_000_000).rounded(.up)
                guard delay.asNumber >= 0 && rounded.isFinite && rounded >= 0 && rounded < 9223372036854775808.0 else {
                    setBoolean(Int(i.word0), false)
                    return
                }
                micros = Int64(rounded)
            }
        }
        let message: GameEventScriptMessage?
        if i.unitAndFlags & 0x80 != 0 { message = value(Int(i.word1)).messageValue } else { message = linked!.outbound[i.word1]?.createMessage(values(i.word2)) }
        guard var message else {
            setBoolean(Int(i.word0), false)
            return
        }
        if i.unitAndFlags & 0x40 != 0 {
            guard let taggedMessage = try? Self.tagged(message, values(i.a)) else {
                setBoolean(Int(i.word0), false)
                return
            }
            message = taggedMessage
        }
        let accepted = c.send(message, publish: i.opcode == .publishInstant || i.opcode == .publishAfter, microseconds: micros)
        setBoolean(Int(i.word0), accepted)
    }
}
