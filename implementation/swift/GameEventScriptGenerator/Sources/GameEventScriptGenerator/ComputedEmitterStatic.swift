// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

/// What binding knows about a node: its range, whether it may give nothing (NaN or an absent field), a negative zero, or
/// an exact Int64 that binary64 cannot hold.
final class GesComputedFacts {
    var lower = -Double.infinity
    var upper = Double.infinity
    var mayBeNothing = false
    var negativeZero = false
    var unrepresentable = false

    var infinite: Bool { lower.isInfinite || upper.isInfinite }

    var hasZero: Bool { lower <= 0 && upper >= 0 }

    var mayBeNegative: Bool { lower < 0 || negativeZero }

    var magnitude: Double { Swift.max(Swift.abs(lower), Swift.abs(upper)) }

    init() {}

    init(_ lower: Double, _ upper: Double) {
        if !lower.isNaN { self.lower = lower }
        if !upper.isNaN { self.upper = upper }
    }
}

/// The static form of a computed field: plain binary64 and Boolean code from the bound expression. The virtual machine holds
/// a number as an exact Int64 while it is whole, as binary64 otherwise, nothing for NaN and never a negative zero; binary64
/// arithmetic gives the same bits whenever no exact Int64 beyond 2^53 meets another operand. Binding proves that from the
/// declared ranges of the fields; NaN stands for nothing, and only the few places where IEEE and the virtual machine part,
/// such as `not` of nothing or a negative zero, get a helper.
extension GesComputedEmitter {
    typealias Facts = GesComputedFacts

    private static let limit = 9_007_199_254_740_992.0

    private final class Analysis {
        var owner: String
        var element = ""
        var risky = false

        init(_ owner: String) { self.owner = owner }
    }

    /// The facts of a computed field's value after its conversion to the field's type, or nil for the dynamic form.
    func analyze(_ owner: String, _ field: GesSchemaComputedFieldInfo) -> Facts? {
        let key = owner + "\u{0}" + field.name
        if analyzed.contains(key) { return fields[key] }
        let analysis = Analysis(owner)
        let facts = analyze(field.tree, analysis)
        var result: Facts?
        if !analysis.risky {
            let known = Facts(facts.lower, facts.upper)
            known.unrepresentable = facts.unrepresentable
            switch field.kind {
            case .boolean:
                break
            case .percentage:
                // A ratio that becomes infinite is no percentage, which the field's type turns into nothing.
                known.mayBeNothing = facts.mayBeNothing || facts.infinite || field.isOptional
            default:
                known.mayBeNothing = facts.mayBeNothing || field.isOptional
            }
            result = known
        }
        analyzed.insert(key)
        fields[key] = result
        return result
    }

    private static func unbounded(_ nothing: Bool) -> Facts {
        let facts = Facts()
        facts.mayBeNothing = nothing
        facts.unrepresentable = true
        return facts
    }

    private static func minimum(_ values: [Double]) -> Double {
        var result = Double.infinity
        for value in values { result = value.isNaN ? -Double.infinity : Swift.min(result, value) }
        return result
    }

    private static func maximum(_ values: [Double]) -> Double {
        var result = -Double.infinity
        for value in values { result = value.isNaN ? Double.infinity : Swift.max(result, value) }
        return result
    }

    private static func product(_ a: Facts, _ b: Facts) -> Facts {
        let corners = [a.lower * b.lower, a.lower * b.upper, a.upper * b.lower, a.upper * b.upper]
        let facts = Facts(minimum(corners), maximum(corners))
        facts.mayBeNothing = a.mayBeNothing || b.mayBeNothing || a.hasZero && b.infinite || b.hasZero && a.infinite
        facts.negativeZero = facts.hasZero && (a.mayBeNegative || b.mayBeNegative)
        return facts
    }

    private static func quotient(_ a: Facts, _ b: Facts) -> Facts {
        var facts: Facts
        if b.hasZero {
            facts = Facts()
        } else {
            let corners = [a.lower / b.lower, a.lower / b.upper, a.upper / b.lower, a.upper / b.upper]
            facts = Facts(minimum(corners), maximum(corners))
            if a.hasZero { facts = Facts(Swift.min(facts.lower, 0), Swift.max(facts.upper, 0)) }
        }
        facts.mayBeNothing = a.mayBeNothing || b.mayBeNothing || a.hasZero && b.hasZero || a.infinite && b.infinite
        facts.negativeZero = facts.hasZero && (a.mayBeNegative || b.mayBeNegative)
        return facts
    }

    private static func sum(_ a: Facts, _ b: Facts, subtract: Bool) -> Facts {
        let facts = subtract ? Facts(a.lower - b.upper, a.upper - b.lower) : Facts(a.lower + b.lower, a.upper + b.upper)
        facts.mayBeNothing = a.mayBeNothing || b.mayBeNothing || a.infinite && b.infinite
        facts.negativeZero = subtract ? a.negativeZero && b.hasZero : a.negativeZero && b.negativeZero
        return facts
    }

    private static func hull(_ parts: [Facts]) -> Facts {
        let facts = Facts()
        facts.lower = Double.infinity
        facts.upper = -Double.infinity
        for part in parts {
            facts.lower = Swift.min(facts.lower, part.lower)
            facts.upper = Swift.max(facts.upper, part.upper)
            facts.mayBeNothing = facts.mayBeNothing || part.mayBeNothing
            facts.negativeZero = facts.negativeZero || part.negativeZero
            facts.unrepresentable = facts.unrepresentable || part.unrepresentable
        }
        return facts
    }

    private func analyze(_ node: GesComputedNode, _ analysis: Analysis) -> Facts {
        let result = nodeFacts(node, analysis)
        facts[ObjectIdentifier(node)] = result
        return result
    }

    /// An operand that the virtual machine treats as an exact Int64 where binary64 rounds parts the two.
    private static func exact(_ analysis: Analysis, _ operands: [Facts]) {
        for operand in operands where operand.unrepresentable { analysis.risky = true }
    }

    private func nodeFacts(_ node: GesComputedNode, _ analysis: Analysis) -> Facts {
        let children = node.children
        switch node.op {
        case .literal:
            if node.type == .boolean { return Facts() }
            let value = node.literal.kind == .integer ? Double(node.literal.integer) : node.literal.number
            let facts = Facts(value, value)
            facts.unrepresentable = node.literal.kind == .integer && Swift.abs(value) > Self.limit
            return facts
        case .field, .item:
            return read(node, analysis)
        case .negate:
            let a = analyze(children[0], analysis)
            let facts = Facts(-a.upper, -a.lower)
            facts.mayBeNothing = a.mayBeNothing
            facts.negativeZero = a.hasZero
            facts.unrepresentable = a.unrepresentable
            return facts
        case .abs:
            let a = analyze(children[0], analysis)
            let facts = Facts(a.hasZero ? 0 : Swift.min(Swift.abs(a.lower), Swift.abs(a.upper)), a.magnitude)
            facts.mayBeNothing = a.mayBeNothing
            facts.unrepresentable = a.unrepresentable
            return facts
        case .add, .subtract:
            let a = analyze(children[0], analysis)
            let b = analyze(children[1], analysis)
            let subtract = node.op == .subtract
            if children[0].type == .number && children[1].type == .percentage {
                // x ± x · p in binary64 from the rounded x, as the virtual machine computes it; an infinite p is a plain
                // number there.
                if b.infinite { analysis.risky = true }
                return Self.sum(a, Self.product(a, b), subtract: subtract)
            }
            if children[0].type == .percentage {
                if a.infinite || b.infinite { analysis.risky = true }
                return Self.sum(a, b, subtract: subtract)
            }
            Self.exact(analysis, [a, b])
            let result = Self.sum(a, b, subtract: subtract)
            result.unrepresentable = result.magnitude > Self.limit
            return result
        case .multiply:
            let a = analyze(children[0], analysis)
            let b = analyze(children[1], analysis)
            let result = Self.product(a, b)
            if children[0].type == .number && children[1].type == .number {
                Self.exact(analysis, [a, b])
                result.unrepresentable = result.magnitude > Self.limit
            }
            return result
        case .divide:
            return Self.quotient(analyze(children[0], analysis), analyze(children[1], analysis))
        case .less, .lessOrEqual, .greater, .greaterOrEqual, .equal, .notEqual:
            let a = analyze(children[0], analysis)
            let b = analyze(children[1], analysis)
            Self.exact(analysis, [a, b])
            let facts = Facts()
            facts.mayBeNothing = a.mayBeNothing || b.mayBeNothing
            return facts
        case .not:
            let facts = Facts()
            facts.mayBeNothing = analyze(children[0], analysis).mayBeNothing
            return facts
        case .and, .or:
            let a = analyze(children[0], analysis)
            let b = analyze(children[1], analysis)
            let facts = Facts()
            facts.mayBeNothing = a.mayBeNothing || b.mayBeNothing
            return facts
        case .choice:
            var values: [Facts] = []
            for (index, child) in children.enumerated() {
                let facts = analyze(child, analysis)
                if index % 2 == 0 || index == children.count - 1 { values.append(facts) }
            }
            return Self.hull(values)
        case .min, .max, .clamp:
            let parts = children.map { analyze($0, analysis) }
            Self.exact(analysis, parts)
            return Self.hull(parts)
        case .default:
            let a = analyze(children[0], analysis)
            let b = analyze(children[1], analysis)
            let facts = Self.hull([a, b])
            facts.mayBeNothing = b.mayBeNothing
            return facts
        case .hasValue:
            _ = analyze(children[0], analysis)
            return Facts()
        case .count:
            let list = access.reach("", analysis.owner, children[0].path)
            return Facts(0, Swift.min(list.maxCount, Self.limit))
        case .sum:
            return sumFacts(node, analysis)
        }
    }

    private func sumFacts(_ node: GesComputedNode, _ analysis: Analysis) -> Facts {
        let source = node.children[0]
        let list = access.reach("", analysis.owner, source.path)
        let element: Facts
        if node.children.count > 1 {
            let previous = analysis.element
            analysis.element = access.elementOwner(analysis.owner, source.path)
            element = analyze(node.children[1], analysis)
            analysis.element = previous
        } else {
            element = Self.declared(access.element(analysis.owner, source.path))
        }
        let count = list.maxCount
        let facts = Facts(Swift.min(0, count * element.lower), Swift.max(0, count * element.upper))
        facts.mayBeNothing = element.mayBeNothing || element.infinite
        Self.exact(analysis, [element])
        // Each partial sum is added to the next element: as exact Int64 values in the virtual machine while they are whole,
        // and as a percentage only while it stays finite.
        if node.type == .percentage ? facts.infinite : facts.magnitude > Self.limit { analysis.risky = true }
        return facts
    }

    private static func declared(_ reach: GesComputedReach) -> Facts {
        let facts = Facts(reach.lower, reach.upper)
        facts.unrepresentable = reach.integer && facts.magnitude > limit
        return facts
    }

    private func read(_ node: GesComputedNode, _ analysis: Analysis) -> Facts {
        let owner = node.op == .item ? analysis.element : analysis.owner
        if node.type == .record { return Facts() }
        let reach = access.reach("", owner, node.path)
        let facts: Facts
        if !reach.computedName.isEmpty {
            if let known = analyze(reach.computedOwner, target(reach)) {
                facts = Facts(known.lower, known.upper)
                facts.mayBeNothing = known.mayBeNothing
                facts.unrepresentable = known.unrepresentable
            } else {
                facts = Self.unbounded(true)
            }
        } else {
            facts = node.type == .boolean ? Facts() : Self.declared(reach)
        }
        facts.mayBeNothing = facts.mayBeNothing || node.mayBeAbsent
        return facts
    }

    // ---- Code ----

    /// A number, or a Boolean where nothing counts as false (plain), or a Boolean that keeps nothing as an optional Bool.
    private enum Want {
        case plain, tri
    }

    /// Generated text with its precedence: 10 primary, 9 unary, 8 multiplicative, 7 additive, 6 relational, 5 equality,
    /// 4 and, 3 or, 2 conditional.
    private struct Text {
        let value: String
        let precedence: Int

        init(_ value: String, _ precedence: Int) {
            self.value = value
            self.precedence = precedence
        }
    }

    private static func wrap(_ text: Text, _ precedence: Int) -> String { text.precedence < precedence ? "(" + text.value + ")" : text.value }

    private var nan: String { swift ? "Double.nan" : "double.NaN" }

    private func isNan(_ value: Text) -> String { swift ? Self.wrap(value, 10) + ".isNaN" : "double.IsNaN(\(value.value))" }

    private var null: String { swift ? "nil" : "(bool?)null" }

    func staticMembers(_ code: inout Code, _ field: GesSchemaComputedFieldInfo, _ known: Facts, _ access: String) {
        let root = facts[ObjectIdentifier(field.tree)]!
        let value = emit(field.tree, .plain, "")
        let text: String
        if field.kind == .boolean {
            text = value.value
        } else if field.kind == .percentage {
            text = field.isOptional ? call("OptionalRatio", value.value) : root.infinite || root.negativeZero ? call("Ratio", value.value) : value.value
        } else {
            text = field.isOptional ? call("Optional", value.value) : root.negativeZero ? call("PositiveZero", value.value) : value.value
        }
        summary(&code, field)
        let property = Self.property(field.name, owner, swift: swift)
        let type = Self.propertyType(field, swift: swift)
        if swift {
            let head = "\(access.isEmpty ? "" : access + " ")var \(member(property)): \(type)"
            if head.count + text.count + 6 <= 120 {
                code.line("\(head) { \(text) }")
            } else {
                code.open(head + " {")
                for line in Self.lines(text) { code.line(line) }
                code.close()
            }
        } else {
            let head = "\(access) \(type) \(property) =>"
            if head.count + text.count + 2 <= 120 {
                code.line("\(head) \(text);")
            } else {
                code.line(head)
                code.indent()
                let lines = Self.lines(text)
                for (index, line) in lines.enumerated() { code.line(line + (index == lines.count - 1 ? ";" : "")) }
                code.dedent()
            }
        }
    }

    /// A long chain of choices, one choice per line.
    private static func lines(_ text: String) -> [String] {
        let characters = Array(text)
        var lines: [String] = []
        var depth = 0
        var start = 0
        var index = 0

        func part(_ from: Int, _ to: Int) -> String {
            var piece = String(characters[from..<to])
            while let first = piece.first, first == ":" || first == " " { piece.removeFirst() }
            return piece
        }

        while index + 2 < characters.count {
            let character = characters[index]
            if character == "(" {
                depth += 1
            } else if character == ")" {
                depth -= 1
            } else if depth == 0 && character == " " && characters[index + 1] == ":" && characters[index + 2] == " " {
                lines.append((lines.isEmpty ? "" : ": ") + part(start, index))
                start = index + 1
            }
            index += 1
        }
        lines.append((lines.isEmpty ? "" : ": ") + part(start, characters.count))
        for index in lines.indices.dropFirst() { lines[index] = "    " + lines[index] }
        return lines
    }

    private func literal(_ node: GesComputedNode) -> String {
        let text = GesSchemaPlanner.decimal(node.decimal.significand, node.decimal.exponent)
        return text.contains(".") || text.contains("e") ? text : text + ".0"
    }

    // An operand of an addition or subtraction. A C++ compiler, such as the one behind IL2CPP, may contract a product that
    // an addition takes in the same expression into one fused operation with other rounding; a product computed by a helper
    // call stays a separate operation, as in the VM. Swift does not contract without an explicit option.
    private func operand(_ node: GesComputedNode, _ root: String) -> Text {
        if !swift && node.op == .multiply { return Text(product(node, root), 10) }
        if !swift && node.op == .negate && node.children[0].op == .multiply { return Text("-" + product(node.children[0], root), 9) }
        return emit(node, .plain, root)
    }

    private func product(_ node: GesComputedNode, _ root: String) -> String {
        call("Product", emit(node.children[0], .plain, root).value, emit(node.children[1], .plain, root).value)
    }

    private func emit(_ node: GesComputedNode, _ want: Want, _ root: String) -> Text {
        let children = node.children
        let facts = self.facts[ObjectIdentifier(node)]!
        switch node.op {
        case .literal:
            if node.type == .boolean { return Text(node.literal.kind == .true ? "true" : "false", 10) }
            return Text(literal(node), 10)
        case .field, .item:
            return fieldText(node, want, root)
        case .negate:
            let operand = emit(children[0], .plain, root)
            return Text("-" + Self.wrap(operand, operand.value.hasPrefix("-") ? 11 : 9), 9)
        case .abs:
            return Text((swift ? "abs(" : "global::System.Math.Abs(") + emit(children[0], .plain, root).value + ")", 10)
        case .add, .subtract:
            let symbol = node.op == .add ? " + " : " - "
            let a = operand(children[0], root)
            let b = operand(children[1], root)
            if children[0].type == .number && children[1].type == .percentage {
                if swift && a.precedence == 10 && children[0].op != .literal && !a.value.contains("(") {
                    return Text(a.value + symbol + a.value + " * " + Self.wrap(b, 9), 7)
                }
                return Text(call(node.op == .add ? "AddPercentage" : "SubtractPercentage", a.value, b.value), 10)
            }
            return Text(Self.wrap(a, 7) + symbol + Self.wrap(b, 8), 7)
        case .multiply:
            return Text(Self.wrap(emit(children[0], .plain, root), 8) + " * " + Self.wrap(emit(children[1], .plain, root), 9), 8)
        case .divide:
            let a = emit(children[0], .plain, root)
            let b = emit(children[1], .plain, root)
            let divisor = self.facts[ObjectIdentifier(children[1])]!.negativeZero ? call("PositiveZero", b.value) : Self.wrap(b, 9)
            return Text(Self.wrap(a, 8) + " / " + divisor, 8)
        case .less, .lessOrEqual, .greater, .greaterOrEqual:
            let a = emit(children[0], .plain, root)
            let b = emit(children[1], .plain, root)
            let name =
                node.op == .less ? "Less" : node.op == .lessOrEqual ? "LessOrEqual" : node.op == .greater ? "Greater" : "GreaterOrEqual"
            if want == .tri && facts.mayBeNothing { return Text(call(name + "OrNothing", a.value, b.value), 10) }
            let symbol = node.op == .less ? " < " : node.op == .lessOrEqual ? " <= " : node.op == .greater ? " > " : " >= "
            return Text(Self.wrap(a, 7) + symbol + Self.wrap(b, 7), 6)
        case .equal, .notEqual:
            return equality(node, want, root)
        case .not:
            let child = self.facts[ObjectIdentifier(children[0])]!
            if !child.mayBeNothing { return Text("!" + Self.wrap(emit(children[0], .plain, root), 9), 9) }
            let operand = emit(children[0], .tri, root)
            if want == .plain { return Text(Self.wrap(operand, 6) + " == false", 5) }
            return swift ? Text(Self.wrap(operand, 10) + ".map { !$0 }", 10) : Text("!" + Self.wrap(operand, 9), 9)
        case .and, .or:
            let and = node.op == .and
            if want == .tri && facts.mayBeNothing {
                return Text(call(and ? "And" : "Or", emit(children[0], .tri, root).value, emit(children[1], .tri, root).value), 10)
            }
            let precedence = and ? 4 : 3
            return Text(Self.wrap(emit(children[0], .plain, root), precedence) + (and ? " && " : " || ") + Self.wrap(emit(children[1], .plain, root), precedence), precedence)
        case .choice:
            var result = emit(children[children.count - 1], want, root)
            var index = children.count - 3
            while index >= 0 {
                let condition = emit(children[index + 1], .plain, root)
                let value = emit(children[index], want, root)
                // A condition that binding proves, such as `has value` of a field that is never absent, needs no choice.
                if condition.value == "true" {
                    result = value
                } else if condition.value != "false" {
                    result = Text(Self.wrap(condition, 3) + " ? " + Self.wrap(value, 3) + " : " + Self.wrap(result, 2), 2)
                }
                index -= 2
            }
            return result
        case .min, .max:
            var result = emit(children[0], .plain, root)
            for child in children.dropFirst() { result = Text(call(node.op == .min ? "Min" : "Max", result.value, emit(child, .plain, root).value), 10) }
            return result
        case .clamp:
            return Text(call("Clamp", emit(children[0], .plain, root).value, emit(children[1], .plain, root).value, emit(children[2], .plain, root).value), 10)
        case .default:
            return fallback(node, want, root)
        case .hasValue:
            return hasValue(node, root)
        case .count:
            let list = access.reach(root, owner, children[0].path).text
            return swift ? Text("Double(\(list).count)", 10) : Text("(double)\(list).Count", 9)
        case .sum:
            return staticSum(node, root)
        }
    }

    /// A read: a member converted to binary64, NaN where an optional field on the way is absent; Booleans stay Booleans.
    private func fieldText(_ node: GesComputedNode, _ want: Want, _ root: String) -> Text {
        let reach = access.reach(self.root(node, root), owner(node), node.path)
        let conditions = reach.conditions.filter { $0.step >= node.present }.map(\.condition)
        let when = conditions.joined(separator: " && ")
        if !reach.computedName.isEmpty {
            let target = target(reach)
            var text = Self.join(reach.text, member(Self.property(target.name, reach.computedOwner, swift: swift)))
            if target.kind == .boolean {
                if reach.nullable { return want == .tri ? Text(text, 10) : Text(text + " == true", 5) }
                if conditions.isEmpty { return Text(text, 10) }
                return want == .tri ? Text("\(when) ? \(text) : \(null)", 2) : Text("\(when) && \(Self.wrap(Text(text, 10), 4))", 4)
            }
            if target.isOptional || reach.nullable { text = "(\(text) ?? \(nan))" }
            return conditions.isEmpty ? Text(text, 10) : Text("\(when) ? \(text) : \(nan)", 2)
        }
        let member = reach.text
        if node.type == .boolean {
            if reach.nullable { return want == .tri ? Text(member, 10) : Text(member + " == true", 5) }
            if conditions.isEmpty { return Text(member, 10) }
            return want == .tri ? Text("\(when) ? \(member) : \(null)", 2) : Text("\(when) && \(member)", 4)
        }
        if reach.nullable {
            let number = reach.integer && swift ? "\(member).map { Double($0) }" : member
            return Text("(\(number) ?? \(nan))", 10)
        }
        let value = reach.integer ? (swift ? "Double(\(member))" : "(double)\(member)") : member
        if conditions.isEmpty { return Text(value, reach.integer && !swift ? 9 : 10) }
        return Text("\(when) ? \(value) : \(nan)", 2)
    }

    private func equality(_ node: GesComputedNode, _ want: Want, _ root: String) -> Text {
        let children = node.children
        let equal = node.op == .equal
        let facts = self.facts[ObjectIdentifier(node)]!
        if children[0].type == .boolean {
            if !facts.mayBeNothing {
                return Text(Self.wrap(emit(children[0], .plain, root), 6) + (equal ? " == " : " != ") + Self.wrap(emit(children[1], .plain, root), 6), 5)
            }
            let same = Text(call("Same", emit(children[0], .tri, root).value, emit(children[1], .tri, root).value), 10)
            return truth(same, equal, want)
        }
        let name = children[0].type == .percentage ? "EqualRatios" : "Equal"
        let a = emit(children[0], .plain, root).value
        let b = emit(children[1], .plain, root).value
        if !facts.mayBeNothing {
            let call = Text(self.call(name, a, b), 10)
            return equal ? call : Text("!" + call.value, 9)
        }
        if want == .plain && equal { return Text(call(name, a, b), 10) }
        return truth(Text(call(name + "OrNothing", a, b), 10), equal, want)
    }

    /// An optional Boolean as asked for: kept, or true only for true (or for false when negated).
    private func truth(_ value: Text, _ positive: Bool, _ want: Want) -> Text {
        if want == .plain { return Text(value.value + (positive ? " == true" : " == false"), 5) }
        if positive { return value }
        return swift ? Text(value.value + ".map { !$0 }", 10) : Text("!" + value.value, 9)
    }

    private func fallback(_ node: GesComputedNode, _ want: Want, _ root: String) -> Text {
        let children = node.children
        let left = facts[ObjectIdentifier(children[0])]!
        if !left.mayBeNothing { return emit(children[0], want, root) }
        if node.type == .boolean {
            let value = emit(children[0], .tri, root)
            let fallback = emit(children[1], want, root)
            return Text("(\(value.value) ?? \(fallback.value))", 10)
        }
        let right = emit(children[1], .plain, root)
        // An optional field that is only ever absent reads its fallback directly.
        if (children[0].op == .field || children[0].op == .item) && children[0].type != .record {
            let reach = access.reach(self.root(children[0], root), owner(children[0]), children[0].path)
            if reach.computedName.isEmpty && reach.conditions.count + (reach.nullable ? 1 : 0) > 0 && (reach.conditions.isEmpty || !reach.nullable) {
                let conditions = reach.conditions.filter { $0.step >= children[0].present }.map(\.condition)
                let member = reach.integer ? (swift ? "Double(\(reach.text))" : "(double)\(reach.text)") : reach.text
                if reach.nullable {
                    let number = reach.integer && swift ? "\(reach.text).map { Double($0) }" : reach.text
                    return Text("(\(number) ?? \(right.value))", 10)
                }
                if !conditions.isEmpty { return Text("\(conditions.joined(separator: " && ")) ? \(member) : \(Self.wrap(right, 3))", 2) }
            }
        }
        return Text(call("Otherwise", emit(children[0], .plain, root).value, right.value), 10)
    }

    private func hasValue(_ node: GesComputedNode, _ root: String) -> Text {
        let child = node.children[0]
        if child.type == .record { return Text(present(self.root(child, root), owner(child), child.path), 4) }
        let facts = self.facts[ObjectIdentifier(child)]!
        if !facts.mayBeNothing { return Text("true", 10) }
        // A stored field is nothing only where it is absent, which its presence flags or its optional value tell.
        if child.op == .field || child.op == .item {
            let reach = access.reach(self.root(child, root), owner(child), child.path)
            if reach.computedName.isEmpty {
                if reach.nullable { return Text(reach.text + (swift ? " != nil" : " != null"), 5) }
                let conditions = reach.conditions.filter { $0.step >= child.present }.map(\.condition)
                if conditions.isEmpty { return Text("true", 10) }
                return Text(conditions.joined(separator: " && "), conditions.count == 1 ? 10 : 4)
            }
        }
        if child.type == .boolean { return Text(Self.wrap(emit(child, .tri, root), 6) + (swift ? " != nil" : " != null"), 5) }
        return Text("!" + isNan(emit(child, .plain, root)), 9)
    }

    /// A sum adds the projected elements from left to right; binary64 from 0 gives the bits of the virtual machine, which
    /// starts with the first element and has no negative zero.
    private func staticSum(_ node: GesComputedNode, _ root: String) -> Text {
        let selfName = swift ? "self" : "this"
        let list = access.reach(selfName, owner, node.children[0].path).text
        let name = "__\(field)Sum\(sums.count)"
        let previous = element
        element = access.elementOwner(owner, node.children[0].path)
        let value: String
        if node.children.count > 1 {
            value = emit(node.children[1], .plain, selfName).value
        } else {
            let element = access.element(owner, node.children[0].path)
            value = element.integer ? (swift ? "Double(item)" : "(double)item") : "item"
        }
        element = previous
        var body = ""
        if swift {
            body += "private func \(name)() -> Double {\n"
            body += "    var sum = 0.0\n"
            body += "    for item in \(list) { sum += \(value) }\n"
            body += "    return sum\n"
            body += "}"
        } else {
            body += "private double \(name)()\n"
            body += "{\n"
            body += "    var sum = 0.0;\n"
            body += "    foreach (var item in \(list)) sum += \(value);\n"
            body += "    return sum;\n"
            body += "}"
        }
        sums.append(body)
        return Text("\(name)()", 10)
    }
}
