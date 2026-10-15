// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesExternalTypeNames {
    static func identifier(_ text: String) throws -> String {
        let text = GesMessageNames.trim(text)
        guard GesNames.plain(text) else { throw GesApiError.invalidArgument("Invalid external field or extension name") }
        return text
    }

    static func type(_ text: String) throws -> String {
        var text = GesMessageNames.trim(text)
        if text.hasPrefix(":") { text.removeFirst() }
        guard GesNames.type(text) else { throw GesApiError.invalidArgument("Invalid external type name") }
        return text
    }
}

extension GesExternalTypeNames {
    static func kind(_ name: String) -> GesBytecodeTypeKind? {
        switch name {
        case "Nothing": .nothing
        case "Boolean": .boolean
        case "Number": .float
        case "Percentage": .percentage
        case "Tag": .tag
        case "Text": .text
        case "Vector": .vector
        case "Point": .point
        case "Series": .series
        case "Range": .range
        case "Message": .message
        case "Handler": .handler
        case "List": .list
        case "Map": .map
        case "Dice": .dice
        default: nil
        }
    }

    static func typeName(_ kind: GesBytecodeTypeKind, _ unit: GesQuantityUnit) throws -> String {
        guard [.nothing, .tag, .text, .percentage, .vector, .point, .float, .boolean, .series, .range, .handler, .list, .map, .dice].contains(kind), unit == .none || kind == .float || kind == .vector || kind == .point else {
            throw GesApiError.invalidArgument("Unsupported external value kind or unit")
        }
        if (kind == .float || kind == .integer) && unit != .none { return "Quantity(" + unit.suffix + ")" }
        switch kind {
        case .float, .integer: return "Number"
        default:
            let name = String(describing: kind)
            return name.prefix(1).uppercased() + name.dropFirst()
        }
    }

    static func coerce(_ value: GesValue, kind: GesBytecodeTypeKind?, unit: GesQuantityUnit) -> GesValue {
        switch kind {
        case .float: return .float(value.asNumber, unit: unit)
        case .vector where value.spatialValue != nil: return .vector(x: value.x, y: value.y, z: value.z, unit: unit == .none ? value.unit : unit)
        case .point where value.spatialValue != nil: return .point(x: value.x, y: value.y, z: value.z, unit: unit == .none ? value.unit : unit)
        case .tag:
            if value.kind == .boolean { return try! .tag(value.asBoolean ? "true" : "false") }
            var name = value.toText
            if value.kind == .text {
                if name == "True" { name = "true" }
                if name == "False" { name = "false" }
            }
            return (try? .tag(name)) ?? .nothing
        case .text: return .text(value.toText)
        case .boolean: return .boolean(value.asBoolean)
        case .percentage: return .percentage(value.asNumber)
        case .nothing: return .nothing
        default: return value
        }
    }
}
