// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesPatterns {
    static func execute(_ slot: GesVmState.Slot, pattern: GameEventScriptBytecodePatternKind, count: Int, face: GesValue, take: Bool) -> GesValue {
        let source = slot.value
        if source.kind == .series { return .nothing }
        let values: [GesValue]
        if case .iterator(let iterator) = slot {
            if !iterator.isPatternSequence { return take ? .nothing : .boolean(false) }
            values = GesCollectionOperators.read(iterator)
        } else if let array = GesCollectionOperators.materialize(slot, ranges: false) {
            values = array
        } else {
            return take ? .nothing : .boolean(false)
        }
        var result: [GesValue]?
        switch pattern {
        case .countFace:
            if !take && source.kind == .dice { return .boolean(face.asNumber.isFinite && values.filter { $0.asInteger == GesNumber.saturatedInteger(face.asNumber) }.count >= count) }
            let matches = values.filter { $0 == face }
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
            if !take && values.count != 5 { return .boolean(false) }
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
        if !take { return .boolean(result != nil) }
        guard let result else { return .nothing }
        return source.kind == .dice ? .dice(result.map { Int32($0.asInteger) }) : .list(result)
    }
}
