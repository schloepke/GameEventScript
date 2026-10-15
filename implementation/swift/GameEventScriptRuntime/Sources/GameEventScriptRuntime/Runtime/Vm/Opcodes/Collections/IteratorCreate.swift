// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func compoundIterator(_ instruction: GesBytecodeInstruction, _ context: GesContext) throws {
        let mode = instruction.unitAndFlags >> 5
        let destination = Int(instruction.word0)
        if mode == 0 {
            createIterator(destination, value(Int(instruction.word1)), context)
            return
        }
        let result: GesIterator?
        if mode == 6 {
            if let map = try value(Int(instruction.word1)).materializedMap() { result = GesEntriesIterator(map.entries, budget: context.budget) } else { result = nil }
        } else {
            let sources = linked!.program.uint16IndexLists[Int(instruction.word1)].map { value(Int($0)) }
            result = GesCompoundSources.create(sources, mode: mode, budget: context.budget)
        }
        if let result { setIterator(destination, result) } else { setNothing(destination) }
    }
}
