// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCasts {
    static func unit(_ value: GesValue, _ unit: GesQuantityUnit) -> GesValue {
        Self.unit(value, unit, sink: GesValueFactory())
    }

    static func unit<Output: GesValueOutput>(_ value: GesValue, _ unit: GesQuantityUnit, sink: Output) -> Output.Result {
        guard unit == .none || value.unit == .none || value.unit == unit else { return sink.nothing }
        switch value.kind {
        case .integer: return sink.integer(value.asInteger, unit: unit)
        case .float: return value.asNumber.isFinite ? sink.float(value.asNumber, unit: unit) : sink.nothing
        case .vector: return sink.vector(x: value.x, y: value.y, z: value.z, unit: unit)
        case .point: return sink.point(x: value.x, y: value.y, z: value.z, unit: unit)
        default: return sink.nothing
        }
    }
}
