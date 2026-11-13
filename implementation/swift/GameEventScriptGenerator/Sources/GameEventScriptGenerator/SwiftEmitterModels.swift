// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The direct conversion between a wire record and the model of the same schema, which `--models` names: toModel() builds the
// model, read(model:) fills the record and the model gets init(wire:), field by field without GES values. Both walk the wire
// plan and the model plan of the schema side by side; a packed record is the same type on both sides, so its fields and its
// Lists copy as they are.
extension GesSwiftEmitter {
    // The model layout and the qualifier of its namespace, and a counter for the names of locals.
    final class ModelConversion {
        let layout: GesModelLayout
        let qualifier: String
        // The qualifier of the wire namespace: the conversion extends a wire record at file scope, where a bare name of a
        // record or tag set means the model's when the models live in the global namespace.
        let wireSpace: String
        private var counter = 0

        init(_ layout: GesModelLayout, _ qualifier: String, _ wireSpace: String) {
            self.layout = layout
            self.qualifier = qualifier
            self.wireSpace = wireSpace
        }

        func type(_ name: String) -> GesModelType? { layout.types.first { $0.name == name } }

        func local(_ name: String) -> String {
            defer { counter += 1 }
            return name + String(counter)
        }

        func reset() { counter = 0 }
    }

    // The conversion between a wire record and its model at file scope: an extension of the wire record with toModel() and
    // read(model:), and one of the model with init(wire:). Inside the wire namespace a bare name would mean the wire type.
    static func modelConversionUnit(_ code: inout Code, _ type: GesPlanType, _ model: GesModelType, _ access: String, _ conversion: ModelConversion) throws(GesGeneratorError) {
        let wire = conversion.wireSpace + type.name
        code.line("/// The conversion of the wire record to and from the model of the same schema.")
        code.open("extension \(wire) {")
        try modelMembers(&code, type, model, access, conversion)
        code.close()
        code.blank()
        modelInitializer(&code, conversion.qualifier + model.name, wire, access)
    }

    // The members of a wire record that convert it to and from its model.
    static func modelMembers(_ code: inout Code, _ type: GesPlanType, _ model: GesModelType, _ access: String, _ conversion: ModelConversion) throws(GesGeneratorError) {
        let name = conversion.qualifier + model.name
        let fields = GesModelPlanner.fields(model)
        let check = "precondition(\(name).schemaHash == Self.schemaHash, \"The model type \(model.name) was generated from another state of the schema than this wire record.\")"
        code.line("/// Creates the model of this record, field by field without GES values.")
        code.line("/// - Precondition: The model type was generated from the same state of the schema.")
        code.open("\(access)func toModel() -> \(name) {")
        code.line(check)
        conversion.reset()
        var arguments: [String] = []
        for field in fields {
            guard let wire = type.fields.first(where: { $0.layout.name == field.field.name }) else {
                throw GesGeneratorError(
                    GesGeneratorError.usage,
                    "The field \(field.field.name) of the model :\(model.name) has no counterpart in the wire record; generate the model from the same schema."
                )
            }
            let local = conversion.local("field")
            code.line("let \(local): \(modelType(field.value, model.name, conversion))")
            toModelField(&code, wire, field, model.name, "self", local, conversion)
            arguments.append("\(GesSchemaPlanner.camel(field.base)): \(local)")
        }
        code.line("return \(name)(\(arguments.joined(separator: ", ")))")
        code.close()
        code.blank()
        code.line("/// Fills this record from a model, field by field without GES values; the values are checked when the record is written.")
        code.line("/// - Precondition: The model type was generated from the same state of the schema.")
        code.open("\(access)\(type.isClass ? "" : "mutating ")func read(model: \(name)) {")
        code.line(check)
        conversion.reset()
        for field in type.fields {
            guard let match = fields.first(where: { $0.field.name == field.layout.name }) else {
                throw GesGeneratorError(
                    GesGeneratorError.usage,
                    "The field \(field.layout.name) of :\(type.name) has no counterpart in the model; generate the model from the same schema."
                )
            }
            fromModelField(&code, field, match, model.name, "self", "model.\(member(match.base))", conversion)
        }
        code.close()
    }

    // The model's initializer from a wire record, which extends the model type at file scope.
    static func modelInitializer(_ code: inout Code, _ model: String, _ wire: String, _ access: String) {
        code.line("/// The conversion from the wire record of the same schema.")
        code.open("extension \(model) {")
        code.line("/// Creates the model of a wire record, field by field without GES values.")
        code.line("/// - Precondition: The model type was generated from the same state of the schema.")
        code.line("\(access)init(wire: \(wire)) { self = wire.toModel() }")
        code.close()
    }

    // The type of a model value, qualified so that it resolves from wire code.
    private static func modelType(_ value: GesModelPlanValue, _ owner: String, _ conversion: ModelConversion) -> String {
        let plain: String =
            switch value.kind {
            case .boolean: "Bool"
            case .number: value.model.integer ? "Int" : "Double"
            case .tag: value.closedTag ? modelNested(value, owner, conversion) : "String"
            case .text: "String"
            case .bytes: "[UInt8]"
            case .list: "[\(modelType(value.element!, owner, conversion))]"
            case .dice: "[Int]"
            case .point: "GesPoint"
            case .vector: "GesVector"
            case .box: "GesBox"
            case .poly: "GesPoly"
            case .disc: "GesDisc"
            case .schema: conversion.qualifier + value.model.schema
            default: modelNested(value, owner, conversion)
            }
        return value.nullable ? plain + "?" : plain
    }

    private static func modelNested(_ value: GesModelPlanValue, _ owner: String, _ conversion: ModelConversion) -> String {
        conversion.qualifier + (value.tagSet.isEmpty ? owner + "." + value.nested : value.tagSet)
    }

    private static func wireMember(_ owner: String, _ value: GesPlanValue) -> String { "\(owner).\(member(value.base))" }

    private static func toModelField(
        _ code: inout Code,
        _ field: GesPlanField,
        _ model: GesModelPlanField,
        _ modelOwner: String,
        _ owner: String,
        _ target: String,
        _ conversion: ModelConversion
    ) {
        if !field.optional {
            toModel(&code, field.value, model.value, modelOwner, owner, "", wireMember(owner, field.value), target, conversion)
            return
        }
        code.open("if \(owner).has\(field.base) {")
        toModel(&code, field.value, model.value, modelOwner, owner, "", wireMember(owner, field.value), target, conversion)
        code.close("} else {")
        code.indent()
        code.line("\(target) = nil")
        code.close()
    }

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
            code.line("\(target) = GesPoly(\(source))")
        case .list where model.kind == .dice:
            code.line("\(target) = \(source).map { Int($0) }")
        case .list:
            toModelList(&code, value, model, modelOwner, wired, source, target, conversion)
        case .schema where value.packed == nil:
            code.line("\(target) = \(source).toModel()")
        default:
            code.line("\(target) = \(converted(value, model, modelOwner, source, toModel: true, wired, conversion))")
        }
    }

    // A value that converts by assignment or a conversion: numbers, Booleans, tags, flags, texts, bytes, geometry and packed
    // records. A tag converts by its index, which is its declared position on both sides.
    private static func converted(
        _ value: GesPlanValue,
        _ model: GesModelPlanValue,
        _ modelOwner: String,
        _ source: String,
        toModel: Bool,
        _ wired: String,
        _ conversion: ModelConversion
    ) -> String {
        var modelType = self.modelType(model, modelOwner, conversion)
        while modelType.hasSuffix("?") { modelType.removeLast() }
        let wireType = !value.tagType.isEmpty ? conversion.wireSpace + typeName(value) : wired + typeName(value)
        switch value.encoding.kind {
        case .integer:
            return model.model.integer ? (toModel ? "Int(\(source))" : "Int64(\(source))") : (toModel ? "Double(\(source))" : "Int64(\(source))")
        case .quantized, .float:
            return model.model.integer ? (toModel ? "Int(\(source))" : "Double(\(source))") : source
        case .tag:
            return toModel
                ? "\(modelType).allCases[Int(\(source).rawValue)]"
                : "\(wireType)(rawValue: \(wireType).RawValue(truncatingIfNeeded: \(modelType).allCases.firstIndex(of: \(source))!))!"
        case .flags:
            return toModel ? "\(modelType)(rawValue: \(source).rawValue)" : "\(wireType)(rawValue: \(source).rawValue)"
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
        if value.item == nil && same(value.element!, element) {
            // Elements of the same type, such as packed records, share the array's storage.
            code.line("\(target) = \(source)")
            return
        }
        let type = modelType(element, modelOwner, conversion)
        let list = conversion.local("list")
        let item = conversion.local("item")
        let converted = conversion.local("element")
        code.line("var \(list): [\(type)] = []")
        code.line("\(list).reserveCapacity(\(source).count)")
        code.open("for \(item) in \(source) {")
        code.line("let \(converted): \(type)")
        if let holder = value.item {
            toModel(&code, holder.fields[0].value, element, modelOwner, item, wired + holder.name + ".", wireMember(item, holder.fields[0].value), converted, conversion)
        } else {
            toModel(&code, value.element!, element, modelOwner, item, wired, item, converted, conversion)
        }
        code.line("\(list).append(\(converted))")
        code.close()
        code.line("\(target) = \(list)")
    }

    // Whether a wire element and a model element have the same type, so that a List of them is the same array.
    private static func same(_ value: GesPlanValue, _ model: GesModelPlanValue) -> Bool {
        switch value.encoding.kind {
        case .boolean, .point, .vector, .box, .disc: true
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
        code.line("switch \(wireMember(owner, value)) {")
        for alternative in value.alternatives {
            code.line("case .\(member(alternative.label)):")
            code.indent()
            if alternative.encoding.kind == .nothing {
                code.line("\(target) = nil")
            } else if model.kind != .union {
                toModel(&code, alternative, model, modelOwner, owner, wired, wireMember(owner, alternative), target, conversion)
            } else {
                let present = model.alternatives.first { $0.label == alternative.label }!
                let local = conversion.local("alternative")
                code.line("let \(local): \(modelType(present, modelOwner, conversion))")
                toModel(&code, alternative, present, modelOwner, owner, wired, wireMember(owner, alternative), local, conversion)
                code.line("\(target) = .\(member(alternative.label))(\(local))")
            }
            code.dedent()
        }
        code.line("}")
    }

    private static func fromModelField(
        _ code: inout Code,
        _ field: GesPlanField,
        _ model: GesModelPlanField,
        _ modelOwner: String,
        _ owner: String,
        _ source: String,
        _ conversion: ModelConversion
    ) {
        if !field.optional {
            fromModel(&code, field.value, model.value, modelOwner, owner, "", source, GesCSharpEmitter.isPooledList(field), conversion)
            return
        }
        let local = conversion.local("present")
        code.open("if let \(local) = \(source) {")
        code.line("\(owner).has\(field.base) = true")
        fromModel(&code, field.value, model.value, modelOwner, owner, "", local, false, conversion)
        code.close("} else {")
        code.indent()
        code.line("\(owner).has\(field.base) = false")
        code.close()
    }

    private static func fromModel(
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
        let member = wireMember(owner, value)
        switch value.encoding.kind {
        case .union:
            fromModelUnion(&code, value, model, modelOwner, owner, wired, source, conversion)
        case .list where model.kind == .poly:
            code.line("\(member) = \(source).points")
        case .list where model.kind == .dice:
            code.line("\(member) = \(source).map { Int64($0) }")
        case .list:
            fromModelList(&code, value, model, modelOwner, owner, wired, source, pooled, conversion)
        case .schema where value.packed == nil:
            code.line("\(member).read(model: \(source))")
        default:
            code.line("\(member) = \(converted(value, model, modelOwner, source, toModel: false, wired, conversion))")
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
        let member = wireMember(owner, value)
        let element = model.element!
        if value.item == nil && same(value.element!, element) {
            code.line("\(member) = \(source)")
            return
        }
        let index = conversion.local("index")
        let target = conversion.local("target")
        let wireType = value.item.map { wired + $0.name } ?? conversion.wireSpace + elementName(value)
        let from = "\(source)[\(index)]"
        let holder = value.item
        let record = holder == nil && value.element!.encoding.kind == .schema && value.element!.packed == nil
        if holder == nil && !record {
            let each = conversion.local("each")
            code.line("\(member) = \(source).map { \(each) in \(converted(value.element!, element, modelOwner, each, toModel: false, wired, conversion)) }")
            return
        }
        if pooled {
            code.line("\(owner).set\(value.base)Count(\(source).count)")
            code.open("for \(index) in 0..<\(source).count {")
            code.line("\(value.element!.isClass ? "let" : "var") \(target) = \(member)[\(index)]")
        } else {
            let list = conversion.local("list")
            code.line("var \(list): [\(wireType)] = []")
            code.line("\(list).reserveCapacity(\(source).count)")
            code.open("for \(index) in 0..<\(source).count {")
            code.line("\(value.element!.isClass ? "let" : "var") \(target) = \(wireType)()")
            if let holder {
                fromModel(&code, holder.fields[0].value, element, modelOwner, target, wired + holder.name + ".", from, GesCSharpEmitter.isPooledList(holder.fields[0]), conversion)
            } else {
                code.line("\(target).read(model: \(from))")
            }
            code.line("\(list).append(\(target))")
            code.close()
            code.line("\(member) = \(list)")
            return
        }
        if let holder {
            fromModel(&code, holder.fields[0].value, element, modelOwner, target, wired + holder.name + ".", from, GesCSharpEmitter.isPooledList(holder.fields[0]), conversion)
        } else {
            code.line("\(target).read(model: \(from))")
        }
        if !value.element!.isClass { code.line("\(member)[\(index)] = \(target)") }
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
        let member = wireMember(owner, value)
        let nothing = value.alternatives.first { $0.encoding.kind == .nothing }
        if model.kind != .union {
            // One alternative besides :Nothing is a nullable model value.
            let present = value.alternatives.first { $0.encoding.kind != .nothing }!
            let local = conversion.local("present")
            code.open("if let \(local) = \(source) {")
            code.line("\(member) = .\(self.member(present.label))")
            fromModel(&code, present, model, modelOwner, owner, wired, local, false, conversion)
            code.close("} else {")
            code.indent()
            code.line("\(member) = .\(self.member(nothing!.label))")
            code.close()
            return
        }
        let optional = model.nullable ? "?" : ""
        code.line("switch \(source) {")
        for alternative in value.alternatives {
            if alternative.encoding.kind == .nothing { continue }
            let present = model.alternatives.first { $0.label == alternative.label }!
            let local = conversion.local("alternative")
            code.line("case .\(self.member(alternative.label))(let \(local))\(optional):")
            code.indent()
            code.line("\(member) = .\(self.member(alternative.label))")
            fromModel(&code, alternative, present, modelOwner, owner, wired, local, false, conversion)
            code.dedent()
        }
        if model.nullable {
            code.line("case nil:")
            code.line("    \(member) = .\(self.member(nothing!.label))")
        }
        code.line("}")
    }
}
