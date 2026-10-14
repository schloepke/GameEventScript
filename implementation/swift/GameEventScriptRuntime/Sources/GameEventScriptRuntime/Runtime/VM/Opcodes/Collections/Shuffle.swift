// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeShuffle(_ destinationRegister: Int, _ sourceRegister: Int, _ context: GameEventScriptContext) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        guard var values = GesCollectionOperators.materialize(registers[x]) else { return setNothing(destination) }
        if values.count > 1 { for index in stride(from: values.count - 1, through: 1, by: -1) { values.swapAt(index, Int(context.random.nextInclusiveInteger(0, Int64(index)))) } }
        return setList(destination, values)
    }
}
