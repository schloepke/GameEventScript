// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func performNegateAbs<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        if let integer = a.integerValue { return integer == .min ? sink.float(-Double(integer), unit: a.unit) : sink.integer(i.opcode == .abs ? Swift.abs(integer) : -integer, unit: a.unit) }
        if a.kind == .point { return sink.nothing }
        if a.kind == .vector { return i.opcode == .abs ? sink.float((a.x * a.x + a.y * a.y + a.z * a.z).squareRoot(), unit: a.unit) : sink.vector(x: -a.x, y: -a.y, z: -a.z, unit: a.unit) }
        let number = i.opcode == .abs ? Swift.abs(a.asNumber) : -a.asNumber
        return a.kind == .percentage ? sink.percentage(number) : sink.float(number, unit: a.unit)
    }
}
