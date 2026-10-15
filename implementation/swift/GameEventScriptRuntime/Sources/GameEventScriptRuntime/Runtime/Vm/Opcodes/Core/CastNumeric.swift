// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCasts {
    static func number(_ value: GesValue) -> GesValue {
        number(value, sink: GesValueFactory())
    }

    static func number<Output: GesValueOutput>(_ value: GesValue, sink: Output) -> Output.Result {
        if value.kind == .integer { return sink.copy(value) }
        if value.kind == .text { return TextNumberCast.read(value.asText, sink: sink) ?? sink.nothing }
        if value.kind == .series { return GesSeriesValue.term(value, index: 0, sink: sink) }
        return sink.float(value.asNumber, unit: value.kind == .float ? value.unit : .none)
    }
}
