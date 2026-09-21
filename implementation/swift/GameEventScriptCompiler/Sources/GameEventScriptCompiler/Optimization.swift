// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    func optimize(_ routine: GesRoutine) {
        let branches: Set<Op> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorNext, .iteratorCreateOrJump]
        for _ in 0..<2 {
            var reads: [Int: Int] = [:]
            var writes: [Int: [Int]] = [:]
            for (index, instruction) in routine.code.enumerated() {
                let fields = instruction.fields
                for slot in GameEventScriptCompilerSupport.registerSlots(instruction) {
                    let register = Int(fields[slot])
                    if slot == 0 { writes[register, default: []].append(index) } else { reads[register, default: 0] += 1 }
                }
                for slot in GameEventScriptCompilerSupport.listSlots(instruction, text: false) { for register in lists[Int(fields[slot])] { reads[Int(register), default: 0] += 1 } }
            }
            let targets = Set(routine.code.filter { branches.contains($0.opcode) }.map { Int($0.word2) })
            var removed: Set<Int> = []
            for (index, move) in routine.code.enumerated() where move.opcode == .move {
                let source = Int(move.word1)
                let destination = move.word0
                guard !routine.pinned.contains(source), reads[source] == 1, writes[source]?.count == 1, let writer = writes[source]?.first, writer < index, !removed.contains(writer) else { continue }
                let instruction = routine.code[writer]
                let fields = instruction.fields
                if GameEventScriptCompilerSupport.registerSlots(instruction).contains(where: { $0 != 0 && fields[$0] == destination }) { continue }
                var blocked = targets.contains(index)
                for i in (writer + 1)..<index {
                    let candidate = routine.code[i]
                    if targets.contains(i) || branches.contains(candidate.opcode) || candidate.opcode == .returnVoid || candidate.opcode == .returnValue {
                        blocked = true
                        break
                    }
                    if GameEventScriptCompilerSupport.registerSlots(candidate).contains(where: { candidate.fields[$0] == destination }) {
                        blocked = true
                        break
                    }
                    for slot in GameEventScriptCompilerSupport.listSlots(candidate, text: false) where lists[Int(candidate.fields[slot])].contains(destination) { blocked = true }
                }
                if !blocked {
                    var fields = fields
                    fields[0] = destination
                    routine.code[writer] = instruction.replacing(fields)
                    removed.insert(index)
                }
            }
            for index in routine.code.indices where branches.contains(routine.code[index].opcode) {
                let instruction = routine.code[index]
                var target = Int(instruction.word2)
                var seen: Set<Int> = []
                while target < routine.code.count && routine.code[target].opcode == .jump && seen.insert(target).inserted { target = Int(routine.code[target].word2) }
                routine.patch(index, target: target)
                if instruction.opcode == .jump && target == index + 1 { removed.insert(index) }
            }
            if removed.isEmpty { continue }
            var addresses = [Int](repeating: 0, count: routine.code.count + 1)
            var next = 0
            for index in routine.code.indices {
                addresses[index] = next
                if !removed.contains(index) { next += 1 }
            }
            addresses[routine.code.count] = next
            var code: [Instruction] = []
            var locations: [GameEventScriptSourceLocation] = []
            for index in routine.code.indices where !removed.contains(index) {
                let instruction = routine.code[index]
                var fields = instruction.fields
                if branches.contains(instruction.opcode) { fields[2] = UInt16(addresses[Int(instruction.word2)]) }
                code.append(instruction.replacing(fields))
                locations.append(routine.locations[index])
            }
            routine.code = code
            routine.locations = locations
            routine.calls = routine.calls.map { (addresses[$0.0], $0.1) }
        }
    }
}
