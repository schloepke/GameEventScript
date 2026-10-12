// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptVirtualMachine {
    static func nextComponents(_ state: GesVmState, _ instruction: GameEventScriptBytecodeInstruction) -> Bool {
        let iterator = state.slot(Int(instruction.word1)).iteratorValue
        let components = iterator as? any GesComponentIterator
        let item = components == nil ? iterator?.next() : nil
        let hasValue = components?.moveNext() ?? (item != nil)
        for (index, target) in state.linked!.program.uint16IndexLists[Int(instruction.word0)].enumerated() {
            var value = GesValue.nothing
            if hasValue {
                if let components {
                    value = components.component(index)
                } else if let item {
                    if let list = item.listValue {
                        value = index < list.count ? list[index] : .nothing
                    } else if item.kind == .map, let map = item.mapEntries {
                        value = index < map.count ? map[index].value : .nothing
                    } else if index == 0 {
                        value = item
                    }
                }
            }
            state.setValue(Int(target), value)
        }
        if !hasValue { state.ip = Int(instruction.word2) }
        return hasValue
    }
}
