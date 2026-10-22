// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesDataConstructionShape {
    static let types = ["nothing", "number", "percentage", "boolean", "text", "tag", "list", "map", "dice", "vector", "point", "range", "series", "message", "handler", "record"]

    static func positions(_ type: String, _ labels: [String]) -> [Int]? {
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
}
