// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCasts {
    static func check(_ value: GesValue, _ kind: GameEventScriptBytecodeTypeKind) -> Bool {
        switch kind {
        case .nothing: value.isNothing
        case .boolean: value.kind == .boolean
        case .integer: value.kind == .integer
        case .float: value.kind == .float
        case .percentage: value.kind == .percentage
        case .text: value.kind == .text
        case .tag: value.kind == .tag
        case .vector: value.kind == .vector
        case .point: value.kind == .point
        case .range: value.kind == .integerRange || value.kind == .floatRange
        case .handler: value.kind == .handler
        case .message: value.kind == .message
        case .list: value.kind == .list
        case .dice: value.kind == .dice
        case .map: value.kind == .map || value.kind == .record || value.kind == .external
        case .series: value.kind == .series
        default: false
        }
    }
}
