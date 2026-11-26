// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func send(_ i: GesBytecodeInstruction, _ c: GesContext) throws {
        var micros: Int64 = 0
        let delayed = i.opcode == .emitAfter || i.opcode == .publishAfter
        if delayed {
            guard let delay = readDelay(i.b) else {
                setBoolean(Int(i.word0), false)
                return
            }
            micros = delay
        }
        setBoolean(Int(i.word0), sendResolved(i, publish: i.opcode == .publishInstant || i.opcode == .publishAfter, c, delay: delayed ? micros : nil, name: nil, repeats: false))
    }

    func sendNamed(_ i: GesBytecodeInstruction, _ c: GesContext) {
        guard let micros = readDelay(i.b), let name = readTimerName(i.c) else {
            setBoolean(Int(i.word0), false)
            return
        }
        let repeats = i.unitAndFlags & 0x20 != 0
        if repeats && micros == 0 {
            setBoolean(Int(i.word0), false)
            return
        }
        setBoolean(Int(i.word0), sendResolved(i, publish: i.opcode == .publishNamed, c, delay: micros, name: name, repeats: repeats))
    }

    func executeCancelTimers(_ i: GesBytecodeInstruction, _ c: GesContext) {
        var name: String?
        if i.a == 0 {
            name = readTimerName(i.word1)
            if name == nil {
                setBoolean(Int(i.word0), false)
                return
            }
        }
        setBoolean(Int(i.word0), c.cancelTimers(mode: Int(i.a), name: name))
    }

    private func readTimerName(_ register: UInt16) -> String? { registers[frameStart + Int(register)].textValue }

    // A delay is a seconds Quantity rounded up to whole microseconds; negative or unrepresentable delays fail.
    private func readDelay(_ register: UInt16) -> Int64? {
        let index = frameStart + Int(register)
        guard registers[index].unit == .second && (registers[index].kind == .integer || registers[index].kind == .float) else { return nil }
        if registers[index].kind == .integer {
            let value = registers[index].integerValue!
            guard value >= 0 && value <= Int64.max / 1_000_000 else { return nil }
            return value * 1_000_000
        }
        let number = registers[index].asNumber
        let rounded = (number * 1_000_000).rounded(.up)
        guard number >= 0 && rounded.isFinite && rounded >= 0 && rounded < 9223372036854775808.0 else { return nil }
        return Int64(rounded)
    }

    private func sendResolved(_ i: GesBytecodeInstruction, publish: Bool, _ c: GesContext, delay: Int64?, name: String?, repeats: Bool) -> Bool {
        let message: GameEventScriptMessage?
        if i.unitAndFlags & 0x80 != 0 { message = value(Int(i.word1)).messageValue } else { message = linked!.outbound[i.word1]?.makeMessage(values(i.word2)) }
        guard var message else { return false }
        if i.unitAndFlags & 0x40 != 0 {
            guard let taggedMessage = try? Self.tagged(message, values(i.a)) else { return false }
            message = taggedMessage
        }
        if let name { return c.sendNamed(message, publish: publish, microseconds: delay ?? 0, name: name, repeats: repeats) }
        return c.send(message, publish: publish, microseconds: delay ?? 0)
    }
}
