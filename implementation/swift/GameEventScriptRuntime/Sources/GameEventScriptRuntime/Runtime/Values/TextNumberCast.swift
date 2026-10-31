// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// Number text as GES values; the grammar is `GesNumberLiteral` in Syntax.
enum GesTextNumberCast {
    static func read(_ input: String, percentage: Bool = false, allowGrouping: Bool = true, percentageMagnitude: Bool = false) -> GesValue? {
        read(input, percentage: percentage, allowGrouping: allowGrouping, percentageMagnitude: percentageMagnitude, sink: GesValueFactory())
    }

    static func read<Output: GesValueOutput>(_ input: String, percentage: Bool = false, allowGrouping: Bool = true, percentageMagnitude: Bool = false, sink: Output) -> Output.Result? {
        value(GesNumberLiteral.read(input, percentage: percentage, allowGrouping: allowGrouping, percentageMagnitude: percentageMagnitude), sink: sink)
    }

    static func value<Output: GesValueOutput>(_ number: GesNumberLiteral, sink: Output) -> Output.Result? {
        switch number.kind {
        case .integer: return sink.integer(number.integerValue, unit: number.unit)
        case .float: return sink.float(number.floatValue, unit: number.unit)
        case .percentage: return sink.percentage(number.floatValue)
        case .nothing: return sink.nothing
        case .invalid: return nil
        }
    }

    static func percentage(_ value: GesValue) -> GesValue {
        percentage(value, sink: GesValueFactory())
    }

    static func percentage<Output: GesValueOutput>(_ value: GesValue, sink: Output) -> Output.Result {
        if value.kind == .text { return read(value.textValue!, percentage: true, sink: sink) ?? sink.nothing }
        if value.kind == .percentage { return sink.copy(value) }
        return !value.hasUnit && value.asNumber.isFinite ? sink.percentage(value.asNumber) : sink.nothing
    }
}
