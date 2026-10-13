// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeHasPattern(_ destinationRegister: Int, _ sourceRegister: Int, _ countOperand: Int, _ operandA: UInt16, _ operandB: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let face = frameStart + operandB
        let pattern = GameEventScriptBytecodePatternKind(rawValue: operandA)!
        let count = countOperand
        if registers[x].kind == .series { return setNothing(destination) }
        let values: [GesValue]
        if let iterator = registers[x].iteratorValue {
            if !iterator.isPatternSequence { return setBoolean(destination, false) }
            values = GesCollectionOperators.read(iterator)
        } else if let array = GesCollectionOperators.materialize(registers[x], ranges: false) {
            values = array
        } else {
            return setBoolean(destination, false)
        }
        var result: [GesValue]?
        switch pattern {
        case .countFace:
            if registers[x].kind == .dice { return setBoolean(destination, registers[face].asNumber.isFinite && values.filter { $0.asInteger == GesNumber.saturatedInteger(registers[face].asNumber) }.count >= count) }
            let matches = values.filter { $0 == registers[face].registerValue }
            if matches.count >= count { result = Array(matches.prefix(max(0, count))) }
        case .countAny:
            for value in values {
                let matches = values.filter { $0 == value }
                if matches.count >= count {
                    result = Array(matches.prefix(max(0, count)))
                    break
                }
            }
        case .fullHouse:
            if values.count != 5 { return setBoolean(destination, false) }
            for triple in values {
                let triples = values.filter { $0 == triple }
                if triples.count < 3 { continue }
                for pair in values where pair != triple {
                    let pairs = values.filter { $0 == pair }
                    if pairs.count >= 2 {
                        result = Array(triples.prefix(3)) + Array(pairs.prefix(2))
                        break
                    }
                }
                if result != nil { break }
            }
        case .straight:
            if values.contains(where: { !$0.asNumber.isFinite }) { break }
            var seen = Set<Int64>()
            var selected: [GesValue] = []
            for value in values where seen.insert(GesNumber.saturatedInteger(value.asNumber)).inserted { selected.append(value) }
            let numbers = seen.sorted()
            if numbers.count < 2 { break }
            if numbers.indices.dropFirst().allSatisfy({ numbers[$0 - 1] != .max && numbers[$0 - 1] + 1 == numbers[$0] }) { result = selected }
        }
        return setBoolean(destination, result != nil)
    }
}
