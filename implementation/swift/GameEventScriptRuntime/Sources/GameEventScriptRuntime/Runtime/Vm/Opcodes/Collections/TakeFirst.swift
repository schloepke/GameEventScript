// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeTakeFirst(_ destinationRegister: Int, _ sourceRegister: Int, _ countOperand: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let count = max(0, countOperand)
        if registers[x].seriesValue != nil {
            return setList(destination, (0..<count).map { GesSeriesValue.term(registers[x].registerValue, index: Int64($0)) })
        }
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let length = GesCollectionOperators.length(registers[x].registerValue)
            if Int64(count) >= length { return setValue(destination, registers[x].registerValue) }
            if count == 0 { return setIntegerRange(destination, from: 0, to: 0, step: 0) }
            let first: Int64 = 1
            let last: Int64 = Int64(count)
            if let range = registers[x].integerRangeValue {
                guard let from = range.term(at: first), let to = range.term(at: last) else { return setNothing(destination) }
                return setIntegerRange(destination, from: from, to: to, step: range.step)
            }
            guard let range = registers[x].floatRangeValue, let from = range.term(at: first), let to = range.term(at: last) else { return setNothing(destination) }
            return setFloatRange(destination, from: from, to: to, step: range.step)
        }
        if let iterator = registers[x].iteratorValue {
            defer { iterator.close() }
            var values: [GesValue] = []
            while values.count < count, let value = iterator.next() { values.append(value) }
            return setList(destination, values)
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }
        let number = min(count, values.count)
        let result = Array(values.prefix(number))
        return registers[x].kind == .dice ? setDice(destination, result.map { Int32($0.asInteger) }) : setList(destination, result)
    }
}
