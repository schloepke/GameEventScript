// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDropHighest(_ destinationRegister: Int, _ sourceRegister: Int, _ countOperand: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let count = max(0, countOperand)
        if registers[x].seriesValue != nil { return setNothing(destination) }
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let length = GesCollectionOperators.length(registers[x].registerValue)
            if count == 0 { return setValue(destination, registers[x].registerValue) }
            if Int64(count) >= length { return setIntegerRange(destination, from: 0, to: 0, step: 0) }
            let ascending = (registers[x].integerRangeValue?.step ?? 0) > 0 || (registers[x].floatRangeValue?.step ?? 0) > 0
            let dropLast = ascending
            let first: Int64 = dropLast ? 1 : Int64(count) + 1
            let last = dropLast ? length - Int64(count) : length
            let preserveEnd = !dropLast
            if let range = registers[x].integerRangeValue {
                guard let from = range.term(at: first), let to = preserveEnd ? range.to : range.term(at: last) else { return setNothing(destination) }
                return setIntegerRange(destination, from: from, to: to, step: range.step)
            }
            guard let range = registers[x].floatRangeValue, let from = range.term(at: first), let to = preserveEnd ? range.to : range.term(at: last) else { return setNothing(destination) }
            return setFloatRange(destination, from: from, to: to, step: range.step)
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }
        let number = min(count, values.count)
        var selected = [Bool](repeating: false, count: values.count)
        var chosen: [GesValue] = []
        for _ in 0..<number {
            var best: Int?
            for index in values.indices where !selected[index] {
                if let old = best {
                    let comparison = GesSelectionOrder.compare(values[index], values[old])
                    if comparison > 0 { best = index }
                } else {
                    best = index
                }
            }
            selected[best!] = true
            chosen.append(values[best!])
        }
        let result = values.indices.filter { !selected[$0] }.map { values[$0] }
        return registers[x].kind == .dice ? setDice(destination, result.map { Int32($0.asInteger) }) : setList(destination, result)
    }
}
