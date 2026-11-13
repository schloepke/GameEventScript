// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The direct conversion between a wire record and the model of the same schema, which `--models` names: ToModel builds the
// model and CopyFrom(model) fills the record, field by field without GES values. Both walk the wire plan and the model plan of
// the schema side by side; a packed record is the same type on both sides, so its fields and its Lists copy as they are.
extension GesCSharpEmitter {
    // The model layout and the qualifier of its namespace, and a counter for the names of locals.
    final class ModelConversion {
        let layout: GesModelLayout
        let qualifier: String
        private var counter = 0

        init(_ layout: GesModelLayout, _ qualifier: String) {
            self.layout = layout
            self.qualifier = qualifier
        }

        func type(_ name: String) -> GesModelType? { layout.types.first { $0.name == name } }

        func local(_ name: String) -> String {
            defer { counter += 1 }
            return name + String(counter)
        }

        func reset() { counter = 0 }
    }

    static func modelMembers(_ code: inout Code, _ type: GesPlanType, _ model: GesModelType, _ conversion: ModelConversion) throws(GesGeneratorError) {
        let name = conversion.qualifier + model.name
        let fields = GesModelPlanner.fields(model)
        code.blank()
        code.line("// Whether the model type was generated from the same schema; generated code of another schema state never converts.")
        code.line("private static readonly bool __modelMatches = \(name).SchemaHash == SchemaHash;")
        code.blank()
        code.line("/// <summary>Creates the model of this record, field by field without GES values.</summary>")
        code.line("/// <returns>The model.</returns>")
        code.line("/// <exception cref=\"global::System.InvalidOperationException\">The model type was generated from another state of the schema.</exception>")
        code.line("public \(type.isClass ? "" : "readonly ")\(name) ToModel()")
        code.open("{")
        code.line("if (!__modelMatches) throw new global::System.InvalidOperationException(\"The model type \(model.name) was generated from another state of the schema than this wire record.\");")
        code.line("var model = new \(name)();")
        conversion.reset()
        for field in type.fields {
            guard let match = fields.first(where: { $0.field.name == field.layout.name }) else { throw unconvertible(type, field.layout.name) }
            toModelField(&code, field, match, model.name, "this", "", "model.\(match.base)", conversion)
        }
        code.line("return model;")
        code.close()
        code.blank()
        code.line("/// <summary>Fills this record from a model, field by field without GES values; the values are checked when the record is written.</summary>")
        code.line("/// <param name=\"model\">The model.</param>")
        code.line("/// <exception cref=\"global::System.InvalidOperationException\">The model type was generated from another state of the schema.</exception>")
        code.line("public void CopyFrom(\(name) model)")
        code.open("{")
        code.line("if (model is null) throw new global::System.ArgumentNullException(nameof(model));")
        code.line("if (!__modelMatches) throw new global::System.InvalidOperationException(\"The model type \(model.name) was generated from another state of the schema than this wire record.\");")
        conversion.reset()
        for field in type.fields {
            let match = fields.first { $0.field.name == field.layout.name }!
            fromModelField(&code, field, match, model.name, "this", "", "model.\(match.base)", conversion)
        }
        code.close()
    }

    // A statement header with the brace that opens its block on the next line.
    private static func block(_ code: inout Code, _ header: String) {
        code.line(header)
        code.open("{")
    }

    private static func unconvertible(_ type: GesPlanType, _ field: String) -> GesGeneratorError {
        GesGeneratorError(GesGeneratorError.usage, "The field \(field) of :\(type.name) has no counterpart in the model; generate the model from the same schema.")
    }

    private static func trimmed(_ text: String) -> String {
        var text = text
        while text.hasSuffix("?") { text.removeLast() }
        return text
    }

    // Converts a field of `owner`, whose nested wire types `wired` qualifies, into the model member `target`.
    private static func toModelField(
        _ code: inout Code,
        _ field: GesPlanField,
        _ model: GesModelPlanField,
        _ modelOwner: String,
        _ owner: String,
        _ wired: String,
        _ target: String,
        _ conversion: ModelConversion
    ) {
        if !field.optional {
            toModel(&code, field.value, model.value, modelOwner, owner, wired, "\(owner).\(field.value.base)", target, conversion)
            return
        }
        let local = conversion.local("present")
        block(&code, "if (\(owner).Has\(field.base))")
        code.line("\(GesCSharpModelEmitter.type(model.value, conversion.qualifier, modelOwner)) \(local);")
        toModel(&code, field.value, model.value, modelOwner, owner, wired, "\(owner).\(field.value.base)", local, conversion)
        code.line("\(target) = \(local);")
        code.close()
        code.line("else \(target) = null;")
    }

    // Converts a wire value, read from `source` or, for a union, from the members of `owner`, into the model value `target`.
    private static func toModel(
        _ code: inout Code,
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ owner: String,
        _ wired: String,
        _ source: String,
        _ target: String,
        _ conversion: ModelConversion
    ) {
        switch value.encoding.kind {
        case .union:
            toModelUnion(&code, value, model, modelOwner, owner, wired, target, conversion)
        case .list where model.kind == .poly:
            code.line("\(target) = new GesPoly(\(source));")
        case .list where model.kind == .dice:
            let dice = conversion.local("dice")
            let face = conversion.local("face")
            code.line("var \(dice) = new List<int>(\(source).Count);")
            code.line("foreach (var \(face) in \(source)) \(dice).Add(checked((int)\(face)));")
            code.line("\(target) = \(dice);")
        case .list:
            toModelList(&code, value, model, modelOwner, wired, source, target, conversion)
        case .schema where value.packed == nil:
            code.line("\(target) = \(source).ToModel();")
        case .bytes:
            code.line("\(target) = \(source).ToArray();")
        default:
            code.line("\(target) = \(converted(value, model, modelOwner, source, toModel: true, wired, conversion));")
        }
    }

    // A value that converts by assignment or a cast: numbers, Booleans, tags, flags, texts, geometry and packed records.
    private static func converted(
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ source: String,
        toModel: Bool,
        _ wired: String,
        _ conversion: ModelConversion
    ) -> String {
        switch value.encoding.kind {
        case .integer where model.kind == .number && !model.model.integer:
            return toModel ? "(double)\(source)" : "checked((long)\(source))"
        case .quantized, .float:
            guard model.kind == .number && model.model.integer else { return source }
            return toModel ? "checked((long)\(source))" : "(double)\(source)"
        case .tag, .flags:
            let type = toModel ? trimmed(GesCSharpModelEmitter.type(model, conversion.qualifier, modelOwner)) : (!value.tagType.isEmpty ? typeName(value) : wired + typeName(value))
            return "(\(type))\(source)"
        default:
            return source
        }
    }

    private static func toModelList(
        _ code: inout Code,
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ wired: String,
        _ source: String,
        _ target: String,
        _ conversion: ModelConversion
    ) {
        let element = model.element!
        let type = GesCSharpModelEmitter.type(element, conversion.qualifier, modelOwner)
        if value.item == nil && same(value.element!, element) {
            // Elements of the same type, such as packed records, copy as a block.
            code.line("\(target) = new List<\(type)>(\(source));")
            return
        }
        let list = conversion.local("list")
        let item = conversion.local("item")
        let converted = conversion.local("element")
        code.line("var \(list) = new List<\(type)>(\(source).Count);")
        block(&code, "foreach (var \(item) in \(source))")
        code.line("\(type) \(converted);")
        if let holder = value.item {
            toModel(&code, holder.fields[0].value, element, modelOwner, item, wired + holder.name + ".", "\(item).\(holder.fields[0].value.base)", converted, conversion)
        } else {
            toModel(&code, value.element!, element, modelOwner, item, wired, item, converted, conversion)
        }
        code.line("\(list).Add(\(converted));")
        code.close()
        code.line("\(target) = \(list);")
    }

    // Whether a wire element and a model element have the same type, so that a List of them copies as a block.
    private static func same(_ value: GesPlanValue, _ model: GesModelPlanValue) -> Bool {
        switch value.encoding.kind {
        case .boolean, .point, .vector, .box, .disc: true
        case .integer: model.kind == .number && model.model.integer
        case .quantized, .float: model.kind == .number && !model.model.integer
        case .schema: value.packed != nil
        default: false
        }
    }

    private static func toModelUnion(
        _ code: inout Code,
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ owner: String,
        _ wired: String,
        _ target: String,
        _ conversion: ModelConversion
    ) {
        let selector = "\(wired)\(nestedName(value))"
        block(&code, "switch (\(owner).\(value.base))")
        for alternative in value.alternatives {
            code.line("case \(selector).\(alternative.label):")
            code.indent()
            if alternative.encoding.kind == .nothing {
                code.line("\(target) = null;")
            } else if model.kind != .union {
                toModel(&code, alternative, model, modelOwner, owner, wired, "\(owner).\(alternative.base)", target, conversion)
            } else {
                let present = model.alternatives.first { $0.label == alternative.label }!
                let local = conversion.local("alternative")
                code.open("{")
                code.line("\(GesCSharpModelEmitter.type(present, conversion.qualifier, modelOwner)) \(local);")
                toModel(&code, alternative, present, modelOwner, owner, wired, "\(owner).\(alternative.base)", local, conversion)
                code.line("\(target) = new \(trimmed(GesCSharpModelEmitter.type(model, conversion.qualifier, modelOwner))).\(alternative.label)(\(local));")
                code.close()
            }
            code.line("break;")
            code.dedent()
        }
        code.line("default:")
        code.line("    throw new GesWireException(GesWireException.InvalidIndex, \"\");")
        code.close()
    }

    // Fills a field of `owner` from the model member `source`.
    private static func fromModelField(
        _ code: inout Code,
        _ field: GesPlanField,
        _ model: GesModelPlanField,
        _ modelOwner: String,
        _ owner: String,
        _ wired: String,
        _ source: String,
        _ conversion: ModelConversion
    ) {
        if !field.optional {
            fromModel(&code, field.value, model.value, modelOwner, owner, wired, source, pooled: isPooledList(field), conversion)
            return
        }
        let local = conversion.local("present")
        block(&code, "if (\(source) is { } \(local))")
        code.line("\(owner).Has\(field.base) = true;")
        fromModel(&code, field.value, model.value, modelOwner, owner, wired, local, pooled: false, conversion)
        code.close()
        code.line("else \(owner).Has\(field.base) = false;")
    }

    // Fills the wire member of `value` in `owner` from the model value `source`.
    private static func fromModel(
        _ code: inout Code,
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ owner: String,
        _ wired: String,
        _ source: String,
        pooled: Bool,
        _ conversion: ModelConversion
    ) {
        let member = "\(owner).\(value.base)"
        switch value.encoding.kind {
        case .union:
            fromModelUnion(&code, value, model, modelOwner, owner, wired, source, conversion)
        case .list where model.kind == .poly:
            code.line("\(member).Clear();")
            code.line("\(member).AddRange(\(source).Points);")
        case .list where model.kind == .dice:
            let face = conversion.local("face")
            code.line("\(member).Clear();")
            code.line("foreach (var \(face) in \(source)) \(member).Add(\(face));")
        case .list:
            fromModelList(&code, value, model, modelOwner, owner, wired, source, pooled, conversion)
        case .schema where value.packed == nil:
            code.line("\(member).CopyFrom(\(source));")
        case .bytes:
            code.line("\(owner).Set\(value.base)(\(source));")
        default:
            code.line("\(member) = \(converted(value, model, modelOwner, source, toModel: false, wired, conversion));")
        }
    }

    private static func fromModelList(
        _ code: inout Code,
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ owner: String,
        _ wired: String,
        _ source: String,
        _ pooled: Bool,
        _ conversion: ModelConversion
    ) {
        let member = "\(owner).\(value.base)"
        let element = model.element!
        if value.item == nil && same(value.element!, element) {
            code.line("\(member).Clear();")
            code.line("\(member).AddRange(\(source));")
            return
        }
        let index = conversion.local("index")
        let target = conversion.local("target")
        let wireType = value.item.map { wired + $0.name } ?? wired + elementName(value)
        if pooled {
            code.line("\(owner).Set\(value.base)Count(\(source).Count);")
            block(&code, "for (var \(index) = 0; \(index) < \(source).Count; \(index)++)")
            code.line("var \(target) = \(member)[\(index)];")
        } else {
            code.line("\(member).Clear();")
            block(&code, "for (var \(index) = 0; \(index) < \(source).Count; \(index)++)")
            if value.item != nil || value.element!.encoding.kind == .schema { code.line("var \(target) = new \(wireType)();") }
        }
        let from = "\(source)[\(index)]"
        if let holder = value.item {
            let inner = holder.fields[0]
            fromModel(&code, inner.value, element, modelOwner, target, wired + holder.name + ".", from, pooled: isPooledList(inner), conversion)
            if !pooled { code.line("\(member).Add(\(target));") }
        } else if value.element!.encoding.kind == .schema && value.element!.packed == nil {
            code.line("\(target).CopyFrom(\(from));")
            if !pooled { code.line("\(member).Add(\(target));") }
        } else {
            code.line("\(member).Add(\(converted(value.element!, element, modelOwner, from, toModel: false, wired, conversion)));")
        }
        code.close()
    }

    private static func fromModelUnion(
        _ code: inout Code,
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ owner: String,
        _ wired: String,
        _ source: String,
        _ conversion: ModelConversion
    ) {
        let selector = "\(wired)\(nestedName(value))"
        let nothing = value.alternatives.first { $0.encoding.kind == .nothing }
        let absent =
            nothing.map { "\(owner).\(value.base) = \(selector).\($0.label);" }
            ?? "throw new global::System.ArgumentException(\"The model holds no value of \(value.base).\", nameof(model));"
        if model.kind != .union {
            // One alternative besides :Nothing is a nullable model value.
            let present = value.alternatives.first { $0.encoding.kind != .nothing }!
            let local = conversion.local("present")
            block(&code, "if (\(source) is { } \(local))")
            code.line("\(owner).\(value.base) = \(selector).\(present.label);")
            fromModel(&code, present, model, modelOwner, owner, wired, local, pooled: false, conversion)
            code.close()
            code.line("else \(absent)")
            return
        }
        let union = trimmed(GesCSharpModelEmitter.type(model, conversion.qualifier, modelOwner))
        block(&code, "switch (\(source))")
        for alternative in value.alternatives {
            if alternative.encoding.kind == .nothing { continue }
            let present = model.alternatives.first { $0.label == alternative.label }!
            let local = conversion.local("alternative")
            code.line("case \(union).\(alternative.label) \(local):")
            code.indent()
            code.line("\(owner).\(value.base) = \(selector).\(alternative.label);")
            fromModel(&code, alternative, present, modelOwner, owner, wired, "\(local).Value", pooled: false, conversion)
            code.line("break;")
            code.dedent()
        }
        code.line("default:")
        code.line("    " + absent)
        if nothing != nil { code.line("    break;") }
        code.close()
    }
}
