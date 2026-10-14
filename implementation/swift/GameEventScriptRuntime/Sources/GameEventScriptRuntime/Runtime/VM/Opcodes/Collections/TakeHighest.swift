// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeTakeHighest(_ destinationRegister: Int, _ sourceRegister: Int, _ countOperand: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let count = max(0, countOperand)
        if registers[x].seriesValue != nil { return setNothing(destination) }
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let length = GesCollectionOperators.length(registers[x].registerValue)
            let ascending = (registers[x].integerRangeValue?.step ?? 0) > 0 || (registers[x].floatRangeValue?.step ?? 0) > 0
            let reverse = ascending
            let n = min(Int64(count), length)
            if n <= 0 { return setIntegerRange(destination, from: 0, to: 0, step: 0) }
            if let range = registers[x].integerRangeValue {
                guard let origin = range.term(at: reverse ? length : 1) else { return setNothing(destination) }
                let step = reverse ? 0 &- range.step : range.step
                let ordered = GesIntegerRange(from: origin, to: reverse ? range.from : range.to, step: step)
                guard let from = ordered.term(at: 1), let to = ordered.term(at: n) else { return setNothing(destination) }
                return setIntegerRange(destination, from: from, to: to, step: step)
            }
            let range = registers[x].floatRangeValue!
            guard let from = reverse ? range.term(at: length) : range.term(at: 1) else { return setNothing(destination) }
            // Evaluate the endpoint from the reversed origin, matching the range's binary64 arithmetic.
            let step = reverse ? -range.step : range.step
            let end = reverse ? range.from : range.to
            if let integerFrom = GesNumber.exactInteger(from), let integerTo = GesNumber.exactInteger(end), let integerStep = GesNumber.exactInteger(step) {
                let ordered = GesIntegerRange(from: integerFrom, to: integerTo, step: integerStep)
                guard let selectedFrom = ordered.term(at: 1), let selectedTo = ordered.term(at: n) else { return setNothing(destination) }
                return setIntegerRange(destination, from: selectedFrom, to: selectedTo, step: integerStep)
            }
            let ordered = GesFloatRange(from: from, to: end, step: step)
            guard let selectedFrom = ordered.term(at: 1), let to = ordered.term(at: n) else { return setNothing(destination) }
            return setFloatRange(destination, from: selectedFrom, to: to, step: step)
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
        let result = chosen
        return registers[x].kind == .dice ? setDice(destination, result.map { Int32($0.asInteger) }) : setList(destination, result)
    }
}
