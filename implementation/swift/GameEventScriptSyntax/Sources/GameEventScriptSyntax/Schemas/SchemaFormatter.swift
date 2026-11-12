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
                if declaration.isAbstract { output += "abstract " }
                output += "schema :\(declaration.name) as {\n"
                for declared in declaration.lets { output += "  let \(declared.name) be \(value(declared.value))\n" }
                // Fields, computed fields and withs in their written order.
                var withIndex = 0
                var fieldIndex = 0
                var computedIndex = 0
                while true {
                    let with = withIndex < declaration.withs.count ? declaration.withs[withIndex].sequence : Int.max
                    let stored = fieldIndex < declaration.fields.count ? declaration.fields[fieldIndex].sequence : Int.max
                    let computed = computedIndex < declaration.computed.count ? declaration.computed[computedIndex].sequence : Int.max
                    if with == Int.max && stored == Int.max && computed == Int.max { break }
                    if with < stored && with < computed {
                        let entry = declaration.withs[withIndex]
                        withIndex += 1
                        output += "  with :\(entry.name)"
                        if entry.allOptional { output += " all optional" }
                        if !entry.without.isEmpty { output += " without (\(entry.without.joined(separator: ", ")))" }
                        output += "\n"
                        continue
                    }
                    output += "  "
                    if stored < computed {
                        field(declaration.fields[fieldIndex], &output, "  ")
                        fieldIndex += 1
                    } else {
                        field(declaration.computed[computedIndex], &output, "  ")
                        computedIndex += 1
                    }
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

    /// The source spelling of an expression on one line.
    @_spi(Syntax) public static func expression(_ expression: GesSchemaExpressionSyntax) -> String {
        var output = ""
        self.expression(expression, 0, &output)
        return output
    }

    /// Prints an expression with the parentheses its precedence needs: 0 choice, 1 default, 2 or, 3 and, 4 equality,
    /// 5 relation, 6 addition, 7 multiplication, 8 unary, 9 postfix and primary.
    private static func expression(_ node: GesSchemaExpressionSyntax, _ minimum: Int, _ output: inout String) {
        let level = level(node)
        if level < minimum { output += "(" }
        let children = node.children
        switch node.kind {
        case .literal: output += node.literal!.text
        case .boolean: output += node.boolean ? "true" : "false"
        case .constant: output += "$" + node.name
        case .name: output += node.name
        case .member:
            expression(children[0], 9, &output)
            output += "." + node.name
        case .negate:
            output += "-"
            expression(children[0], 8, &output)
        case .not, .abs:
            output += node.kind == .not ? "not " : "abs "
            expression(children[0], 8, &output)
        case .binary:
            expression(children[0], level, &output)
            output += " \(symbol(node.binary)) "
            expression(children[1], level + 1, &output)
        case .choice:
            for index in stride(from: 0, to: children.count - 1, by: 2) {
                expression(children[index], 1, &output)
                output += " when "
                expression(children[index + 1], 1, &output)
                output += ", "
            }
            output += "otherwise "
            expression(children[children.count - 1], 1, &output)
        case .min, .max:
            output += node.kind == .min ? "min of " : "max of "
            for (index, child) in children.enumerated() {
                if index > 0 { output += " and " }
                expression(child, 6, &output)
            }
        case .clamp:
            output += "clamp "
            expression(children[0], 6, &output)
            output += " between "
            expression(children[1], 6, &output)
            output += " and "
            expression(children[2], 6, &output)
        case .default:
            expression(children[0], 1, &output)
            output += " default "
            expression(children[1], 2, &output)
        case .hasValue:
            expression(children[0], 6, &output)
            output += " has value"
        case .count:
            expression(children[0], 9, &output)
            output += "[:count]"
        case .sum:
            expression(children[0], 9, &output)
            output += "[:sum"
            if children.count > 1 {
                output += " \(node.name) => "
                expression(children[1], 0, &output)
            }
            output += "]"
        }
        if level < minimum { output += ")" }
    }

    private static func level(_ node: GesSchemaExpressionSyntax) -> Int {
        switch node.kind {
        case .choice: return 0
        case .default: return 1
        case .hasValue: return 5
        case .negate, .not, .abs: return 8
        case .binary:
            switch node.binary {
            case .or: return 2
            case .and: return 3
            case .equal, .notEqual: return 4
            case .add, .subtract: return 6
            case .multiply, .divide: return 7
            default: return 5
            }
        default: return 9
        }
    }

    /// The spelling of a binary operator.
    @_spi(Syntax) public static func symbol(_ binary: GesSchemaBinaryOperator) -> String {
        switch binary {
        case .add: return "+"
        case .subtract: return "-"
        case .multiply: return "*"
        case .divide: return "/"
        case .less: return "<"
        case .lessOrEqual: return "<="
        case .greater: return ">"
        case .greaterOrEqual: return ">="
        case .equal: return "="
        case .notEqual: return "<>"
        case .and: return "and"
        case .or: return "or"
        }
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
        if let computed = field.computed {
            output += " computed by "
            expression(computed, 0, &output)
            return
        }
        if let fallback = field.defaultValue {
            output += " default "
            if let tag = fallback.tag {
                output += "#" + tag
            } else if let boolean = fallback.boolean {
                output += boolean ? "true" : "false"
            } else {
                output += value(fallback.value!)
            }
        }
    }

    private static func union(_ alternatives: [GesSchemaTypeSyntax], _ output: inout String, _ indent: String) {
        for (index, alternative) in alternatives.enumerated() {
            if index > 0 { output += " | " }
            if !alternative.label.isEmpty { output += alternative.label + ": " }
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
            if let max = type.max { output += " max \(max)" } else if !type.maxName.isEmpty { output += " max \(type.maxName)" }
            return
        case .flags:
            output += ":Flags"
            tags(type.tags ?? [], &output)
            return
        case .text, .bytes:
            output += type.kind == .text ? ":Text" : ":Bytes"
            if let max = type.max { output += " max \(max)" } else if !type.maxName.isEmpty { output += " max \(type.maxName)" }
            return
        case .list:
            output += ":List"
            if let element = type.element {
                output += "("
                union(element, &output, indent)
                output += ")"
            }
            if let max = type.max { output += " max \(max)" } else if !type.maxName.isEmpty { output += " max \(type.maxName)" }
            if type.depth > 0 { output += " depth \(type.depth)" }
            return
        case .dice:
            output += ":Dice"
            if let max = type.max { output += " max \(max)" } else if !type.maxName.isEmpty { output += " max \(type.maxName)" }
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
        !value.name.isEmpty ? value.name : !value.constant.isEmpty ? "$" + value.constant : value.text
    }

    private static func tags(_ tags: [String], _ output: inout String) {
        output += "(" + tags.map { "#" + $0 }.joined(separator: ", ") + ")"
    }
}
