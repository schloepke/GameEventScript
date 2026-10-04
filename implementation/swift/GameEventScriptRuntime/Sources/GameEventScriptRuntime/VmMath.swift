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
    static func execute<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, sink: Output) throws -> Output.Result {
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
            if a.truth == true || b.truth == true { return sink.boolean(true) }
            return a.truth != nil && b.truth != nil ? sink.boolean(false) : sink.nothing
        case .and:
            if a.truth == false || b.truth == false { return sink.boolean(false) }
            return a.truth != nil && b.truth != nil ? sink.boolean(true) : sink.nothing
        case .implies:
            if a.truth == false || b.truth == true { return sink.boolean(true) }
            return a.truth != nil && b.truth != nil ? sink.boolean(false) : sink.nothing
        case .xor:
            guard let av = a.truth, let bv = b.truth else { return sink.nothing }
            return sink.boolean(av != bv)
        case .not:
            guard let truth = a.truth else { return sink.nothing }
            return sink.boolean(!truth)
        case .equal, .notEqual:
            if a.isNothing || b.isNothing { return sink.nothing }
            return sink.boolean(GesComparison.equal(a, b) == (i.opcode == .equal))
        case .less, .greater, .lessOrEqual, .greaterOrEqual:
            if a.isNothing || b.isNothing { return sink.nothing }
            if !a.isNumeric || !b.isNumeric || a.unit != b.unit { return sink.boolean(false) }
            if let av = a.integerValue, let bv = b.integerValue {
                switch i.opcode {
                case .less: return sink.boolean(av < bv)
                case .greater: return sink.boolean(av > bv)
                case .lessOrEqual: return sink.boolean(av <= bv)
                default: return sink.boolean(av >= bv)
                }
            }
            switch i.opcode {
            case .less: return sink.boolean(a.asNumber < b.asNumber)
            case .greater: return sink.boolean(a.asNumber > b.asNumber)
            case .lessOrEqual: return sink.boolean(a.asNumber <= b.asNumber)
            default: return sink.boolean(a.asNumber >= b.asNumber)
            }
        case .add, .subtract: return add(a, b, subtract: i.opcode == .subtract, sink: sink)
        case .multiply, .divide, .integerDivide, .modulo, .remainder, .power: return arithmetic(i.opcode, a, b, sink: sink)
        case .min, .max: return sink.copy(GesComparison.extreme(a, b, maximum: i.opcode == .max))
        case .negate, .abs:
            if let integer = a.integerValue { return integer == .min ? sink.float(-Double(integer), unit: a.unit) : sink.integer(i.opcode == .abs ? Swift.abs(integer) : -integer, unit: a.unit) }
            if a.kind == .point { return sink.nothing }
            if a.kind == .vector { return i.opcode == .abs ? sink.float((a.x * a.x + a.y * a.y + a.z * a.z).squareRoot(), unit: a.unit) : sink.vector(x: -a.x, y: -a.y, z: -a.z, unit: a.unit) }
            let number = i.opcode == .abs ? Swift.abs(a.asNumber) : -a.asNumber
            return a.kind == .percentage ? sink.percentage(number) : sink.float(number, unit: a.unit)
        case .clamp:
            let upper = s.value(Int(i.a))
            guard a.unit == b.unit, b.unit == upper.unit, a.isNumeric, b.isNumeric, upper.isNumeric else { return sink.nothing }
            if let av = a.integerValue, let bv = b.integerValue, let cv = upper.integerValue { return sink.integer(Swift.min(Swift.max(av, Swift.min(bv, cv)), Swift.max(bv, cv)), unit: a.unit) }
            return sink.float(Swift.min(Swift.max(a.asNumber, Swift.min(b.asNumber, upper.asNumber)), Swift.max(b.asNumber, upper.asNumber)), unit: a.unit)
        case .logN: return a.hasUnit ? sink.nothing : sink.float(log(a.asNumber))
        case .exp: return a.hasUnit ? sink.nothing : sink.float(exp(a.asNumber))
        case .floor, .ceil, .truncate, .roundHalfEven, .roundHalfUp, .roundHalfDown:
            if !a.isNumeric { return sink.nothing }
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
            return sink.integer(GesNumber.saturatedInteger(result))
        case .degreeToRadians: return a.isNumeric && a.asNumber.isFinite && (a.unit == .none || a.unit == .degree) ? sink.float(a.asNumber / 180 * Double.pi) : sink.nothing
        case .degreeFromRadians: return a.isNumeric && a.asNumber.isFinite && !a.hasUnit ? sink.float(a.asNumber / Double.pi * 180, unit: .degree) : sink.nothing
        case .wrapDegree:
            if !a.isNumeric || !a.asNumber.isFinite || (a.unit != .none && a.unit != .degree) { return sink.nothing }
            var value = a.asNumber.truncatingRemainder(dividingBy: 360)
            if value < 0 { value += 360 }
            return sink.float(value == 360 ? 0 : value, unit: .degree)
        case .chance:
            if !a.numericOnly || a.hasUnit || !a.asNumber.isFinite { return sink.nothing }
            let ratio = a.kind == .integer || (a.kind == .float && Swift.abs(a.asNumber) > 1) ? a.asNumber / 100 : a.asNumber
            return sink.boolean(ratio <= 0 ? false : ratio >= 1 ? true : c.random.nextFloat(0, 1) < ratio)
        case .randomTake, .randomTakeFloat:
            if a.unit != b.unit { return sink.nothing }
            if i.opcode == .randomTake, let av = a.integerValue, let bv = b.integerValue { return sink.integer(c.random.nextInclusiveInteger(av, bv), unit: a.unit) }
            return a.asNumber.isFinite && b.asNumber.isFinite ? sink.float(c.random.nextFloat(a.asNumber, b.asNumber), unit: a.unit) : sink.nothing
        case .term: return b.isNumeric ? GesSeries.term(a, index: b.asInteger, sink: sink) : sink.nothing
        default: return GesNavigation.execute(i, s, sink: sink)
        }
    }

    static func add(_ a: GesValue, _ b: GesValue, subtract: Bool = false) -> GesValue {
        add(a, b, subtract: subtract, sink: GesValueFactory())
    }

    static func add<Output: GesValueOutput>(_ a: GesValue, _ b: GesValue, subtract: Bool = false, sink: Output) -> Output.Result {
        if let av = a.integerValue, let bv = b.integerValue { return integerArithmetic(subtract ? .subtract : .add, av, bv, a.unit, b.unit, sink: sink) }
        if a.isNothing || (b.isNothing && !(subtract && a.kind == .list)) { return sink.nothing }
        if !subtract && (a.kind == .text || b.kind == .text) { return sink.text(a.toText + b.toText) }
        if subtract && a.kind == .list && b.kind == .map { return sink.nothing }
        if a.kind == .list || (b.kind == .list && !(subtract && a.kind == .map)) {
            if !subtract {
                if let list = a.listValue { return sink.list(list + [b]) }
                return sink.list([a] + b.listValue!)
            }
            if let list = a.listValue { return sink.list(removing(list, b.listValue ?? b.diceRolls?.map { .integer(Int64($0)) } ?? [b])) }
            if let dice = a.diceRolls { return sink.list(removing(dice.map { .integer(Int64($0)) }, b.listValue!)) }
            return sink.nothing
        }
        if a.kind == .dice || b.kind == .dice {
            if !subtract {
                if let dice = a.diceRolls, validFace(b) { return sink.dice(dice + [Int32(b.asInteger)]) }
                if let dice = b.diceRolls, validFace(a) { return sink.dice([Int32(a.asInteger)] + dice) }
                return sink.nothing
            }
            if let dice = a.diceRolls {
                if let other = b.diceRolls { return sink.dice(removing(dice.map { .integer(Int64($0)) }, other.map { .integer(Int64($0)) }).map { Int32($0.asInteger) }) }
                if validFace(b) { return sink.dice(removing(dice.map { .integer(Int64($0)) }, [b]).map { Int32($0.asInteger) }) }
                return sink.nothing
            }
        }
        if a.kind == .map && subtract {
            let keys: [String]
            if b.kind == .map { keys = b.asMap!.entries.map(\.key) } else if let key = b.textValue { keys = [key] } else if let list = b.listValue, list.allSatisfy({ $0.textValue != nil }) { keys = list.map(\.asText) } else { return sink.nothing }
            return sink.map(a.mapEntries!.filter { entry in !keys.contains { GesText.scalarEqual($0, entry.key) } })
        }
        if a.spatialValue != nil || b.spatialValue != nil {
            guard a.unit == b.unit else { return sink.nothing }
            if a.kind == .vector && b.kind == .vector || a.kind == .point && b.kind == .vector || subtract && a.kind == .point && b.kind == .point {
                let x = subtract ? a.x - b.x : a.x + b.x
                let y = subtract ? a.y - b.y : a.y + b.y
                let z = subtract ? a.z - b.z : a.z + b.z
                return a.kind == .point && b.kind == .vector ? sink.point(x: x, y: y, z: z, unit: a.unit) : sink.vector(x: x, y: y, z: z, unit: a.unit)
            }
            return sink.nothing
        }
        if a.kind == .percentage {
            if b.kind != .percentage { return sink.nothing }
            return sink.percentage(subtract ? a.asNumber - b.asNumber : a.asNumber + b.asNumber)
        }
        if b.kind == .percentage {
            let delta = a.asNumber * b.asNumber
            return sink.float(subtract ? a.asNumber - delta : a.asNumber + delta, unit: a.unit)
        }
        if a.unit != b.unit { return sink.nothing }
        return sink.float(subtract ? a.asNumber - b.asNumber : a.asNumber + b.asNumber, unit: a.unit)
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

    // Numeric ALU results cross the helper boundary as scalars, never full GesValue storage.
    enum IntegerResult {
        case nothing
        case integer(Int64, unit: GesUnit)
        case float(Double, unit: GesUnit)

    }

    static func integerArithmetic<Output: GesValueOutput>(_ op: GameEventScriptBytecodeOpCode, _ a: Int64, _ b: Int64, _ leftUnit: GesUnit, _ rightUnit: GesUnit, sink: Output) -> Output.Result {
        switch integerArithmeticResult(op, a, b, leftUnit, rightUnit) {
        case .nothing: return sink.nothing
        case .integer(let value, let unit): return sink.integer(value, unit: unit)
        case .float(let value, let unit): return sink.float(value, unit: unit)
        }
    }

    static func integerArithmeticResult(_ op: GameEventScriptBytecodeOpCode, _ a: Int64, _ b: Int64, _ leftUnit: GesUnit, _ rightUnit: GesUnit) -> IntegerResult {
        switch op {
        case .add, .subtract:
            guard leftUnit == rightUnit else { return .nothing }
            let (result, overflow) = op == .subtract ? a.subtractingReportingOverflow(b) : a.addingReportingOverflow(b)
            return overflow ? .float(op == .subtract ? Double(a) - Double(b) : Double(a) + Double(b), unit: leftUnit) : .integer(result, unit: leftUnit)
        case .multiply:
            guard leftUnit == .none || rightUnit == .none else { return .nothing }
            let unit = leftUnit == .none ? rightUnit : leftUnit
            let (result, overflow) = a.multipliedReportingOverflow(by: b)
            return overflow ? .float(Double(a) * Double(b), unit: unit) : .integer(result, unit: unit)
        case .divide, .integerDivide:
            guard rightUnit == .none || leftUnit == rightUnit else { return .nothing }
            let unit: GesUnit = rightUnit == .none ? leftUnit : .none
            if op == .integerDivide, b != 0, !(a == .min && b == -1) {
                var result = a / b
                if a % b != 0 && (a < 0) != (b < 0) { result -= 1 }
                return .integer(result, unit: unit)
            }
            let result = Double(a) / Double(b)
            return .float(op == .integerDivide ? result.rounded(.down) : result, unit: unit)
        case .modulo, .remainder:
            guard leftUnit == rightUnit, b != 0 else { return .nothing }
            var result = a == .min && b == -1 ? 0 : a % b
            if op == .modulo && result != 0 && (result < 0) != (b < 0) { result += b }
            return .integer(result, unit: leftUnit)
        default: return .nothing
        }
    }

    static func arithmetic(_ op: GameEventScriptBytecodeOpCode, _ a: GesValue, _ b: GesValue) -> GesValue {
        arithmetic(op, a, b, sink: GesValueFactory())
    }

    static func arithmetic<Output: GesValueOutput>(_ op: GameEventScriptBytecodeOpCode, _ a: GesValue, _ b: GesValue, sink: Output) -> Output.Result {
        if op != .power, let av = a.integerValue, let bv = b.integerValue { return integerArithmetic(op, av, bv, a.unit, b.unit, sink: sink) }
        if a.isNothing || b.isNothing { return sink.nothing }
        if op == .power {
            if b.hasUnit || a.asNumber.isNaN || b.asNumber.isNaN || (a.hasUnit && b.asNumber != 0 && b.asNumber != 1) { return sink.nothing }
            return sink.float(pow(a.asNumber, b.asNumber), unit: b.asNumber == 1 ? a.unit : .none)
        }
        let unit: GesUnit?
        if op == .multiply {
            unit = a.hasUnit && b.hasUnit ? nil : a.hasUnit ? a.unit : b.unit
        } else if op == .divide || op == .integerDivide {
            unit = !b.hasUnit ? a.unit : a.unit == b.unit ? GesUnit.none : nil
        } else {
            unit = a.unit == b.unit ? a.unit : nil
        }
        guard let unit else { return sink.nothing }
        if a.spatialValue != nil || b.spatialValue != nil {
            if op == .multiply {
                let vector = a.kind == .vector ? a : b
                let scalar = a.kind == .vector ? b : a
                guard vector.kind == .vector, scalar.asNumber.isFinite else { return sink.nothing }
                return sink.vector(x: vector.x * scalar.asNumber, y: vector.y * scalar.asNumber, z: vector.z * scalar.asNumber, unit: unit)
            }
            if op == .divide && a.kind == .vector && b.asNumber.isFinite && b.asNumber != 0 { return sink.vector(x: a.x / b.asNumber, y: a.y / b.asNumber, z: a.z / b.asNumber, unit: unit) }
            return sink.nothing
        }
        let x = a.asNumber
        let y = b.asNumber
        switch op {
        case .multiply: return a.kind == .percentage && b.kind == .percentage ? sink.percentage(x * y) : sink.float(x * y, unit: unit)
        case .divide: return a.kind == .percentage && b.kind != .percentage ? sink.percentage(x / y) : sink.float(x / y, unit: unit)
        case .integerDivide: return sink.float((x / y).rounded(.down), unit: unit)
        case .modulo, .remainder:
            if !x.isFinite || y.isNaN || y == 0 { return sink.nothing }
            var result = y.isInfinite ? x : x.truncatingRemainder(dividingBy: y)
            if op == .modulo && y.isFinite && result != 0 && (result < 0) != (y < 0) { result += y }
            return sink.float(result, unit: unit)
        default: return sink.nothing
        }
    }
}
