// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCallbacks {
    static func convert(_ value: GesValue, to name: String, _ c: GesContext) throws -> GesValue {
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
        default:
            // Unit names are checked only after the built-in type names. `Quantity(...)` takes any quantity type name;
            // a bare name is a portable unit type name such as `meter`.
            let unit: GesQuantityUnit?
            if name.hasPrefix("Quantity("), name.hasSuffix(")") {
                unit = GesQuantityUnit(quantityName: String(name.dropFirst(9).dropLast()))
            } else {
                unit = name == "°" ? nil : GesQuantityUnit(typeName: name)
            }
            guard let unit else { return .nothing }
            return GesCasts.unit(value, unit)
        }
        return try GesCasts.cast(value, kind, c)
    }
}
