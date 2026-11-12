// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

/// How generated code reaches the fields that computed expressions read. Paths start at the record (`self`, `this`) or at a
/// list element (`item`, whose owner is its schema, or empty for a number).
protocol GesComputedAccess {
    /// The exact value at a path, nothing where an optional field on the way is absent.
    func read(_ root: String, _ owner: String, _ path: [String], percentage: Bool) -> String

    /// Whether the record at a path is present.
    func present(_ root: String, _ owner: String, _ path: [String]) -> String

    /// The list at a path that holds no optional field.
    func list(_ root: String, _ owner: String, _ path: [String]) -> String

    /// The schema of a list's elements, or empty for numbers.
    func elementOwner(_ owner: String, _ path: [String]) -> String
}

/// Translates the checked expressions of computed fields into C# or Swift. Each field becomes a property and a method that
/// returns the exact GES value, which other computed fields read, and each sum a method with a loop. The arithmetic lives in
/// one helper type per generation that repeats the rules of the GES virtual machine, so that the generated code gives the
/// bits a script record gives.
final class GesComputedEmitter {
    private typealias Code = GesSchemaPlanner.Code

    private let swift: Bool
    private let helper: String
    private let access: any GesComputedAccess
    private var sums: [String] = []
    private var owner = ""
    private var field = ""
    private var element = ""

    init(swift: Bool, helper: String, access: any GesComputedAccess) {
        self.swift = swift
        self.helper = helper
        self.access = access
    }

    static func helper(_ suffix: String) -> String { "__GesComputed" + suffix }

    /// The member names of a computed field: the property and the method with the exact value.
    static func property(_ name: String, _ type: String, swift: Bool) -> String {
        var pascal = GesSchemaPlanner.pascal(name)
        if pascal == type { pascal += "Value" }
        return swift ? GesSchemaPlanner.camel(pascal) : pascal
    }

    static func method(_ name: String, _ type: String, swift: Bool) -> String { "__" + property(name, type, swift: swift) }

    /// The names a type's computed fields add, for the collision check of its members.
    static func names(_ computed: [GesSchemaComputedFieldInfo], _ type: String, swift: Bool, _ names: inout [(name: String, origin: String)]) {
        for field in computed {
            names.append((property(field.name, type, swift: swift), "the computed field " + field.name))
            names.append((method(field.name, type, swift: swift), "the value of the computed field " + field.name))
        }
    }

    /// The type of a computed field's property: binary64 for numbers and percentage ratios, optional when the field is.
    static func propertyType(_ field: GesSchemaComputedFieldInfo, swift: Bool) -> String {
        if field.kind == .boolean { return swift ? "Bool" : "bool" }
        return (swift ? "Double" : "double") + (field.isOptional ? "?" : "")
    }

    /// Writes the properties, value methods and sum methods of a type's computed fields.
    func members(_ code: inout GesSchemaPlanner.Code, _ owner: String, _ computed: [GesSchemaComputedFieldInfo], _ access: String) {
        let root = swift ? "self" : "this"
        for field in computed {
            self.owner = owner
            self.field = Self.property(field.name, owner, swift: swift)
            sums = []
            let expression = cast(self.expression(field.tree, root), field)
            let property = Self.property(field.name, owner, swift: swift)
            let method = Self.method(field.name, owner, swift: swift)
            let result = field.kind == .boolean ? "IsTrue" : field.isOptional ? "OptionalNumber" : "Number"
            code.blank()
            if swift {
                code.line("/// `\(field.name)` computed by `\(field.spelling)`; \(Self.absent(field)).")
                code.line("\(access.isEmpty ? "" : access + " ")var \(property): \(Self.propertyType(field, swift: true)) { \(helper).\(camel(result))(\(method)()) }")
                code.blank()
                code.line("/// The exact GES value of `\(field.name)`, which other computed fields read.")
                code.line("func \(method)() -> \(helper).Value { \(expression) }")
            } else {
                code.line("/// <summary><c>\(Self.escape(field.name))</c> computed by <c>\(Self.escape(field.spelling))</c>; \(Self.absent(field)).</summary>")
                code.line("\(access) \(Self.propertyType(field, swift: false)) \(property) => \(helper).\(result)(\(method)());")
                code.blank()
                code.line("/// <summary>The exact GES value of <c>\(Self.escape(field.name))</c>, which other computed fields read.</summary>")
                code.line("internal \(helper).Value \(method)() => \(expression);")
            }
            for body in sums {
                code.blank()
                for line in body.split(separator: "\n", omittingEmptySubsequences: false) { code.line(String(line)) }
            }
        }
    }

    private static func absent(_ field: GesSchemaComputedFieldInfo) -> String {
        field.kind == .boolean
            ? "false where the expression gives nothing, as a script record reads it"
            : field.isOptional ? "absent where the expression gives nothing" : "NaN where the expression gives nothing"
    }

    private static func escape(_ text: String) -> String {
        replace(replace(replace(text, "&", "&amp;"), "<", "&lt;"), ">", "&gt;")
    }

    /// Replaces every occurrence of a text, without the newer standard library or Foundation.
    static func replace(_ text: String, _ target: String, _ replacement: String) -> String {
        let source = Array(text.unicodeScalars)
        let pattern = Array(target.unicodeScalars)
        var result = String.UnicodeScalarView()
        var index = 0
        while index < source.count {
            if index + pattern.count <= source.count && source[index..<(index + pattern.count)].elementsEqual(pattern) {
                result.append(contentsOf: replacement.unicodeScalars)
                index += pattern.count
            } else {
                result.append(source[index])
                index += 1
            }
        }
        return String(result)
    }

    private func camel(_ name: String) -> String { swift ? GesSchemaPlanner.camel(name) : name }

    private func call(_ name: String, _ arguments: String...) -> String { "\(helper).\(camel(name))(\(arguments.joined(separator: ", ")))" }

    private func flag(_ value: Bool) -> String { value ? "true" : "false" }

    /// A script record converts the value to the field's type: a Percentage takes a finite number, a Boolean only true.
    private func cast(_ expression: String, _ field: GesSchemaComputedFieldInfo) -> String {
        switch field.kind {
        case .percentage: return call("CastPercentage", expression)
        case .boolean: return call("CastBoolean", expression)
        default: return expression
        }
    }

    private static func opName(_ op: GesComputedOp) -> String { GesSchemaPlanner.pascal(op.name) }

    private func expression(_ node: GesComputedNode, _ root: String) -> String {
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
            return access.read(node.op == .item ? "item" : root, node.op == .item ? element : owner, node.path, percentage: node.type == .percentage)
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
            return call("Boolean", access.present(child.op == .item ? "item" : root, child.op == .item ? element : owner, child.path))
        case .count:
            let list = access.list(root, owner, children[0].path)
            return swift ? call("Integer", "Int64(\(list).count)") : call("Integer", "\(list).Count")
        case .sum:
            return sum(node, root)
        }
    }

    /// A sum adds the projected elements from left to right, starting with the first one, and is the integer 0 for none.
    private func sum(_ node: GesComputedNode, _ root: String) -> String {
        let list = access.list(root, owner, node.children[0].path)
        let name = "__\(field)Sum\(sums.count)"
        let previous = element
        element = access.elementOwner(owner, node.children[0].path)
        let value = node.children.count > 1 ? expression(node.children[1], root) : access.read("item", "", [], percentage: node.type == .percentage)
        element = previous
        var body = ""
        if swift {
            body += "func \(name)() -> \(helper).Value {\n"
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
            body += "internal \(helper).Value \(name)()\n"
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
