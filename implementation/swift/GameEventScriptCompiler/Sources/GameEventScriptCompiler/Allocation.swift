// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GameEventScriptBytecodeInstruction {
    var fields: [UInt16] { [word0, word1, word2, a, b, c, d] }
    func replacing(_ fields: [UInt16]) -> Self {
        .init(
            opcode: opcode, unitAndFlags: unitAndFlags, word0: fields[0], word1: fields[1], word2: fields[2],
            payload: UInt64(fields[3]) | UInt64(fields[4]) << 16 | UInt64(fields[5]) << 32 | UInt64(fields[6]) << 48)
    }
}
extension GesCompiler {
    func allocate(_ routine: GesRoutine) throws {
        var intervals: [Int: (Int, Int)] = [:]
        for (index, instruction) in routine.code.enumerated() {
            let fields = instruction.fields
            var registers = GameEventScriptCompilerSupport.registerSlots(instruction).map { Int(fields[$0]) }
            for slot in GameEventScriptCompilerSupport.listSlots(instruction, text: false) {
                registers += lists[Int(fields[slot])].map(Int.init)
            }
            for register in registers { intervals[register] = (intervals[register]?.0 ?? index, index) }
        }
        // Keep values entering a loop alive through its back edge: a later iteration
        // reads them again even if their last textual use precedes other temporaries.
        for (index, instruction) in routine.code.enumerated() {
            switch instruction.opcode {
            case .jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing:
                let start = Int(instruction.word2)
                if start <= index {
                    for register in 0..<routine.registers {
                        if let interval = intervals[register], interval.0 < start,
                            interval.1 >= start, interval.1 < index
                        {
                            intervals[register] = (interval.0, index)
                        }
                    }
                }
            default: break
            }
        }
        var mapping: [Int: Int] = [:]
        var next = 0
        for register in routine.pinned.sorted() {
            mapping[register] = next
            next += 1
        }
        let temporaries = intervals.filter { !routine.pinned.contains($0.key) }.sorted { a, b in
            a.value.0 == b.value.0 ? a.key < b.key : a.value.0 < b.value.0
        }
        var active: [(end: Int, register: Int)] = []
        var free: [Int] = []
        for (id, interval) in temporaries {
            for index in active.indices.reversed() where active[index].end < interval.0 {
                free.append(active[index].register)
                active.remove(at: index)
            }
            let register: Int
            if let reused = free.popLast() {
                register = reused
            } else {
                register = next
                next += 1
            }
            mapping[id] = register
            active.append((interval.1, register))
        }
        for index in routine.code.indices {
            let instruction = routine.code[index]
            var fields = instruction.fields
            for slot in GameEventScriptCompilerSupport.registerSlots(instruction) {
                fields[slot] = UInt16(mapping[Int(fields[slot])]!)
            }
            for slot in GameEventScriptCompilerSupport.listSlots(instruction, text: false) {
                fields[slot] = UInt16(list(lists[Int(fields[slot])].map { mapping[Int($0)]! }))
            }
            routine.code[index] = instruction.replacing(fields)
        }
        routine.symbols = routine.symbols.map { ($0.0, mapping[$0.1]!, $0.2, $0.3) }
        routine.registers = next
    }
}
