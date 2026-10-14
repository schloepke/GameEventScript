// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesRegisterOutput: GesValueOutput {
    let state: GesVirtualMachine
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

    func record(typeName: String, fields: GesValueMap) { state.setRecord(index, typeName: typeName, fields: fields) }

    func integerRange(from: Int64, to: Int64, step: Int64) { state.setIntegerRange(index, from: from, to: to, step: step) }

    func floatRange(from: Double, to: Double, step: Double) { state.setFloatRange(index, from: from, to: to, step: step) }

    func message(_ value: GameEventScriptMessage) { state.setMessage(index, value) }

    func handler(_ value: GameEventScriptMessageSignature) { state.setHandler(index, value) }

    func series(_ value: GesSeriesValue) { state.setSeries(index, value) }
}
