// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

enum GesMath {
    static func execute(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws -> GesValue {
        let a = s.value(Int(i.word1))
        // Read the second register only for instructions which actually have one.
        let b: GesValue
        switch i.opcode {
        case .or, .and, .implies, .xor, .equal, .notEqual, .less, .greater, .lessOrEqual, .greaterOrEqual, .add, .subtract, .multiply, .divide, .integerDivide, .modulo, .remainder, .power, .min, .max, .clamp, .randomTake, .randomTakeFloat, .term:
            b = s.value(Int(i.word2))
        default: b = .nothing
        }
        switch i.opcode {
        case .or:
            if a.truth == true || b.truth == true { return .boolean(true) }
            return a.truth != nil && b.truth != nil ? .boolean(false) : .nothing
        case .and:
            if a.truth == false || b.truth == false { return .boolean(false) }
            return a.truth != nil && b.truth != nil ? .boolean(true) : .nothing
        case .implies:
            if a.truth == false || b.truth == true { return .boolean(true) }
            return a.truth != nil && b.truth != nil ? .boolean(false) : .nothing
        case .xor: return a.truth.flatMap { av in b.truth.map { .boolean(av != $0) } } ?? .nothing
        case .not: return a.truth.map { .boolean(!$0) } ?? .nothing
        case .equal, .notEqual:
            if a.isNothing || b.isNothing { return .nothing }
            return .boolean(GesComparison.equal(a, b) == (i.opcode == .equal))
        case .less, .greater, .lessOrEqual, .greaterOrEqual:
            if a.isNothing || b.isNothing { return .nothing }
            if !a.isNumeric || !b.isNumeric || a.unit != b.unit { return .boolean(false) }
            if let av = a.integerValue, let bv = b.integerValue {
                switch i.opcode {
                case .less: return .boolean(av < bv)
                case .greater: return .boolean(av > bv)
                case .lessOrEqual: return .boolean(av <= bv)
                default: return .boolean(av >= bv)
                }
            }
            switch i.opcode {
            case .less: return .boolean(a.asNumber < b.asNumber)
            case .greater: return .boolean(a.asNumber > b.asNumber)
            case .lessOrEqual: return .boolean(a.asNumber <= b.asNumber)
            default: return .boolean(a.asNumber >= b.asNumber)
            }
        case .add, .subtract: return add(a, b, subtract: i.opcode == .subtract)
        case .multiply, .divide, .integerDivide, .modulo, .remainder, .power: return arithmetic(i.opcode, a, b)
        case .min, .max: return GesComparison.extreme(a, b, maximum: i.opcode == .max)
        case .negate, .abs:
            if let integer = a.integerValue { return integer == .min ? .float(-Double(integer), unit: a.unit) : .integer(i.opcode == .abs ? Swift.abs(integer) : -integer, unit: a.unit) }
            if a.kind == .point { return .nothing }
            if a.kind == .vector { return i.opcode == .abs ? .float((a.x * a.x + a.y * a.y + a.z * a.z).squareRoot(), unit: a.unit) : .vector(x: -a.x, y: -a.y, z: -a.z, unit: a.unit) }
            let number = i.opcode == .abs ? Swift.abs(a.asNumber) : -a.asNumber
            return a.kind == .percentage ? .percentage(number) : .float(number, unit: a.unit)
        case .clamp:
            let upper = s.value(Int(i.a))
            guard a.unit == b.unit, b.unit == upper.unit, a.isNumeric, b.isNumeric, upper.isNumeric else { return .nothing }
            if let av = a.integerValue, let bv = b.integerValue, let cv = upper.integerValue { return .integer(Swift.min(Swift.max(av, Swift.min(bv, cv)), Swift.max(bv, cv)), unit: a.unit) }
            return .float(Swift.min(Swift.max(a.asNumber, Swift.min(b.asNumber, upper.asNumber)), Swift.max(b.asNumber, upper.asNumber)), unit: a.unit)
        case .logN: return a.hasUnit ? .nothing : .float(log(a.asNumber))
        case .exp: return a.hasUnit ? .nothing : .float(exp(a.asNumber))
        case .floor, .ceil, .truncate, .roundHalfEven, .roundHalfUp, .roundHalfDown:
            if !a.isNumeric { return .nothing }
            let n = a.asNumber
            let result: Double
            switch i.opcode {
            case .floor: result = n.rounded(.down)
            case .ceil: result = n.rounded(.up)
            case .truncate: result = n.rounded(.towardZero)
            case .roundHalfEven: result = n.rounded(.toNearestOrEven)
            case .roundHalfUp: result = n.rounded(.toNearestOrAwayFromZero)
            default:
                let truncated = n.rounded(.towardZero)
                result = Swift.abs(n - truncated) <= 0.5 ? truncated : n.rounded(.toNearestOrAwayFromZero)
            }
            return .integer(GesNumber.saturatedInteger(result))
        case .degreeToRadians: return a.isNumeric && a.asNumber.isFinite && (a.unit == .none || a.unit == .degree) ? .float(a.asNumber / 180 * Double.pi) : .nothing
        case .degreeFromRadians: return a.isNumeric && a.asNumber.isFinite && !a.hasUnit ? .float(a.asNumber / Double.pi * 180, unit: .degree) : .nothing
        case .wrapDegree:
            if !a.isNumeric || !a.asNumber.isFinite || (a.unit != .none && a.unit != .degree) { return .nothing }
            var value = a.asNumber.truncatingRemainder(dividingBy: 360)
            if value < 0 { value += 360 }
            return .float(value == 360 ? 0 : value, unit: .degree)
        case .chance:
            if !a.numericOnly || a.hasUnit || !a.asNumber.isFinite { return .nothing }
            let ratio = a.kind == .integer || (a.kind == .float && Swift.abs(a.asNumber) > 1) ? a.asNumber / 100 : a.asNumber
            return .boolean(ratio <= 0 ? false : ratio >= 1 ? true : c.random.nextFloat(0, 1) < ratio)
        case .randomTake, .randomTakeFloat:
            if a.unit != b.unit { return .nothing }
            if i.opcode == .randomTake, let av = a.integerValue, let bv = b.integerValue { return .integer(c.random.nextInclusiveInteger(av, bv), unit: a.unit) }
            return a.asNumber.isFinite && b.asNumber.isFinite ? .float(c.random.nextFloat(a.asNumber, b.asNumber), unit: a.unit) : .nothing
        case .term: return b.isNumeric ? GesSeries.term(a, index: b.asInteger) : .nothing
        default: return GesNavigation.execute(i, s)
        }
    }

    static func add(_ a: GesValue, _ b: GesValue, subtract: Bool = false) -> GesValue {
        if a.isNothing || (b.isNothing && !(subtract && a.kind == .list)) { return .nothing }
        if !subtract && (a.kind == .text || b.kind == .text) { return .text(a.toText + b.toText) }
        if subtract && a.kind == .list && b.kind == .map { return .nothing }
        if a.kind == .list || (b.kind == .list && !(subtract && a.kind == .map)) {
            if !subtract { return a.listValue.map { .list($0 + [b]) } ?? .list([a] + b.listValue!) }
            if let list = a.listValue { return .list(removing(list, b.listValue ?? b.diceRolls?.map { .integer(Int64($0)) } ?? [b])) }
            if let dice = a.diceRolls { return .list(removing(dice.map { .integer(Int64($0)) }, b.listValue!)) }
            return .nothing
        }
        if a.kind == .dice || b.kind == .dice {
            if !subtract {
                if let dice = a.diceRolls, validFace(b) { return .dice(dice + [Int32(b.asInteger)]) }
                if let dice = b.diceRolls, validFace(a) { return .dice([Int32(a.asInteger)] + dice) }
                return .nothing
            }
            if let dice = a.diceRolls {
                if let other = b.diceRolls { return .dice(removing(dice.map { .integer(Int64($0)) }, other.map { .integer(Int64($0)) }).map { Int32($0.asInteger) }) }
                if validFace(b) { return .dice(removing(dice.map { .integer(Int64($0)) }, [b]).map { Int32($0.asInteger) }) }
                return .nothing
            }
        }
        if a.kind == .map && subtract {
            let keys: [String]
            if b.kind == .map { keys = b.asMap!.entries.map(\.key) } else if let key = b.textValue { keys = [key] } else if let list = b.listValue, list.allSatisfy({ $0.textValue != nil }) { keys = list.map(\.asText) } else { return .nothing }
            return .map(a.mapEntries!.filter { entry in !keys.contains { GesText.scalarEqual($0, entry.key) } })
        }
        if a.spatialValue != nil || b.spatialValue != nil {
            guard a.unit == b.unit else { return .nothing }
            if a.kind == .vector && b.kind == .vector || a.kind == .point && b.kind == .vector || subtract && a.kind == .point && b.kind == .point {
                let x = subtract ? a.x - b.x : a.x + b.x
                let y = subtract ? a.y - b.y : a.y + b.y
                let z = subtract ? a.z - b.z : a.z + b.z
                return a.kind == .point && b.kind == .vector ? .point(x: x, y: y, z: z, unit: a.unit) : .vector(x: x, y: y, z: z, unit: a.unit)
            }
            return .nothing
        }
        if a.kind == .percentage {
            if b.kind != .percentage { return .nothing }
            return .percentage(subtract ? a.asNumber - b.asNumber : a.asNumber + b.asNumber)
        }
        if b.kind == .percentage {
            let delta = a.asNumber * b.asNumber
            return .float(subtract ? a.asNumber - delta : a.asNumber + delta, unit: a.unit)
        }
        if a.unit != b.unit { return .nothing }
        if let av = a.integerValue, let bv = b.integerValue {
            let (result, overflow) = subtract ? av.subtractingReportingOverflow(bv) : av.addingReportingOverflow(bv)
            if !overflow { return .integer(result, unit: a.unit) }
        }
        return .float(subtract ? a.asNumber - b.asNumber : a.asNumber + b.asNumber, unit: a.unit)
    }

    private static func validFace(_ value: GesValue) -> Bool { value.kind == .integer && !value.hasUnit && value.asInteger > 0 && value.asInteger <= Int32.max }

    private static func removing(_ values: [GesValue], _ remove: [GesValue]) -> [GesValue] {
        var used = [Bool](repeating: false, count: remove.count)
        return values.filter { value in
            if let index = remove.indices.first(where: { !used[$0] && remove[$0] == value }) {
                used[index] = true
                return false
            }
            return true
        }
    }

    static func arithmetic(_ op: GameEventScriptBytecodeOpCode, _ a: GesValue, _ b: GesValue) -> GesValue {
        if a.isNothing || b.isNothing { return .nothing }
        if op == .power {
            if b.hasUnit || a.asNumber.isNaN || b.asNumber.isNaN || (a.hasUnit && b.asNumber != 0 && b.asNumber != 1) { return .nothing }
            return .float(pow(a.asNumber, b.asNumber), unit: b.asNumber == 1 ? a.unit : .none)
        }
        let unit: GesUnit?
        if op == .multiply {
            unit = a.hasUnit && b.hasUnit ? nil : a.hasUnit ? a.unit : b.unit
        } else if op == .divide || op == .integerDivide {
            unit = !b.hasUnit ? a.unit : a.unit == b.unit ? GesUnit.none : nil
        } else {
            unit = a.unit == b.unit ? a.unit : nil
        }
        guard let unit else { return .nothing }
        if a.spatialValue != nil || b.spatialValue != nil {
            if op == .multiply {
                let vector = a.kind == .vector ? a : b
                let scalar = a.kind == .vector ? b : a
                guard vector.kind == .vector, scalar.asNumber.isFinite else { return .nothing }
                return .vector(x: vector.x * scalar.asNumber, y: vector.y * scalar.asNumber, z: vector.z * scalar.asNumber, unit: unit)
            }
            if op == .divide && a.kind == .vector && b.asNumber.isFinite && b.asNumber != 0 { return .vector(x: a.x / b.asNumber, y: a.y / b.asNumber, z: a.z / b.asNumber, unit: unit) }
            return .nothing
        }
        if let av = a.integerValue, let bv = b.integerValue {
            if op == .multiply {
                let (result, overflow) = av.multipliedReportingOverflow(by: bv)
                if !overflow { return .integer(result, unit: unit) }
            }
            if op == .integerDivide && bv != 0 && !(av == .min && bv == -1) {
                var quotient = av / bv
                if av % bv != 0 && (av < 0) != (bv < 0) { quotient -= 1 }
                return .integer(quotient, unit: unit)
            }
            if op == .modulo || op == .remainder {
                if bv == 0 { return .nothing }
                var remainder = av == .min && bv == -1 ? 0 : av % bv
                if op == .modulo && remainder != 0 && (remainder < 0) != (bv < 0) { remainder += bv }
                return .integer(remainder, unit: unit)
            }
        }
        let x = a.asNumber
        let y = b.asNumber
        switch op {
        case .multiply: return a.kind == .percentage && b.kind == .percentage ? .percentage(x * y) : .float(x * y, unit: unit)
        case .divide: return a.kind == .percentage && b.kind != .percentage ? .percentage(x / y) : .float(x / y, unit: unit)
        case .integerDivide: return .float((x / y).rounded(.down), unit: unit)
        case .modulo, .remainder:
            if !x.isFinite || y.isNaN || y == 0 { return .nothing }
            var result = y.isInfinite ? x : x.truncatingRemainder(dividingBy: y)
            if op == .modulo && y.isFinite && result != 0 && (result < 0) != (y < 0) { result += y }
            return .float(result, unit: unit)
        default: return .nothing
        }
    }
}
