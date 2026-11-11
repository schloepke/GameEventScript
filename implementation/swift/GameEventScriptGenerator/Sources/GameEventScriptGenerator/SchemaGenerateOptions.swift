// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A target language of generated schema code.
public enum GesSchemaLanguage: Sendable {
    /// C# 9, which Unity compiles.
    case csharp
    /// Swift 6.
    case swift
}

/// The visibility of generated types.
public enum GesSchemaAccess: Sendable {
    /// Visible to every module.
    case `public`
    /// Visible within the module that compiles the generated code.
    case `internal`
}

/// How schema code is generated.
public struct GesSchemaGenerateOptions: Sendable {
    /// The target language.
    public var language: GesSchemaLanguage
    /// The namespace of the generated types, dot-separated identifiers or empty for none. C# declares it as a namespace and
    /// Swift as nested enums, so that generated names cannot collide with types of the app.
    public var namespace: String
    /// The visibility of the generated types.
    public var access: GesSchemaAccess
    /// Whether records also convert to and from GES values and messages, through the schema sources the code embeds and
    /// binds once per type, and whether model types convert to and from the same plain values. The conversion allocates and
    /// is meant for values from scripts and for converting between models and wire records, not the hot path.
    public var values: Bool
    /// Whether generated Swift classes keep the dynamic exclusivity checks of their stored properties. By default they mark
    /// them `@exclusivity(unchecked)`, which halves the time of reading and writing; overlapping accesses to one record, such
    /// as passing a list `inout` while the record reads into it, are then undefined instead of a trap. C# has no such checks,
    /// so this does not change C# output.
    public var checkedExclusivity: Bool
    /// Whether generated model types also offer themselves to scripts as external types: a value conversion per type and a
    /// generated `GesTypes` catalog and constructor registry that implement the Runtime's external-type contracts, without
    /// the Bridge, macros or Reflection. Only model generation takes it.
    public var bindings: Bool

    /// Creates options.
    /// - Parameters:
    ///   - language: The target language.
    ///   - namespace: The namespace, or empty for none.
    ///   - access: The visibility of the generated types.
    ///   - values: Whether records and models also convert to and from GES values, and records to and from messages.
    ///   - checkedExclusivity: Whether generated Swift classes keep dynamic exclusivity checks.
    ///   - bindings: Whether generated model types also offer themselves to scripts as external types.
    public init(language: GesSchemaLanguage = .csharp, namespace: String = "", access: GesSchemaAccess = .public, values: Bool = false, checkedExclusivity: Bool = false, bindings: Bool = false) {
        self.language = language
        self.namespace = namespace
        self.access = access
        self.values = values
        self.checkedExclusivity = checkedExclusivity
        self.bindings = bindings
    }
}

/// A generation request that cannot be carried out, such as a name that two generated members share. Callers branch on
/// `code`, a stable `generate.*` code; the message is informative text.
public struct GesGeneratorError: Error, Sendable, Equatable {
    /// The stable `generate.*` code.
    public let code: String
    /// Informative text, not a portable comparison value.
    public let message: String

    static let usage = "generate.usage"
    static let nameCollision = "generate.nameCollision"
    static let invalidNamespace = "generate.invalidNamespace"
    static let unsupported = "generate.unsupported"

    init(_ code: String, _ message: String) {
        self.code = code
        self.message = message
    }
}
