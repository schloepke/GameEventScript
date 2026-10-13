// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptVirtualMachine {
    static func compoundIterator(_ state: GesVmState, _ instruction: GameEventScriptBytecodeInstruction, _ context: GameEventScriptContext) throws {
        let mode = instruction.unitAndFlags >> 5
        let destination = Int(instruction.word0)
        if mode == 0 {
            iterator(state, destination, state.value(Int(instruction.word1)), context)
            return
        }
        let result: GesIterator?
        if mode == 6 {
            if let map = try state.value(Int(instruction.word1)).materializedMap() { result = GesEntriesIterator(map.entries, budget: context.budget) } else { result = nil }
        } else {
            let sources = state.linked!.program.uint16IndexLists[Int(instruction.word1)].map { state.value(Int($0)) }
            result = GesCompoundSources.create(sources, mode: mode, budget: context.budget)
        }
        if let result { state.setIterator(destination, result) } else { state.setNothing(destination) }
    }
}
