// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// What both model emitters generate: per field its value in language-neutral terms with the names of the nested types it
// declares. A union with a :Nothing alternative becomes a nullable value without it, and a union that then holds one
// alternative becomes that alternative, so that absence reads as null or nil like an optional field.
struct GesModelPlanField {
    let field: GesModelField
    let base: String
    let value: GesModelPlanValue
}

final class GesModelPlanValue {
    var kind = GesModelKind.boolean
    var model = GesModelValue()
    // The value may be absent: an optional field or a union with a :Nothing alternative.
    var nullable = false
    // The nested type of a closed Tag (its enum), of Flags or of a union; empty for other kinds.
    var nested = ""
    // The named tag set of a closed Tag, whose enum is declared once beside the records, or empty.
    var tagSet = ""
    // The label of a union alternative, its case or subclass.
    var label = ""
    var element: GesModelPlanValue?
    var alternatives: [GesModelPlanValue] = []

    var closedTag: Bool { kind == .tag && !model.tags.isEmpty }
}

enum GesModelPlanner {
    // The named tag sets the types use, in order of first use, with their members.
    static func tagSets(_ layout: GesModelLayout) -> [(name: String, tags: [String])] {
        var sets: [(name: String, tags: [String])] = []
        var seen: Set<String> = []

        func visit(_ value: GesModelPlanValue) {
            if !value.tagSet.isEmpty && seen.insert(value.tagSet).inserted { sets.append((value.tagSet, value.model.tags)) }
            if let element = value.element { visit(element) }
            for alternative in value.alternatives { visit(alternative) }
        }

        for type in layout.types {
            for field in fields(type) { visit(field.value) }
        }
        return sets
    }

    // The name of the conversion functions of a closed Tag, Flags or union: per owner, or once per named tag set.
    static func conversionName(_ owner: String, _ value: GesModelPlanValue) -> String { value.tagSet.isEmpty ? owner + value.nested : value.tagSet }

    static func fields(_ type: GesModelType) -> [GesModelPlanField] {
        type.fields.map { field in
            let pascal = GesSchemaPlanner.pascal(field.name)
            let member = pascal == type.name ? pascal + "Value" : pascal
            let value = self.value(field.value, member, "")
            if field.optional { value.nullable = true }
            return GesModelPlanField(field: field, base: member, value: value)
        }
    }

    private static func value(_ model: GesModelValue, _ name: String, _ label: String) -> GesModelPlanValue {
        if model.kind == .union {
            let present = model.alternatives.filter { $0.value.kind != .nothing }
            if present.count == 1 {
                let single = self.value(present[0].value, name, label)
                single.nullable = true
                return single
            }
            let union = GesModelPlanValue()
            union.kind = .union
            union.model = model
            union.nullable = present.count < model.alternatives.count
            union.nested = name + "Alternative"
            union.label = label
            union.alternatives = present.map { self.value($0.value, name + GesSchemaPlanner.pascal($0.name), GesSchemaPlanner.pascal($0.name)) }
            return union
        }
        let value = GesModelPlanValue()
        value.kind = model.kind
        value.model = model
        value.label = label
        if value.closedTag {
            value.tagSet = model.tagSet
            value.nested = model.tagSet.isEmpty ? name + "Tag" : model.tagSet
        }
        if model.kind == .flags { value.nested = name + "Flags" }
        if model.kind == .list { value.element = self.value(model.element!.value, name + "Item", "") }
        return value
    }

    // The protocol (Swift) or interface (C#) of a schema that types take with `with`: an abstract schema's own name in Swift,
    // since no type takes it, `<Name>Fields` beside a type of that name, and `I<Name>` in C#.
    static func protocolName(_ layout: GesModelLayout, _ name: String, swift: Bool) -> String {
        if !swift { return "I" + name }
        return layout.protocols.first { $0.name == name }!.isAbstract ? name : name + "Fields"
    }

    // The protocols a type adopts: those of the schemas it takes unchanged and, for a plain schema that others take, its own.
    static func adopted(_ layout: GesModelLayout, _ type: GesModelType, swift: Bool) -> [String] {
        var adopted = type.takes.map { protocolName(layout, $0, swift: swift) }
        if layout.protocols.contains(where: { $0.name == type.name }) { adopted.append(protocolName(layout, type.name, swift: swift)) }
        return adopted
    }

    // Protocol names must not meet a type or tag set of the file.
    static func checkProtocols(_ layout: GesModelLayout, swift: Bool) throws(GesGeneratorError) {
        var names: [(name: String, origin: String)] = layout.types.map { ($0.name, "the type " + $0.name) }
        for set in tagSets(layout) { names.append((set.name, "the tag set " + set.name)) }
        for protocolType in layout.protocols { names.append((protocolName(layout, protocolType.name, swift: swift), "the protocol of " + protocolType.name)) }
        try GesSchemaPlanner.unique("the generated file", names)
    }

    // The order in which a record's fields are read: declared order, except that a field defaulting to a sibling follows it.
    static func readOrder(_ fields: [GesModelPlanField]) -> [Int] {
        var order: [Int] = []
        for index in fields.indices { place(fields, index, &order) }
        return order
    }

    private static func place(_ fields: [GesModelPlanField], _ index: Int, _ order: inout [Int]) {
        if order.contains(index) { return }
        if let named = sibling(fields, fields[index]) { place(fields, named, &order) }
        order.append(index)
    }

    // The optional fields that default to a sibling: an initializer takes nil or null for the sibling's value, so reading sets
    // them afterwards, and a value that leaves them out or gives nothing keeps what it says.
    static func kept(_ fields: [GesModelPlanField]) -> [Int] {
        fields.indices.filter { fields[$0].value.nullable && sibling(fields, fields[$0]) != nil }
    }

    // The sibling a field defaults to, or nil.
    static func sibling(_ fields: [GesModelPlanField], _ field: GesModelPlanField) -> Int? {
        guard let fallback = field.field.defaultValue, fallback.kind == .field else { return nil }
        return fields.firstIndex { $0.field.name == fallback.value }
    }

    // The number of a numeric default as a literal: whole digits for an integer field, otherwise a floating literal.
    static func number(_ value: GesModelPlanValue, _ fallback: GesLayoutDefault) -> String {
        let parts = fallback.value.split(separator: "e")
        var significand = Int64(parts[0])!
        let exponent = Int(parts[1])!
        if !value.model.integer { return GesSchemaPlanner.decimal(significand, exponent) }
        for _ in 0..<max(exponent, 0) { significand *= 10 }
        return String(significand)
    }

    // The types in an order where each comes after the types it holds, for registration: by name, depth first.
    static func dependencyOrder(_ layout: GesModelLayout) -> [GesModelType] {
        var ordered: [GesModelType] = []
        var seen: Set<String> = []
        for type in layout.types { visit(layout, type, &seen, &ordered) }
        return ordered
    }

    private static func visit(_ layout: GesModelLayout, _ type: GesModelType, _ seen: inout Set<String>, _ ordered: inout [GesModelType]) {
        guard seen.insert(type.name).inserted else { return }
        for field in type.fields { references(layout, field.value, &seen, &ordered) }
        ordered.append(type)
    }

    private static func references(_ layout: GesModelLayout, _ value: GesModelValue, _ seen: inout Set<String>, _ ordered: inout [GesModelType]) {
        if value.kind == .schema { visit(layout, layout.type(value.schema), &seen, &ordered) }
        if let element = value.element { references(layout, element.value, &seen, &ordered) }
        for alternative in value.alternatives { references(layout, alternative.value, &seen, &ordered) }
    }

    // The type a script sees for a field: a built-in kind with its unit, or a type name; a union keeps its value as `Value`.
    // Whether a model holds a Box or a Poly, whose conversions the support class then offers.
    // The type that reads a document of any root of a generation: Document within a namespace, which qualifies it, and
    // ModelDocument without one, where Document would collide too easily. It must not take the name of a generated type.
    static func documentType(_ layout: GesModelLayout, _ space: [String]) throws(GesGeneratorError) -> String {
        let name = space.isEmpty ? "ModelDocument" : "Document"
        if layout.types.contains(where: { $0.name == name }) || layout.protocols.contains(where: { $0.name == name }) || tagSets(layout).contains(where: { $0.name == name }) {
            throw GesGeneratorError(GesGeneratorError.nameCollision, "The document type '\(name)' takes the name of a generated type; choose another namespace.")
        }
        return name
    }

    static func usesGeometry(_ layout: GesModelLayout) -> Bool { layout.types.contains { $0.fields.contains { usesGeometry($0.value) } } }

    private static func usesGeometry(_ value: GesModelValue) -> Bool {
        value.kind == .box || value.kind == .poly || (value.element.map { usesGeometry($0.value) } ?? false) || value.alternatives.contains { usesGeometry($0.value) }
    }

    static func declared(_ value: GesModelPlanValue) -> (kind: String?, unit: String, typeName: String?) {
        switch value.kind {
        case .boolean: ("Boolean", "none", nil)
        case .number where value.model.type == "Percentage": ("Percentage", "none", nil)
        case .number: ("Float", value.model.unit, nil)
        case .text: ("Text", "none", nil)
        case .bytes: ("Bytes", "none", nil)
        case .tag: ("Tag", "none", nil)
        case .flags: ("Map", "none", nil)
        case .list: ("List", "none", nil)
        case .dice: ("Dice", "none", nil)
        case .point: ("Point", value.model.unit, nil)
        case .vector: ("Vector", value.model.unit, nil)
        case .box: (nil, "none", "Box")
        case .poly: (nil, "none", "Poly")
        case .schema: (nil, "none", value.model.schema)
        case .union, .nothing: (nil, "none", "Value")
        }
    }

    // The declaration as a reader would write it, for documentation: `:Quantity(meter) between 0 and worldWidth step 0.01`.
    static func describe(_ field: GesModelField) -> String {
        var text = ""
        describe(field.value, &text)
        if field.optional { text += " optional" }
        if field.key { text += " key" }
        if let fallback = field.defaultValue {
            text += " default "
            switch fallback.kind {
            case .number: text += decimal(fallback.value, field.value.type == "Percentage" ? 2 : 0)
            case .boolean: text += fallback.boolean ? "true" : "false"
            case .tag: text += "#" + fallback.value
            case .field: text += fallback.value
            }
        }
        return text
    }

    private static func describe(_ value: GesModelValue, _ text: inout String) {
        switch value.kind {
        case .boolean:
            text += ":Boolean"
        case .number:
            text += ":" + value.type
            if value.type == "Quantity" { text += "(\(value.unit))" }
            if value.integer { text += " integer" }
            let shift = value.type == "Percentage" ? 2 : 0
            if let lower = value.lower, let upper = value.upper {
                text += (value.wrapping ? " wrapping" : "") + " between " + decimal(lower, shift) + " and " + decimal(upper, shift)
            }
            if let step = value.step { text += " step " + decimal(step, shift) }
        case .tag:
            text += ":Tag"
            if !value.tags.isEmpty {
                text += "(#" + value.tags.joined(separator: ", #") + ")"
            } else if let max = value.max {
                text += " max \(max)"
            }
        case .flags:
            text += ":Flags(#" + value.tags.joined(separator: ", #") + ")"
        case .text:
            text += ":Text"
            if let max = value.max { text += " max \(max)" }
        case .bytes:
            text += ":Bytes"
            if let max = value.max { text += " max \(max)" }
        case .list:
            text += ":List("
            describe(value.element!.value, &text)
            text += ")"
            if let max = value.max { text += " max \(max)" }
            if value.depth > 0 { text += " depth \(value.depth)" }
        case .dice:
            text += ":Dice"
            if let max = value.max { text += " max \(max)" }
            if let lower = value.lower, let upper = value.upper { text += " between " + decimal(lower) + " and " + decimal(upper) }
        case .point, .vector, .box, .poly:
            text += value.kind == .point ? ":Point" : value.kind == .vector ? ":Vector" : value.kind == .box ? ":Box" : ":Poly"
            if value.unit != "none" { text += "(\(value.unit))" }
            if value.kind == .poly, let max = value.max { text += " max \(max)" }
            if !value.axes.isEmpty { text += " { " + value.axes.joined(separator: ", ") + " }" }
        case .schema:
            text += ":" + value.schema
        case .union:
            for (index, alternative) in value.alternatives.enumerated() {
                if index > 0 { text += " | " }
                describe(alternative.value, &text)
            }
        case .nothing:
            text += ":Nothing"
        }
    }

    // A canonical decimal `<significand>e<exponent>` as plain digits; a parameter name stays as it is. A Percentage's limits
    // are fractions, written with a shift of two and a percent sign.
    static func decimal(_ canonical: String, _ shift: Int = 0) -> String {
        guard let e = canonical.firstIndex(of: "e") else { return canonical }
        var digits = String(canonical[..<e])
        let exponent = Int(canonical[canonical.index(after: e)...])! + shift
        let negative = digits.hasPrefix("-")
        if negative { digits.removeFirst() }
        if exponent >= 0 {
            if digits != "0" { digits += String(repeating: "0", count: exponent) }
        } else {
            let point = digits.count + exponent
            digits = point > 0 ? String(digits.prefix(point)) + "." + String(digits.dropFirst(point)) : "0." + String(repeating: "0", count: -point) + digits
        }
        return (negative ? "-" + digits : digits) + (shift == 2 ? "%" : "")
    }

    // The header of a model file: the roots and their schema hashes, without parameters, which models never use.
    static func header(_ layout: GesModelLayout) -> String {
        var text = "// <auto-generated>\n"
        text += "//     Generated by `ges schema generate` as models of \(layout.roots.count == 1 ? "the root " : "the roots ")\(layout.roots.joined(separator: ", ")).\n"
        text += "//     Do not edit it; generate it again when a schema changes.\n"
        for root in layout.roots { text += "//     \(root): schema hash \(GesSchemaLayoutJson.hex(layout.type(root).hash)).\n" }
        text += "// </auto-generated>\n"
        return text
    }
}

extension GesModelPlanner {
    /// The translator of a model layout's computed fields: paths read the fields of model types, with optional chaining through
    /// optional fields, and a computed field through its exact value.
    static func computed(_ layout: GesModelLayout, _ helper: String, swift: Bool) -> GesComputedEmitter {
        GesComputedEmitter(swift: swift, helper: helper, access: GesModelAccess(layout: layout, swift: swift))
    }
}

private struct GesModelAccess: GesComputedAccess {
    let layout: GesModelLayout
    let swift: Bool

    func reach(_ root: String, _ owner: String, _ path: [String]) -> GesComputedReach {
        let reach = GesComputedReach(text: root)
        var type: GesModelType? = owner.isEmpty ? nil : layout.type(owner)
        var value: GesModelPlanValue?
        for name in path {
            let separator = reach.text.isEmpty ? "" : reach.nullable ? "?." : "."
            guard let field = GesModelPlanner.fields(type!).first(where: { $0.field.name == name }) else {
                reach.computedOwner = type!.name
                reach.computedName = name
                if reach.nullable { reach.text += "?" }
                return reach
            }
            reach.text += separator + member(field.base)
            if field.value.nullable { reach.nullable = true }
            value = field.value
            type = field.value.kind == .schema ? layout.type(field.value.model.schema) : nil
        }
        if let value { Self.declare(value, reach) }
        return reach
    }

    func element(_ owner: String, _ path: [String]) -> GesComputedReach {
        let reach = GesComputedReach(text: "item")
        Self.declare(value(owner, path).element!, reach)
        return reach
    }

    func elementOwner(_ owner: String, _ path: [String]) -> String {
        let element = value(owner, path).element!
        return element.kind == .schema ? element.model.schema : ""
    }

    func computed(_ owner: String) -> [GesSchemaComputedFieldInfo] { layout.type(owner).computed }

    /// The declared range of a number, where its bounds are literals, and the largest count of a list.
    private static func declare(_ value: GesModelPlanValue, _ reach: GesComputedReach) {
        let model = value.model
        reach.integer = model.integer
        if model.integer {
            reach.lower = Double(Int64.min)
            reach.upper = Double(Int64.max)
        }
        if let lower = number(model.lower) { reach.lower = lower }
        if let upper = number(model.upper) { reach.upper = upper }
        if value.kind == .list, let max = model.max { reach.maxCount = Double(max) }
    }

    /// A bound that is a canonical decimal, not a parameter's name.
    private static func number(_ bound: String?) -> Double? {
        guard let bound, let first = bound.unicodeScalars.first, first == "-" || ("0"..."9").contains(first) else { return nil }
        return Double(bound)
    }

    private func value(_ owner: String, _ path: [String]) -> GesModelPlanValue {
        var type = layout.type(owner)
        var value: GesModelPlanValue?
        for name in path {
            value = GesModelPlanner.fields(type).first { $0.field.name == name }!.value
            if value!.kind == .schema { type = layout.type(value!.model.schema) }
        }
        return value!
    }

    private func member(_ pascal: String) -> String {
        if !swift { return pascal }
        let camel = GesSchemaPlanner.camel(pascal)
        return GesSwiftEmitter.keywords.contains(camel) ? "`" + camel + "`" : camel
    }
}
