// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesSelectionOrder {
    static func compare(_ a: GesValue, _ b: GesValue) -> Int {
        func unit(_ value: GesQuantityUnit) -> Int {
            switch value {
            case .none: 0
            case .degree: 1
            case .meter: 2
            case .second: 3
            }
        }

        func rank(_ value: GesValue) -> Int {
            if value.isNumeric { return 1 }
            switch value.kind {
            case .nothing: return 0
            case .tag: return 1
            case .text: return 2
            case .vector: return 4
            case .point: return 5
            case .series: return 7
            case .message: return 9
            case .handler: return 10
            case .list: return 11
            case .map, .record, .external: return 12
            default: return 8
            }
        }

        if a.isNumeric && b.isNumeric {
            if a.unit != b.unit { return unit(a.unit) < unit(b.unit) ? -1 : 1 }
            return a.asNumber < b.asNumber ? -1 : a.asNumber > b.asNumber ? 1 : 0
        }
        if rank(a) != rank(b) { return rank(a) < rank(b) ? -1 : 1 }

        // Selection has its own same-rank kind order; sort's heterogeneous
        // ranks would reverse Boolean/Tag and Map/Record pairs here.
        func tieRank(_ value: GesValue) -> Int {
            switch value.kind {
            case .tag, .record, .external: 1
            case .dice: 2
            default: 0
            }
        }

        if tieRank(a) != tieRank(b) { return tieRank(a) < tieRank(b) ? -1 : 1 }
        if a.spatialValue != nil && a.kind == b.kind && a.unit != b.unit { return unit(a.unit) < unit(b.unit) ? -1 : 1 }
        return GesComparison.order(a, b) ?? 0
    }
}
