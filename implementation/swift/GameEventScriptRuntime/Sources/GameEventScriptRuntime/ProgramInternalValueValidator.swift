// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Follow one producer at a time, matching C# without an instruction/register matrix.
enum GesProgramInternalValueValidator {
    static func validate(_ p: GameEventScriptProgram, _ entries: [Int], _ frames: [Int]) throws {
        var visited: [Int] = []
        var pending: [Int] = []
        var routine = -1
        for origin in p.code.indices {
            if routine + 1 < entries.count && origin == entries[routine + 1] { routine += 1 }
            let producer = p.code[origin]
            let kind = internalKind(producer.opcode)
            if kind == 0 || frames[origin] < 0 { continue }
            if visited.isEmpty { visited = [Int](repeating: 0, count: p.code.count) }
            pending.removeAll(keepingCapacity: true)
            let start = entries[routine]
            let end = routine + 1 < entries.count ? entries[routine + 1] : p.code.count
            let register = producer.word0

            func enqueue(_ address: Int) {
                if address < start || address >= end || visited[address] == origin + 1 { return }
                visited[address] = origin + 1
                pending.append(address)
            }

            // A failed IteratorCreateOrJump writes Nothing, not an internal handle.
            enqueue(origin + 1)
            var cursor = 0
            while cursor < pending.count {
                let address = pending[cursor]
                cursor += 1
                let i = p.code[address]
                if frames[address] <= Int(register) { continue }
                var overwritten = false
                for (position, operand) in i.operands.enumerated() {
                    if operand == .targetRegister {
                        overwritten = overwritten || i.word0 == register
                    } else if operand == .targetRegisterList {
                        overwritten = overwritten || p.uint16IndexLists[Int(i.word0)].contains(register)
                    } else if operand.isRegister && i.register(operand, position) == register {
                        if !allows(i, operand, kind) { throw GameEventScriptProgramValidator.failure(.invalidOperand, 16, address) }
                    } else if [.argumentRegisterList, .itemRegisterList, .valueRegisterList, .captureRegisterList, .sourceRegisterList, .tagRegisterList].contains(operand) {
                        if p.uint16IndexLists[Int(i.list(operand))].contains(register) { throw GameEventScriptProgramValidator.failure(.invalidOperand, 16, address) }
                    }
                }
                if overwritten || i.opcode == .iteratorClose && i.word1 == register || [.returnVoid, .returnValue].contains(i.opcode) { continue }
                if [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorCreateOrJump, .iteratorNext].contains(i.opcode) { enqueue(Int(i.word2)) }
                if i.opcode != .jump { enqueue(address + 1) }
            }
        }
    }

    private static func allows(_ i: GameEventScriptBytecodeInstruction, _ operand: GesOperand, _ kind: Int) -> Bool {
        let op = i.opcode
        if operand == .builderRegister {
            let expected: Int
            switch op {
            case .listBuilderAdd, .listBuilderFinish: expected = 2
            case .mapBuilderAdd, .mapBuilderFinish: expected = 3
            case .distinctBuilderAdd, .distinctBuilderFinish: expected = 4
            case .groupBuilderAdd, .groupBuilderFinish: expected = 5
            case .orderBuilderAdd, .orderBuilderFinishAscending, .orderBuilderFinishDescending: expected = 6
            default: expected = 0
            }
            return kind == expected
        }
        if kind != 1 { return false }
        if [.iteratorNext, .iteratorClose].contains(op) { return operand == .iteratorRegister }
        if [.containsAny, .containsAll].contains(op) { return [.leftRegister, .rightRegister, .needleRegister, .collectionRegister].contains(operand) }
        return [.sourceRegister, .sourceIteratorRegister, .operandRegister, .collectionRegister].contains(operand)
            && [
                .takeFirst, .dropFirst, .takeLast, .dropLast, .takeHighest, .takeLowest, .dropHighest, .dropLowest, .oneRandom, .takeRandom,
                .count, .hasAny, .hasAll, .first, .last, .single, .distinct, .sortAscending, .sortDescending, .reverse, .shuffle, .hasPattern, .takePattern,
            ].contains(op)
    }

    private static func internalKind(_ op: GameEventScriptBytecodeOpCode) -> Int {
        switch op {
        case .iteratorCreate, .iteratorCreateOrJump, .createRangeIterator, .createRangeIteratorWithStep, .createRangeIteratorShort: return 1
        case .listBuilderCreate: return 2
        case .mapBuilderCreate: return 3
        case .distinctBuilderCreate: return 4
        case .groupBuilderCreate: return 5
        case .orderBuilderCreate: return 6
        default: return 0
        }
    }
}
