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

    /// Creates options.
    /// - Parameters:
    ///   - language: The target language.
    ///   - namespace: The namespace, or empty for none.
    ///   - access: The visibility of the generated types.
    public init(language: GesSchemaLanguage = .csharp, namespace: String = "", access: GesSchemaAccess = .public) {
        self.language = language
        self.namespace = namespace
        self.access = access
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
    static let unsupported = "generate.unsupported"
    static let nameCollision = "generate.nameCollision"
    static let invalidNamespace = "generate.invalidNamespace"

    init(_ code: String, _ message: String) {
        self.code = code
        self.message = message
    }
}
