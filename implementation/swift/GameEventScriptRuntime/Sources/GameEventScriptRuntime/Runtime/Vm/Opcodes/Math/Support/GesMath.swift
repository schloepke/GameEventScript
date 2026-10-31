// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

enum GesMath {
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
            if b.kind == .map {
                keys = b.asMap!.keyStorage
            } else if let key = b.textValue {
                keys = [key]
            } else if let list = b.listValue, list.allSatisfy({ $0.textValue != nil }) {
                keys = GesValueMap.sortedKeys(list.map(\.asText))
            } else {
                return sink.nothing
            }
            return sink.map(a.asMap!.selecting(sortedKeys: keys, keep: false))
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

    static func validFace(_ value: GesValue) -> Bool { value.kind == .integer && !value.hasUnit && value.asInteger > 0 && value.asInteger <= Int32.max }

    static func removing(_ values: [GesValue], _ remove: [GesValue]) -> [GesValue] {
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
        case integer(Int64, unit: GesQuantityUnit)
        case float(Double, unit: GesQuantityUnit)

    }

    static func integerArithmetic<Output: GesValueOutput>(_ op: GesBytecodeOpCode, _ a: Int64, _ b: Int64, _ leftUnit: GesQuantityUnit, _ rightUnit: GesQuantityUnit, sink: Output) -> Output.Result {
        switch integerArithmeticResult(op, a, b, leftUnit, rightUnit) {
        case .nothing: return sink.nothing
        case .integer(let value, let unit): return sink.integer(value, unit: unit)
        case .float(let value, let unit): return sink.float(value, unit: unit)
        }
    }

    static func integerArithmeticResult(_ op: GesBytecodeOpCode, _ a: Int64, _ b: Int64, _ leftUnit: GesQuantityUnit, _ rightUnit: GesQuantityUnit) -> IntegerResult {
        switch op {
        case .add, .subtract:
            guard leftUnit == rightUnit else { return .nothing }
            let (result, overflow) = op == .subtract ? a.subtractingReportingOverflow(b) : a.addingReportingOverflow(b)
            return overflow ? .float(op == .subtract ? Double(a) - Double(b) : Double(a) + Double(b), unit: leftUnit) : .integer(result, unit: leftUnit)
        case .multiply:
            guard let unit = GesQuantityUnit.product(leftUnit, rightUnit) else { return .nothing }
            let (result, overflow) = a.multipliedReportingOverflow(by: b)
            return overflow ? .float(Double(a) * Double(b), unit: unit) : .integer(result, unit: unit)
        case .divide, .integerDivide:
            // Division follows the derivation table; integer division keeps the derivation-free rules.
            let derived = op == .divide ? GesQuantityUnit.quotient(leftUnit, rightUnit) : rightUnit == .none ? leftUnit : leftUnit == rightUnit ? GesQuantityUnit.none : nil
            guard let unit = derived else { return .nothing }
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

    static func arithmetic(_ op: GesBytecodeOpCode, _ a: GesValue, _ b: GesValue) -> GesValue {
        arithmetic(op, a, b, sink: GesValueFactory())
    }

    static func arithmetic<Output: GesValueOutput>(_ op: GesBytecodeOpCode, _ a: GesValue, _ b: GesValue, sink: Output) -> Output.Result {
        if op != .power, let av = a.integerValue, let bv = b.integerValue { return integerArithmetic(op, av, bv, a.unit, b.unit, sink: sink) }
        if a.isNothing || b.isNothing { return sink.nothing }
        if op == .power {
            if b.hasUnit || a.asNumber.isNaN || b.asNumber.isNaN || (a.hasUnit && b.asNumber != 0 && b.asNumber != 1) { return sink.nothing }
            return sink.float(pow(a.asNumber, b.asNumber), unit: b.asNumber == 1 ? a.unit : .none)
        }
        let unit: GesQuantityUnit?
        if op == .multiply {
            unit = GesQuantityUnit.product(a.unit, b.unit)
        } else if op == .divide {
            unit = a.kind == .percentage && b.hasUnit ? nil : GesQuantityUnit.quotient(a.unit, b.unit)
        } else if op == .integerDivide {
            unit = !b.hasUnit ? a.unit : a.unit == b.unit ? GesQuantityUnit.none : nil
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
