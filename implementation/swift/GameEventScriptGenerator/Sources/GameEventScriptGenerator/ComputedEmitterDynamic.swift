// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

/// The dynamic form of a computed field: the exact GES value through the helpers that repeat the virtual machine, for the
/// few fields whose operands binding cannot prove to give the same bits in plain binary64, such as a chain of whole numbers
/// beyond 2^53, which the virtual machine adds as exact Int64 values.
extension GesComputedEmitter {
    func dynamicMembers(_ code: inout Code, _ owner: String, _ field: GesSchemaComputedFieldInfo, _ access: String) {
        let root = swift ? "self" : "this"
        let expression = cast(self.expression(field.tree, root), field)
        let property = Self.property(field.name, owner, swift: swift)
        let method = Self.method(field.name, owner, swift: swift)
        let result = field.kind == .boolean ? "IsTrue" : field.isOptional ? "OptionalNumber" : "Number"
        summary(&code, field)
        if swift {
            code.line("\(access.isEmpty ? "" : access + " ")var \(member(property)): \(Self.propertyType(field, swift: true)) { \(helper).\(camel(result))(\(method)()) }")
            code.blank()
            code.line("/// The exact GES value of `\(field.name)`, which other computed fields read.")
            code.line("func \(method)() -> \(helper).Value { \(expression) }")
        } else {
            code.line("\(access) \(Self.propertyType(field, swift: false)) \(property) => \(helper).\(result)(\(method)());")
            code.blank()
            code.line("/// <summary>The exact GES value of <c>\(Self.escape(field.name))</c>, which other computed fields read.</summary>")
            code.line("internal \(helper).Value \(method)() => \(expression);")
        }
    }

    func flag(_ value: Bool) -> String { value ? "true" : "false" }

    /// A script record converts the value to the field's type: a Percentage takes a finite number, a Boolean only true.
    func cast(_ expression: String, _ field: GesSchemaComputedFieldInfo) -> String {
        switch field.kind {
        case .percentage: call("CastPercentage", expression)
        case .boolean: call("CastBoolean", expression)
        default: expression
        }
    }

    /// The exact value at a path, nothing where an optional field on the way is absent.
    func read(_ root: String, _ owner: String, _ path: [String], percentage: Bool) -> String {
        let reach = access.reach(root, owner, path)
        let read: String
        if !reach.computedName.isEmpty {
            let target = target(reach)
            read =
                analyze(reach.computedOwner, target) == nil
                ? Self.join(reach.text, Self.method(target.name, reach.computedOwner, swift: swift)) + "()"
                : call(target.kind == .percentage ? "ReadPercentage" : "Read", Self.join(reach.text, member(Self.property(target.name, reach.computedOwner, swift: swift))))
        } else {
            read = call(percentage ? "ReadPercentage" : "Read", reach.text)
        }
        if reach.conditions.isEmpty { return read }
        return "(\(reach.conditions.map(\.condition).joined(separator: " && ")) ? \(read) : \(helper).\(camel("Nothing")))"
    }

    /// Whether the record at a path is present.
    func present(_ root: String, _ owner: String, _ path: [String]) -> String {
        let reach = access.reach(root, owner, path)
        if reach.nullable { return reach.text + (swift ? " != nil" : " != null") }
        if reach.conditions.isEmpty { return "true" }
        return reach.conditions.map(\.condition).joined(separator: " && ")
    }

    private static func opName(_ op: GesComputedOp) -> String { GesSchemaPlanner.pascal(op.name) }

    func expression(_ node: GesComputedNode, _ root: String) -> String {
        let children = node.children
        switch node.op {
        case .literal:
            switch node.literal.kind {
            case .true, .false: return call("Boolean", flag(node.literal.kind == .true))
            case .integer: return call("Integer", node.literal.integer == Int64.min ? (swift ? "Int64.min" : "long.MinValue") : String(node.literal.integer))
            case .percentage: return call("Percentage", node.decimal.canonical)
            case .float where node.type == .percentage: return call("Percentage", node.decimal.canonical)
            default: return call("Float", node.decimal.canonical)
            }
        case .field, .item:
            return read(self.root(node, root), owner(node), node.path, percentage: node.type == .percentage)
        case .negate: return call("Negate", expression(children[0], root))
        case .not: return call("Not", expression(children[0], root))
        case .abs: return call("Abs", expression(children[0], root))
        case .add, .subtract:
            return call(Self.opName(node.op), expression(children[0], root), expression(children[1], root), flag(node.first), flag(node.second))
        case .multiply, .divide:
            return call(Self.opName(node.op), expression(children[0], root), expression(children[1], root), flag(node.first), flag(node.second), flag(node.third))
        case .less, .lessOrEqual, .greater, .greaterOrEqual:
            let relation = node.op == .less ? 0 : node.op == .lessOrEqual ? 1 : node.op == .greater ? 2 : 3
            return call("Compare", expression(children[0], root), expression(children[1], root), flag(node.first), String(relation))
        case .equal, .notEqual:
            return call("Equal", expression(children[0], root), expression(children[1], root), flag(node.first), flag(node.op == .notEqual))
        case .and, .or:
            return call(Self.opName(node.op), expression(children[0], root), expression(children[1], root))
        case .choice:
            var result = expression(children[children.count - 1], root)
            var index = children.count - 3
            while index >= 0 {
                result = call("When", expression(children[index + 1], root), expression(children[index], root), result)
                index -= 2
            }
            return result
        case .min, .max:
            var result = expression(children[0], root)
            for child in children.dropFirst() { result = call("Extreme", result, expression(child, root), flag(node.first), flag(node.op == .max)) }
            return result
        case .clamp:
            return call("Clamp", expression(children[0], root), expression(children[1], root), expression(children[2], root), flag(node.first))
        case .default:
            return call("Fallback", expression(children[0], root), expression(children[1], root))
        case .hasValue:
            let child = children[0]
            if child.type != .record { return call("Has", expression(child, root)) }
            return call("Boolean", present(self.root(child, root), owner(child), child.path))
        case .count:
            let list = access.reach(root, owner, children[0].path).text
            return swift ? call("Integer", "Int64(\(list).count)") : call("Integer", "\(list).Count")
        case .sum:
            return sum(node, root)
        }
    }

    /// A sum adds the projected elements from left to right, starting with the first one, and is the integer 0 for none.
    private func sum(_ node: GesComputedNode, _ root: String) -> String {
        let list = access.reach(root, owner, node.children[0].path).text
        let name = "__\(field)Sum\(sums.count)"
        let previous = element
        element = access.elementOwner(owner, node.children[0].path)
        let value = node.children.count > 1 ? expression(node.children[1], root) : call(node.type == .percentage ? "ReadPercentage" : "Read", "item")
        element = previous
        var body = ""
        if swift {
            body += "private func \(name)() -> \(helper).Value {\n"
            body += "    var sum = \(helper).integer(0)\n"
            body += "    var first = true\n"
            body += "    for item in \(list) {\n"
            body += "        let value = \(value)\n"
            body += "        sum = first ? value : \(helper).add(sum, value, true, true)\n"
            body += "        first = false\n"
            body += "    }\n"
            body += "    return sum\n"
            body += "}"
        } else {
            body += "private \(helper).Value \(name)()\n"
            body += "{\n"
            body += "    var sum = \(helper).Integer(0);\n"
            body += "    var first = true;\n"
            body += "    foreach (var item in \(list))\n"
            body += "    {\n"
            body += "        var value = \(value);\n"
            body += "        sum = first ? value : \(helper).Add(sum, value, true, true);\n"
            body += "        first = false;\n"
            body += "    }\n"
            body += "\n"
            body += "    return sum;\n"
            body += "}"
        }
        sums.append(body)
        return "\(name)()"
    }
}
