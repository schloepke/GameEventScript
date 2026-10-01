// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Shared semantics specialize either to a value factory (constant/literal evaluation)
// or to owner-local register writes with a Void result. No closure or existential
// is stored; computed register results do not cross helper boundaries as GesValue.
protocol GesValueOutput {
    associatedtype Result
    var nothing: Result { get }

    func copy(_ value: GesValue) -> Result

    func boolean(_ value: Bool) -> Result

    func integer(_ value: Int64, unit: GesUnit) -> Result

    func float(_ value: Double, unit: GesUnit) -> Result

    func percentage(_ value: Double) -> Result

    func text(_ value: String) -> Result

    func tag(_ value: String) throws -> Result

    func vector(x: Double, y: Double, z: Double, unit: GesUnit) -> Result

    func point(x: Double, y: Double, z: Double, unit: GesUnit) -> Result

    func dice(_ value: [Int32]) -> Result

    func list(_ value: [GesValue]) -> Result

    func map(_ value: [GesMapEntry]) -> Result

    func record(typeName: String, entries: [GesMapEntry]) -> Result

    func integerRange(from: Int64, to: Int64, step: Int64) -> Result

    func floatRange(from: Double, to: Double, step: Double) -> Result

    func message(_ value: GameEventScriptMessage) -> Result

    func handler(_ value: GameEventScriptMessageSignature) -> Result

    func series(_ value: GesSeriesValue) -> Result
}

extension GesValueOutput {
    func integer(_ value: Int64) -> Result { integer(value, unit: .none) }

    func float(_ value: Double) -> Result { float(value, unit: .none) }

    func vector(x: Double, y: Double, z: Double) -> Result { vector(x: x, y: y, z: z, unit: .none) }

    func point(x: Double, y: Double, z: Double) -> Result { point(x: x, y: y, z: z, unit: .none) }
}

struct GesValueFactory: GesValueOutput {
    var nothing: GesValue { .nothing }

    func copy(_ value: GesValue) -> GesValue { value }

    func boolean(_ value: Bool) -> GesValue { .boolean(value) }

    func integer(_ value: Int64, unit: GesUnit) -> GesValue { .integer(value, unit: unit) }

    func float(_ value: Double, unit: GesUnit) -> GesValue { .float(value, unit: unit) }

    func percentage(_ value: Double) -> GesValue { .percentage(value) }

    func text(_ value: String) -> GesValue { .text(value) }

    func tag(_ value: String) throws -> GesValue { try .tag(value) }

    func vector(x: Double, y: Double, z: Double, unit: GesUnit) -> GesValue { .vector(x: x, y: y, z: z, unit: unit) }

    func point(x: Double, y: Double, z: Double, unit: GesUnit) -> GesValue { .point(x: x, y: y, z: z, unit: unit) }

    func dice(_ value: [Int32]) -> GesValue { .dice(value) }

    func list(_ value: [GesValue]) -> GesValue { .list(value) }

    func map(_ value: [GesMapEntry]) -> GesValue { .map(value) }

    func record(typeName: String, entries: [GesMapEntry]) -> GesValue { .record(typeName: typeName, entries: entries) }

    func integerRange(from: Int64, to: Int64, step: Int64) -> GesValue { .integerRange(from: from, to: to, step: step) }

    func floatRange(from: Double, to: Double, step: Double) -> GesValue { .floatRange(from: from, to: to, step: step) }

    func message(_ value: GameEventScriptMessage) -> GesValue { .message(value) }

    func handler(_ value: GameEventScriptMessageSignature) -> GesValue { .handler(value) }

    func series(_ value: GesSeriesValue) -> GesValue { .series(value) }
}

struct GesRegisterOutput: GesValueOutput {
    let state: GesVmState
    let index: Int
    var nothing: Void { state.setNothing(index) }

    func copy(_ value: GesValue) { state.setValue(index, value) }

    func boolean(_ value: Bool) { state.setBoolean(index, value) }

    func integer(_ value: Int64, unit: GesUnit) { state.setInteger(index, value, unit: unit) }

    func float(_ value: Double, unit: GesUnit) { state.setFloat(index, value, unit: unit) }

    func percentage(_ value: Double) { state.setPercentage(index, value) }

    func text(_ value: String) { state.setText(index, value) }

    func tag(_ value: String) throws { try state.setTag(index, value) }

    func vector(x: Double, y: Double, z: Double, unit: GesUnit) { state.setVector(index, x: x, y: y, z: z, unit: unit) }

    func point(x: Double, y: Double, z: Double, unit: GesUnit) { state.setPoint(index, x: x, y: y, z: z, unit: unit) }

    func dice(_ value: [Int32]) { state.setDice(index, value) }

    func list(_ value: [GesValue]) { state.setList(index, value) }

    func map(_ value: [GesMapEntry]) { state.setMap(index, value) }

    func record(typeName: String, entries: [GesMapEntry]) { state.setRecord(index, typeName: typeName, entries: entries) }

    func integerRange(from: Int64, to: Int64, step: Int64) { state.setIntegerRange(index, from: from, to: to, step: step) }

    func floatRange(from: Double, to: Double, step: Double) { state.setFloatRange(index, from: from, to: to, step: step) }

    func message(_ value: GameEventScriptMessage) { state.setMessage(index, value) }

    func handler(_ value: GameEventScriptMessageSignature) { state.setHandler(index, value) }

    func series(_ value: GesSeriesValue) { state.setSeries(index, value) }
}
