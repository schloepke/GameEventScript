// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesSeriesValue {
    static func term(_ value: GesValue, index: Int64) -> GesValue {
        term(value, index: index, sink: GesValueFactory())
    }

    static func term<Output: GesValueOutput>(_ value: GesValue, index: Int64, sink: Output) -> Output.Result {
        guard let series = value.seriesValue, index >= 0 else { return sink.nothing }
        let (index, overflow) = index.addingReportingOverflow(series.offset)
        if overflow || index < 0 { return sink.nothing }
        switch series.signatureID {
        case "fibonacci":
            if index > 1476 { return sink.float(.infinity) }
            var a = 0.0
            var b = 1.0
            for _ in 0..<index {
                let next = a + b
                a = b
                b = next
            }
            return sink.float(a)
        case "factorial":
            if index > 170 { return sink.float(.infinity) }
            var result = 1.0
            if index > 1 { for factor in 2...index { result *= Double(factor) } }
            return sink.float(result)
        default: return sink.nothing
        }
    }
}
