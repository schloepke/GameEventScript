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

    func record(typeName: String, fields: GesValueMap) -> Result

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
