// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesValueFactory: GesValueOutput {
    var nothing: GesValue { .nothing }

    func copy(_ value: GesValue) -> GesValue { value }

    func boolean(_ value: Bool) -> GesValue { .boolean(value) }

    func integer(_ value: Int64, unit: GesQuantityUnit) -> GesValue { .integer(value, unit: unit) }

    func float(_ value: Double, unit: GesQuantityUnit) -> GesValue { .float(value, unit: unit) }

    func percentage(_ value: Double) -> GesValue { .percentage(value) }

    func text(_ value: String) -> GesValue { .text(value) }

    func tag(_ value: String) throws -> GesValue { try .tag(value) }

    func vector(x: Double, y: Double, z: Double, unit: GesQuantityUnit) -> GesValue { .vector(x: x, y: y, z: z, unit: unit) }

    func point(x: Double, y: Double, z: Double, unit: GesQuantityUnit) -> GesValue { .point(x: x, y: y, z: z, unit: unit) }

    func dice(_ value: [Int32]) -> GesValue { .dice(value) }

    func list(_ value: [GesValue]) -> GesValue { .list(value) }

    func map(_ value: [GesMapEntry]) -> GesValue { .map(value) }

    func record(typeName: String, fields: GesValueMap) -> GesValue { .record(typeName: typeName, fields: fields) }

    func integerRange(from: Int64, to: Int64, step: Int64) -> GesValue { .integerRange(from: from, to: to, step: step) }

    func floatRange(from: Double, to: Double, step: Double) -> GesValue { .floatRange(from: from, to: to, step: step) }

    func message(_ value: GameEventScriptMessage) -> GesValue { .message(value) }

    func handler(_ value: GesMessageSignature) -> GesValue { .handler(value) }

    func series(_ value: GesSeriesValue) -> GesValue { .series(value) }
}
