// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Prints schema source in the recommended layout. Literals keep their spelling; comments and layout are dropped.
@_spi(Syntax) public enum GesSchemaFormatter {
    @_spi(Syntax) public static func print(_ source: GesSchemaSourceSyntax) -> String {
        var output = ""
        var previous: GesSchemaDeclarationKind?
        for declaration in source.declarations {
            let simple = declaration.kind == .constant || declaration.kind == .parameter || declaration.kind == .tagSet
            if let last = previous, !(simple && last == declaration.kind) { output += "\n" }
            previous = declaration.kind
            role("wire", declaration.wireGroup, &output)
            role("model", declaration.modelGroup, &output)
            switch declaration.kind {
            case .constant: output += "constant $\(declaration.name) be \(declaration.value!.text)\n"
            case .parameter:
                output += "parameter \(declaration.name): "
                type(declaration.type!, &output, "")
                output += "\n"
            case .schema:
                output += "schema :\(declaration.name) as {\n"
                for item in declaration.fields {
                    output += "  "
                    field(item, &output, "  ")
                    output += "\n"
                }
                output += "}\n"
            case .message:
                output += "message \(declaration.name)("
                for (index, item) in declaration.fields.enumerated() {
                    if index > 0 { output += ", " }
                    field(item, &output, "")
                }
                output += ")"
                if !declaration.tags.isEmpty {
                    output += " tags "
                    tags(declaration.tags, &output)
                }
                output += "\n"
            case .messages:
                output += "messages :\(declaration.name) as (\(declaration.members.joined(separator: ", ")))\n"
            case .tagSet:
                output += "tags :\(declaration.name) as "
                tags(declaration.tags, &output)
                output += "\n"
            }
        }
        return output
    }

    /// The group `default` is the one a role without a group names.
    private static func role(_ role: String, _ group: String?, _ output: inout String) {
        guard let group else { return }
        output += group == "default" ? "\(role) " : "\(role)(\(group)) "
    }

    private static func field(_ field: GesSchemaFieldSyntax, _ output: inout String, _ indent: String) {
        if field.unlabeled { output += "_ " }
        output += "\(field.name): "
        union(field.alternatives, &output, indent)
        if field.marker == .optional { output += " optional" } else if field.marker == .key { output += " key" }
    }

    private static func union(_ alternatives: [GesSchemaTypeSyntax], _ output: inout String, _ indent: String) {
        for (index, alternative) in alternatives.enumerated() {
            if index > 0 { output += " | " }
            type(alternative, &output, indent)
        }
    }

    private static func type(_ type: GesSchemaTypeSyntax, _ output: inout String, _ indent: String) {
        switch type.kind {
        case .quantity: output += ":Quantity(\(type.unit.suffix))"
        case .point, .vector:
            output += type.kind == .point ? ":Point" : ":Vector"
            if type.unit != .none { output += "(\(type.unit.suffix))" }
            if let axes = type.axes {
                output += " {\n"
                for axis in axes {
                    output += "\(indent)  \(axis.axis)"
                    constraints(axis.constraints, &output)
                    output += "\n"
                }
                output += "\(indent)}"
            }
            return
        case .tag:
            output += ":Tag"
            if let set = type.tags { tags(set, &output) }
            if let max = type.max { output += " max \(max)" }
            return
        case .flags:
            output += ":Flags"
            tags(type.tags ?? [], &output)
            return
        case .text, .bytes:
            output += type.kind == .text ? ":Text" : ":Bytes"
            if let max = type.max { output += " max \(max)" }
            return
        case .list:
            output += ":List"
            if let element = type.element {
                output += "("
                union(element, &output, indent)
                output += ")"
            }
            if let max = type.max { output += " max \(max)" }
            return
        case .dice:
            output += ":Dice"
            if let max = type.max { output += " max \(max)" }
        case .reference:
            output += ":\(type.name)"
            return
        default: output += ":\(type.kind.rawValue)"
        }
        constraints(type.constraints, &output)
    }

    private static func constraints(_ constraints: GesSchemaConstraintsSyntax, _ output: inout String) {
        if constraints.integer { output += " integer" }
        if let lower = constraints.lower {
            output += constraints.wrapping ? " wrapping between " : " between "
            output += "\(value(lower)) and \(value(constraints.upper!))"
        }
        if let step = constraints.step { output += " step \(value(step))" }
        if let delta = constraints.delta { output += " delta within \(value(delta))" }
    }

    private static func value(_ value: GesSchemaValueSyntax) -> String {
        !value.parameter.isEmpty ? value.parameter : !value.constant.isEmpty ? "$" + value.constant : value.text
    }

    private static func tags(_ tags: [String], _ output: inout String) {
        output += "(" + tags.map { "#" + $0 }.joined(separator: ", ") + ")"
    }
}
