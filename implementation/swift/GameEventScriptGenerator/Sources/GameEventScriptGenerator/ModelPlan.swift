// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
    // The nested type of a closed Tag (its enum) or a union; empty for other kinds.
    var nested = ""
    // The label of a union alternative, its case or subclass.
    var label = ""
    var element: GesModelPlanValue?
    var alternatives: [GesModelPlanValue] = []

    var closedTag: Bool { kind == .tag && !model.tags.isEmpty }
}

enum GesModelPlanner {
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
                let single = value(present[0].value, name, label)
                single.nullable = true
                return single
            }
            let union = GesModelPlanValue()
            union.kind = .union
            union.model = model
            union.nullable = present.count < model.alternatives.count
            union.nested = name + "Alternative"
            union.label = label
            union.alternatives = present.map { value($0.value, name + GesSchemaPlanner.pascal($0.name), $0.name) }
            return union
        }
        let value = GesModelPlanValue()
        value.kind = model.kind
        value.model = model
        value.label = label
        if value.closedTag { value.nested = name + "Tag" }
        if model.kind == .list { value.element = self.value(model.element!.value, name + "Item", "") }
        return value
    }

    // The declaration as a reader would write it, for documentation: `:Quantity(meter) between 0 and worldWidth step 0.01`.
    static func describe(_ field: GesModelField) -> String {
        var text = ""
        describe(field.value, &text)
        if field.optional { text += " optional" }
        if field.key { text += " key" }
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
        case .text:
            text += ":Text"
            if let max = value.max { text += " max \(max)" }
        case .list:
            text += ":List("
            describe(value.element!.value, &text)
            text += ")"
            if let max = value.max { text += " max \(max)" }
        case .dice:
            text += ":Dice"
            if let max = value.max { text += " max \(max)" }
            if let lower = value.lower, let upper = value.upper { text += " between " + decimal(lower) + " and " + decimal(upper) }
        case .point, .vector:
            text += value.kind == .point ? ":Point" : ":Vector"
            if value.unit != "none" { text += "(\(value.unit))" }
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
