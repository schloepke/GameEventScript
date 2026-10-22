// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeTakeRandom(_ destinationRegister: Int, _ sourceRegister: Int, _ countOperand: Int, _ context: GesContext) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let count = max(0, countOperand)
        if registers[x].seriesValue != nil { return setNothing(destination) }
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let length = GesCollectionOperators.length(registers[x].registerValue)
            let selectedCount = min(Int64(count), length)
            var indexes: [Int64] = []
            for _ in 0..<selectedCount {
                if length > Int32.max {
                    var index: Int64
                    repeat { index = context.random.nextInclusiveInteger(0, length - 1) } while indexes.contains(index)
                    indexes.append(index)
                } else {
                    var index = context.random.nextInclusiveInteger(0, length - Int64(indexes.count) - 1)
                    for previous in indexes.sorted() { if previous <= index { index += 1 } }
                    indexes.append(index)
                }
            }
            return setList(destination, indexes.map { registers[x].index($0 + 1) })
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }
        let number = min(count, values.count)
        var available = Array(values.indices)
        var result: [GesValue] = []
        for _ in 0..<number { result.append(values[available.remove(at: Int(context.random.nextInclusiveInteger(0, Int64(available.count - 1))))]) }
        return registers[x].kind == .dice ? setDice(destination, result.map { Int32($0.asInteger) }) : setList(destination, result)
    }
}
