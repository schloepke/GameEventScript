// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// The members of a message with argument defaults: an initializer whose parameters take the defaults, and with values the
// lenient read. A message without an argument default generates neither, so its code stays as it was.
extension GesCSharpEmitter {
    // Whether a message declares an argument default.
    static func hasArgumentDefaults(_ layout: GesLayoutType) -> Bool { layout.kind == .message && layout.fields.contains { $0.defaultValue != nil } }

    // Whether a message, a message set's parameter or a member of a message set declares an argument default.
    static func lenient(_ type: GesLayoutType, _ layout: GesSchemaLayout) -> Bool {
        guard type.kind == .messages else { return hasArgumentDefaults(type) }
        return type.fields.contains { $0.defaultValue != nil } || type.members.contains { member in layout.types.first { $0.name == member }.map(hasArgumentDefaults) ?? false }
    }

    // The lenient read, which fills left-out arguments as GesSchemaValues.CompleteMessage does and then reads the message.
    static func lenientMember(_ code: inout Code, _ bind: String) {
        code.blank()
        code.line("/// <summary>Reads a message that may leave out arguments with a schema default and optional ones, as <see cref=\"GesSchemaValues.CompleteMessage\"/> completes it; this allocates.</summary>")
        code.line("/// <param name=\"message\">The message, whose arguments follow the declared order.</param>")
        code.line("/// <exception cref=\"GesWireException\">An argument without a default is missing, a label is unknown, repeated or out of order, or the message does not fit.</exception>")
        code.line("public void ReadMessageLenient(GameEventScriptMessage message) => ReadMessage(GesSchemaValues.CompleteMessage(\(bind), message));")
    }

    // An argument the initializer takes: a value without union, Bytes or List, whose default has a C# literal.
    static func takes(_ field: GesPlanField) -> Bool {
        let kind = field.value.encoding.kind
        if kind == .union || kind == .bytes || kind == .list { return false }
        guard let fallback = field.layout.defaultValue else { return true }
        return argumentDefault(field.value, fallback) != nil
    }

    // The C# literal of an argument default, nil when the argument's type has none.
    static func argumentDefault(_ value: GesPlanValue, _ fallback: GesLayoutDefault) -> String? {
        switch (fallback.kind, value.encoding.kind) {
        case (.boolean, .boolean): return fallback.boolean ? "true" : "false"
        case (.tag, .tag): return nestedName(value) + "." + GesSchemaPlanner.pascal(fallback.value)
        case (.number, .integer), (.number, .quantized), (.number, .float): return argumentNumber(fallback.value, integer: value.encoding.kind == .integer)
        default: return nil
        }
    }

    // A canonical decimal as an integer or a floating-point literal.
    static func argumentNumber(_ canonical: String, integer: Bool) -> String {
        let e = canonical.firstIndex(of: "e")!
        var significand = Int64(canonical[..<e])!
        let exponent = Int(canonical[canonical.index(after: e)...])!
        if !integer { return GesSchemaPlanner.decimal(significand, exponent) }
        for _ in 0..<max(exponent, 0) { significand *= 10 }
        return String(significand)
    }

    // The parameter name of an argument.
    static func argumentParameter(_ field: GesPlanField) -> String {
        let name = GesSchemaPlanner.camel(field.base)
        return keywords.contains(name) ? "@" + name : name
    }

    // The initializer of a message with argument defaults, whose arguments are all values it can take. C# allows a default
    // only on trailing parameters, so an argument default counts there when every later argument has one or is optional.
    static func argumentInitializer(_ code: inout Code, _ type: GesPlanType) {
        if type.isItem || !hasArgumentDefaults(type.layout) || !type.fields.allSatisfy(takes) { return }
        var trailing = type.fields.count
        while trailing > 0 && (type.fields[trailing - 1].optional || type.fields[trailing - 1].layout.defaultValue != nil) { trailing -= 1 }
        var parameters: [String] = []
        for (index, field) in type.fields.enumerated() {
            let name = argumentParameter(field)
            let typeName = typeName(field.value)
            if field.optional {
                parameters.append("\(typeName)? \(name)\(index >= trailing ? " = null" : "")")
            } else if let fallback = field.layout.defaultValue, index >= trailing {
                parameters.append("\(typeName) \(name) = \(argumentDefault(field.value, fallback)!)")
            } else {
                parameters.append("\(typeName) \(name)")
            }
        }
        if type.isClass {
            code.blank()
            code.line("/// <summary>Creates a \(kind(type)) with every field at its zero value.</summary>")
            code.line("public \(type.name)()")
            code.open("{")
            code.close()
        }
        code.blank()
        code.line("/// <summary>Creates a \(kind(type)) from its arguments; a trailing parameter takes the argument's schema default, an optional argument is absent when null.</summary>")
        for field in type.fields { code.line("/// <param name=\"\(GesSchemaPlanner.camel(field.base))\">The argument <c>\(field.layout.name)</c>.</param>") }
        code.line("public \(type.name)(\(parameters.joined(separator: ", ")))\(type.isClass ? "" : " : this()")")
        code.open("{")
        for field in type.fields {
            let name = argumentParameter(field)
            if !field.optional {
                code.line("\(field.base) = \(name);")
            } else if plain(field.value) {
                code.line("\(field.base)Optional = \(name);")
            } else {
                code.line("if (\(name) is not null) { Has\(field.base) = true; \(field.base) = \(name); }")
            }
        }
        code.close()
    }
}
