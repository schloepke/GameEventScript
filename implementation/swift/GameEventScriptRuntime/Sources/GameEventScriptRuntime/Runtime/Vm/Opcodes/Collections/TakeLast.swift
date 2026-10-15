// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeTakeLast(_ destinationRegister: Int, _ sourceRegister: Int, _ countOperand: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let count = max(0, countOperand)
        if registers[x].seriesValue != nil { return setNothing(destination) }
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let length = GesCollectionOperators.length(registers[x].registerValue)
            if Int64(count) >= length { return setValue(destination, registers[x].registerValue) }
            if count == 0 { return setIntegerRange(destination, from: 0, to: 0, step: 0) }
            let first: Int64 = length - Int64(count) + 1
            if let range = registers[x].integerRangeValue {
                guard let from = range.term(at: first) else { return setNothing(destination) }
                return setIntegerRange(destination, from: from, to: range.to, step: range.step)
            }
            guard let range = registers[x].floatRangeValue, let from = range.term(at: first) else { return setNothing(destination) }
            return setFloatRange(destination, from: from, to: range.to, step: range.step)
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }
        let number = min(count, values.count)
        let result = Array(values.suffix(number))
        return registers[x].kind == .dice ? setDice(destination, result.map { Int32($0.asInteger) }) : setList(destination, result)
    }
}
