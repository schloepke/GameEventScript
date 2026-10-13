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
    static func numericOperand(_ value: GesValue) -> GesValue {
        if value.kind == .boolean { return .integer(value.truth == true ? 1 : 0) }
        if value.kind == .dice { return .integer(value.numericIntegerValue!) }
        return value
    }

    static func execute<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, sink: Output) throws -> Output.Result {
        var a = s.value(Int(i.word1))
        // Read the second register only for instructions which actually have one.
        var b: GesValue
        switch i.opcode {
        case .or, .and, .implies, .xor, .equal, .notEqual, .less, .greater, .lessOrEqual, .greaterOrEqual, .add, .subtract, .multiply, .divide, .integerDivide, .modulo, .remainder, .power, .min, .max, .clamp, .randomTake, .randomTakeFloat, .term:
            b = s.value(Int(i.word2))
        default: b = .nothing
        }
        if i.unitAndFlags & 0x40 != 0 {
            switch i.opcode {
            case .add, .subtract, .multiply, .divide, .power, .integerDivide, .modulo, .remainder, .min, .max, .clamp:
                guard a.isNumeric, b.isNumeric else { return sink.nothing }
                if a.kind == .boolean || a.kind == .dice { a = numericOperand(a) }
                if b.kind == .boolean || b.kind == .dice { b = numericOperand(b) }
            case .negate, .abs:
                guard a.isNumeric else { return sink.nothing }
                if a.kind == .boolean || a.kind == .dice { a = numericOperand(a) }
            default: break
            }
        }
        switch i.opcode {
        case .or: return try executeOr(i, s, c, a, b, sink: sink)
        case .and: return try executeAnd(i, s, c, a, b, sink: sink)
        case .implies: return try executeImplies(i, s, c, a, b, sink: sink)
        case .xor: return try executeXor(i, s, c, a, b, sink: sink)
        case .not: return try executeNot(i, s, c, a, b, sink: sink)
        case .equal: return try executeEqual(i, s, c, a, b, sink: sink)
        case .notEqual: return try executeNotEqual(i, s, c, a, b, sink: sink)
        case .less: return try executeLess(i, s, c, a, b, sink: sink)
        case .greater: return try executeGreater(i, s, c, a, b, sink: sink)
        case .lessOrEqual: return try executeLessOrEqual(i, s, c, a, b, sink: sink)
        case .greaterOrEqual: return try executeGreaterOrEqual(i, s, c, a, b, sink: sink)
        case .add: return try executeAdd(i, s, c, a, b, sink: sink)
        case .subtract: return try executeSubtract(i, s, c, a, b, sink: sink)
        case .multiply: return try executeMultiply(i, s, c, a, b, sink: sink)
        case .divide: return try executeDivide(i, s, c, a, b, sink: sink)
        case .integerDivide: return try executeIntegerDivide(i, s, c, a, b, sink: sink)
        case .modulo: return try executeModulo(i, s, c, a, b, sink: sink)
        case .remainder: return try executeRemainder(i, s, c, a, b, sink: sink)
        case .power: return try executePower(i, s, c, a, b, sink: sink)
        case .min: return try executeMin(i, s, c, a, b, sink: sink)
        case .max: return try executeMax(i, s, c, a, b, sink: sink)
        case .negate: return try executeNegate(i, s, c, a, b, sink: sink)
        case .abs: return try executeAbs(i, s, c, a, b, sink: sink)
        case .clamp: return try executeClamp(i, s, c, a, b, sink: sink)
        case .logN: return try executeLogN(i, s, c, a, b, sink: sink)
        case .exp: return try executeExp(i, s, c, a, b, sink: sink)
        case .floor: return try executeFloor(i, s, c, a, b, sink: sink)
        case .ceil: return try executeCeil(i, s, c, a, b, sink: sink)
        case .truncate: return try executeTruncate(i, s, c, a, b, sink: sink)
        case .roundHalfEven: return try executeRoundHalfEven(i, s, c, a, b, sink: sink)
        case .roundHalfUp: return try executeRoundHalfUp(i, s, c, a, b, sink: sink)
        case .roundHalfDown: return try executeRoundHalfDown(i, s, c, a, b, sink: sink)
        case .degreeToRadians: return try executeDegreeToRadians(i, s, c, a, b, sink: sink)
        case .degreeFromRadians: return try executeDegreeFromRadians(i, s, c, a, b, sink: sink)
        case .wrapDegree: return try executeWrapDegree(i, s, c, a, b, sink: sink)
        case .chance: return try executeChance(i, s, c, a, b, sink: sink)
        case .randomTake: return try executeRandomTake(i, s, c, a, b, sink: sink)
        case .randomTakeFloat: return try executeRandomTakeFloat(i, s, c, a, b, sink: sink)
        case .term: return try executeTerm(i, s, c, a, b, sink: sink)
        case .sin: return GesNavigation.executeSin(i, s, sink: sink)
        case .cos: return GesNavigation.executeCos(i, s, sink: sink)
        case .tan: return GesNavigation.executeTan(i, s, sink: sink)
        case .asin: return GesNavigation.executeAsin(i, s, sink: sink)
        case .acos: return GesNavigation.executeAcos(i, s, sink: sink)
        case .atan: return GesNavigation.executeAtan(i, s, sink: sink)
        case .atan2: return GesNavigation.executeAtan2(i, s, sink: sink)
        case .lengthSquared: return GesNavigation.executeLengthSquared(i, s, sink: sink)
        case .normalize: return GesNavigation.executeNormalize(i, s, sink: sink)
        case .distance: return GesNavigation.executeDistance(i, s, sink: sink)
        case .distanceSquared: return GesNavigation.executeDistanceSquared(i, s, sink: sink)
        case .dot: return GesNavigation.executeDot(i, s, sink: sink)
        case .cross: return GesNavigation.executeCross(i, s, sink: sink)
        case .angleBetween: return GesNavigation.executeAngleBetween(i, s, sink: sink)
        case .hypot2D: return GesNavigation.executeHypot2D(i, s, sink: sink)
        case .hypot3D: return GesNavigation.executeHypot3D(i, s, sink: sink)
        case .lengthSquared2D: return GesNavigation.executeLengthSquared2D(i, s, sink: sink)
        case .lengthSquared3D: return GesNavigation.executeLengthSquared3D(i, s, sink: sink)
        case .normalize2D: return GesNavigation.executeNormalize2D(i, s, sink: sink)
        case .normalize3D: return GesNavigation.executeNormalize3D(i, s, sink: sink)
        case .distance2D: return GesNavigation.executeDistance2D(i, s, sink: sink)
        case .distance3D: return GesNavigation.executeDistance3D(i, s, sink: sink)
        case .distanceSquared2D: return GesNavigation.executeDistanceSquared2D(i, s, sink: sink)
        case .distanceSquared3D: return GesNavigation.executeDistanceSquared3D(i, s, sink: sink)
        case .dot2D: return GesNavigation.executeDot2D(i, s, sink: sink)
        case .dot3D: return GesNavigation.executeDot3D(i, s, sink: sink)
        case .cross2D: return GesNavigation.executeCross2D(i, s, sink: sink)
        case .cross3D: return GesNavigation.executeCross3D(i, s, sink: sink)
        case .angleBetween2D: return GesNavigation.executeAngleBetween2D(i, s, sink: sink)
        case .angleBetween3D: return GesNavigation.executeAngleBetween3D(i, s, sink: sink)
        default: return GesNavigation.evaluate(i.opcode, i, s, sink: sink)
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
