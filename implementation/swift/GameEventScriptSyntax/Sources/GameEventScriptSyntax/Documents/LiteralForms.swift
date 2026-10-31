// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The explicit data forms such as `:Range(from: 1, to: 5)`: their lowercase type names and parameters.
@_spi(Syntax) public enum GesLiteralForms {
    @_spi(Syntax) public static let types = ["nothing", "number", "percentage", "boolean", "text", "tag", "list", "map", "dice", "vector", "point", "range", "series", "message", "handler", "record"]

    @_spi(Syntax) public static func positions(_ type: String, _ labels: [String]) -> [Int]? {
        let parameters: [String]
        switch type {
        case "number": parameters = ["value", "unit"]
        case "range": parameters = ["from", "to", "step"]
        case "series": parameters = ["kind", "offset"]
        case "record": parameters = ["type", "fields"]
        case "message": parameters = ["value", "tags"]
        case "vector", "point": parameters = ["x", "y", "z"]
        default: parameters = ["value"]
        }
        if labels.count > parameters.count || labels.isEmpty && !["nothing", "vector", "point"].contains(type) { return nil }
        var positions: [Int] = []
        for (i, label) in labels.enumerated() {
            guard let position = label == "_" ? i : parameters.firstIndex(of: label), !positions.contains(position) else { return nil }
            positions.append(position)
        }
        if (type == "record" || type == "range" && labels.count > 1) && (!positions.contains(0) || !positions.contains(1)) { return nil }
        if !["nothing", "vector", "point"].contains(type) && !positions.contains(0) { return nil }
        return positions
    }

    /// A built-in form name is the capitalized spelling of its type, such as `Range`.
    @_spi(Syntax) public static func isBuiltinName(_ name: String) -> Bool {
        let type = name.lowercased()
        return types.contains(type) && name == type.prefix(1).uppercased() + type.dropFirst()
    }

    /// A record type name of a document schema: a type name that names no built-in type.
    @_spi(Syntax) public static func isRecordTypeName(_ name: String) -> Bool { GesNames.type(name) && !isBuiltinName(name) }
}
