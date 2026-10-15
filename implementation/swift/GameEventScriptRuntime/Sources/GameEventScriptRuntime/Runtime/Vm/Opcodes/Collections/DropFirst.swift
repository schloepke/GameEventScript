// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeDropFirst(_ destinationRegister: Int, _ sourceRegister: Int, _ countOperand: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let count = max(0, countOperand)
        if let series = registers[x].seriesValue {
            let (offset, overflow) = series.offset.addingReportingOverflow(Int64(count))
            return setSeries(destination, .init(signatureID: series.signatureID, offset: overflow ? .max : offset))
        }
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let length = GesCollectionOperators.length(registers[x].registerValue)
            if count == 0 { return setValue(destination, registers[x].registerValue) }
            if Int64(count) >= length { return setIntegerRange(destination, from: 0, to: 0, step: 0) }
            let first: Int64 = Int64(count) + 1
            if let range = registers[x].integerRangeValue {
                guard let from = range.term(at: first) else { return setNothing(destination) }
                return setIntegerRange(destination, from: from, to: range.to, step: range.step)
            }
            guard let range = registers[x].floatRangeValue, let from = range.term(at: first) else { return setNothing(destination) }
            return setFloatRange(destination, from: from, to: range.to, step: range.step)
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }
        let number = min(count, values.count)
        let result = Array(values.dropFirst(number))
        return registers[x].kind == .dice ? setDice(destination, result.map { Int32($0.asInteger) }) : setList(destination, result)
    }
}
