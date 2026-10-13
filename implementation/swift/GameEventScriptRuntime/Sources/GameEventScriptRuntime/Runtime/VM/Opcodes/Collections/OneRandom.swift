// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeOneRandom(_ destinationRegister: Int, _ sourceRegister: Int, _ context: GameEventScriptContext) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        if registers[x].seriesValue != nil { return setNothing(destination) }
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let length = GesCollectionOperators.length(registers[x].registerValue)
            return length == 0 ? setNothing(destination) : registers[x].index(context.random.nextInclusiveInteger(0, length - 1) + 1, sink: output(destination))
        }
        if let iterator = registers[x].iteratorValue {
            defer { iterator.close() }
            var chosen = GesValue.nothing
            var seen: Int64 = 0
            while let value = iterator.next() {
                seen += 1
                if context.random.nextInclusiveInteger(1, seen) == 1 { chosen = value }
            }
            return setValue(destination, chosen)
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }
        return values.isEmpty ? setNothing(destination) : setValue(destination, values[Int(context.random.nextInclusiveInteger(0, Int64(values.count - 1)))])
    }
}
