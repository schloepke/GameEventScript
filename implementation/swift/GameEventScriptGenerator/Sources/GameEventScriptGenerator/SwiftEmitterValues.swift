// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The conversion of generated Swift types to and from GES values, as in the C# output.
extension GesSwiftEmitter {
    static func valueMembers(_ code: inout Code, _ type: GesPlanType, _ access: String, _ holder: String) {
        let mutating = (type.kind == .messages ? type.membersAreClasses : type.isClass) ? "" : "mutating "
        code.blank()
        code.line("private static let __valueSchema = \(holder).bind(\(GesSchemaPlanner.literal(type.layout.name, swift: true)), bindingHash)")
        code.blank()
        if type.kind == .schema {
            code.line("/// Converts this record into a GES Record value, as `GesSchemaValues` decodes its bytes; this allocates.")
            code.line("/// - Throws: `GesWireError` when a value does not fit its field, or the error of binding the embedded sources.")
            code.open("\(access)func toValue() throws -> GesValue {")
            code.line("try GesSchemaValues.decode(Self.__valueSchema.get(), encoded()).value")
            code.close()
            code.blank()
            code.line("/// Reads a Record or Map into this instance, as `GesSchemaValues` encodes it; this allocates.")
            code.line("/// - Throws: `GesWireError` when the value does not fit the schema, or the error of binding the embedded sources.")
            code.open("\(access)\(mutating)func read(value: GesValue) throws {")
            code.line("_ = try read(from: GesSchemaValues.encode(Self.__valueSchema.get(), value))")
            code.close()
            return
        }
        code.line("/// Converts this record into a message, as `GesSchemaValues` decodes its bytes; this allocates.")
        code.line("/// - Throws: `GesWireError` when a value does not fit its field, or the error of binding the embedded sources.")
        code.open("\(access)func toMessage() throws -> GameEventScriptMessage {")
        code.line("try GesSchemaValues.decode(Self.__valueSchema.get(), encoded()).value.messageValue!")
        code.close()
        code.blank()
        code.line("/// Reads a message into this instance, as `GesSchemaValues` encodes it; this allocates.")
        code.line("/// - Throws: `GesWireError` when the message does not fit, or the error of binding the embedded sources.")
        code.open("\(access)\(mutating)func read(message: GameEventScriptMessage) throws {")
        code.line("_ = try read(from: GesSchemaValues.encode(Self.__valueSchema.get(), .message(message)))")
        code.close()
    }

    // The embedded sources, parsed when a type first converts a value; a different binding hash fails each conversion.
    static func valuesHolder(_ code: inout Code, _ layout: GesSchemaLayout, _ holder: String) {
        code.blank()
        code.line("/// The schema sources of this file, which the conversion to and from GES values binds once per type.")
        code.open("private enum \(holder) {")
        code.line("/// The embedded sources no longer bind like the generated code.")
        code.line("struct Mismatch: Error {}")
        code.blank()
        code.open("static let set = Result<GesSchemaSet, any Error> {")
        code.open("try GesSchemaSet(sources: [")
        for source in layout.sources {
            code.open("GesSchemaSource(name: \(GesSchemaPlanner.literal(source.name, swift: true)), text: [")
            for line in GesSchemaPlanner.lines(source.text) { code.line(GesSchemaPlanner.literal(line, swift: true) + ",") }
            code.close("].joined(separator: \"\\n\")),")
        }
        code.close("])")
        code.close()
        code.blank()
        code.open("static func bind(_ root: String, _ bindingHash: UInt64) -> Result<GesBoundSchema, any Error> {")
        code.open("Result {")
        code.line("let parameters = GesSchemaParameters()")
        for parameter in layout.parameters {
            code.line("try parameters.with(\(GesSchemaPlanner.literal(parameter.name, swift: true)), literal: \(GesSchemaPlanner.literal(parameter.literal, swift: true)))")
        }
        code.line("let bound = try set.get().schema(root).bind(parameters: parameters)")
        code.line("if bound.bindingHash != bindingHash { throw Mismatch() }")
        code.line("return bound")
        code.close()
        code.close()
        code.close()
    }
}
