// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCallbacks {
    static func convert(_ value: GesValue, to name: String, _ c: GesContext) throws -> GesValue {
        let unit: GesQuantityUnit?
        switch name {
        case "Quantity(m)", "meter": unit = .meter
        case "Quantity(s)", "second": unit = .second
        case "Quantity(°)", "degree": unit = .degree
        default: unit = nil
        }
        if let unit { return GesCasts.unit(value, unit) }
        if name == "numeric" { return GesCasts.number(value) }
        let kind: GesBytecodeTypeKind
        switch name {
        case "Nothing": kind = .nothing
        case "Boolean": kind = .boolean
        case "Number": kind = .float
        case "Percentage": kind = .percentage
        case "Tag": kind = .tag
        case "Text": kind = .text
        case "Vector": kind = .vector
        case "Point": kind = .point
        case "Range": kind = .range
        case "Message": kind = .message
        case "Handler": kind = .handler
        case "List": kind = .list
        case "Map": kind = .map
        case "Dice": kind = .dice
        default: return .nothing
        }
        return try GesCasts.cast(value, kind, c)
    }
}
