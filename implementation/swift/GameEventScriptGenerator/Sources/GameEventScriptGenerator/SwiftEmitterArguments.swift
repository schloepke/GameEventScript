// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// The members of a message with argument defaults: an initializer whose parameters take the defaults, and with values the
// lenient read. A message without an argument default generates neither, so its code stays as it was.
extension GesSwiftEmitter {
    // The lenient read, which fills left-out arguments as GesSchemaValues.completeMessage does and then reads the message.
    static func lenientMember(_ code: inout Code, _ access: String, _ mutating: String) {
        code.blank()
        code.line("/// Reads a message that may leave out arguments with a schema default and optional ones, as `GesSchemaValues.completeMessage` completes it; this allocates.")
        code.line("/// - Throws: `GesWireError` when an argument without a default is missing, a label is unknown, repeated or out of order, or the message does not fit, or the error of binding the embedded sources.")
        code.open("\(access)\(mutating)func read(lenientMessage message: GameEventScriptMessage) throws {")
        code.line("try read(message: GesSchemaValues.completeMessage(Self.__valueSchema.get(), message))")
        code.close()
    }

    // An argument the initializer takes: a value without union, Bytes or List, whose default has a Swift literal.
    static func takes(_ field: GesPlanField) -> Bool {
        let kind = field.value.encoding.kind
        if kind == .union || kind == .bytes || kind == .list { return false }
        guard let fallback = field.layout.defaultValue else { return true }
        return argumentDefault(field.value, fallback) != nil
    }

    // The Swift literal of an argument default, nil when the argument's type has none.
    static func argumentDefault(_ value: GesPlanValue, _ fallback: GesLayoutDefault) -> String? {
        switch (fallback.kind, value.encoding.kind) {
        case (.boolean, .boolean): return fallback.boolean ? "true" : "false"
        case (.tag, .tag): return "." + name(fallback.value)
        case (.number, .integer), (.number, .quantized), (.number, .float): return GesCSharpEmitter.argumentNumber(fallback.value, integer: value.encoding.kind == .integer)
        default: return nil
        }
    }

    // The initializer of a message with argument defaults, whose arguments are all values it can take.
    static func argumentInitializer(_ code: inout Code, _ type: GesPlanType, _ access: String) {
        if type.isItem || !GesCSharpEmitter.hasArgumentDefaults(type.layout) || !type.fields.allSatisfy(takes) { return }
        var parameters: [String] = []
        for field in type.fields {
            let typeName = typeName(field.value)
            if field.optional {
                parameters.append("\(member(field.base)): \(typeName)? = nil")
            } else if let fallback = field.layout.defaultValue {
                parameters.append("\(member(field.base)): \(typeName) = \(argumentDefault(field.value, fallback)!)")
            } else {
                parameters.append("\(member(field.base)): \(typeName)")
            }
        }
        code.blank()
        code.line("/// Creates a \(kind(type)) from its arguments; a left-out parameter takes the argument's schema default, an optional argument is absent when nil.")
        code.open("\(access)init(\(parameters.joined(separator: ", "))) {")
        for field in type.fields {
            let name = member(field.base)
            if !field.optional {
                code.line("self.\(name) = \(name)")
            } else {
                code.line("if let \(name) { self.has\(field.base) = true; self.\(name) = \(name) }")
            }
        }
        code.close()
    }
}
