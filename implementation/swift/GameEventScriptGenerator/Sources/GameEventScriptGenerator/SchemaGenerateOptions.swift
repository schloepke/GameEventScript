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

/// How generated code is divided into files.
public enum GesSchemaSplit: Sendable {
    /// One file with every generated type.
    case none
    /// A file per declaration, named after it, and the shared part in a file whose name begins with `_`.
    case schema
    /// A file per schema source with every type it declares, and the shared part in a file whose name begins with `_`.
    case file
}

/// A generated file: its name within the output directory and its text.
public struct GesGeneratedFile: Sendable, Equatable {
    /// The file name, relative to the output directory.
    public let path: String
    /// The text, ending with a line break.
    public let text: String

    init(_ path: String, _ text: String) {
        self.path = path
        self.text = text
    }
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
    /// Whether generated model roots also read GES document text: each root an `init(document:)` (C# `FromDocument`) that
    /// requires the header `:Schema(:Name)`, checks the document against the embedded schema sources, applies defaults and
    /// converts, and one type per generation that reads any root by its header, `Document` inside the namespace and
    /// `ModelDocument` without one. It includes `values`.
    /// The code reads text and does no file I/O. Only model generation takes it.
    public var documents: Bool
    /// How the code is divided into files. `.none`, the default, is the single text of `generate` and `generateModels`; the
    /// other choices are the files of `generateFiles` and `generateModelFiles`.
    public var split: GesSchemaSplit
    /// The namespace of the model code that wire code shares types with, or nil for none, the default. With it, wire code
    /// uses that namespace's packed types instead of declaring its own, so a List of them is one array in both, and converts
    /// each wire record directly to and from the model of the same schema that a model root of any group reaches. An empty
    /// string is the global namespace. Only wire generation takes it.
    public var models: String?
    /// The leading part of the Swift `namespace` that another generation of the module declares, or nil for none, the
    /// default. This generation extends it instead of declaring it again: with the namespace `Game.Panels` and `Game` here it
    /// writes `extension Game { enum Panels { … } }`, and with `Game` for both it writes `extension Game { … }`. It must be a
    /// leading part of the namespace, else `generate.usage`. Wire code with `models` extends the part it shares with the
    /// models' namespace on its own. C# merges namespaces, so C# generation rejects it with `generate.usage`.
    public var extendNamespace: String?

    /// Creates options.
    /// - Parameters:
    ///   - language: The target language.
    ///   - namespace: The namespace, or empty for none.
    ///   - access: The visibility of the generated types.
    ///   - values: Whether records and models also convert to and from GES values, and records to and from messages.
    ///   - checkedExclusivity: Whether generated Swift classes keep dynamic exclusivity checks.
    ///   - bindings: Whether generated model types also offer themselves to scripts as external types.
    ///   - documents: Whether generated model roots also read GES document text.
    ///   - split: How the code is divided into files.
    ///   - models: The namespace of the model code that wire code shares types with, or nil for none.
    ///   - extendNamespace: The leading part of the Swift namespace that another generation of the module declares, or nil.
    public init(
        language: GesSchemaLanguage = .csharp,
        namespace: String = "",
        access: GesSchemaAccess = .public,
        values: Bool = false,
        checkedExclusivity: Bool = false,
        bindings: Bool = false,
        documents: Bool = false,
        split: GesSchemaSplit = .none,
        models: String? = nil,
        extendNamespace: String? = nil
    ) {
        self.language = language
        self.namespace = namespace
        self.access = access
        self.values = values
        self.checkedExclusivity = checkedExclusivity
        self.bindings = bindings
        self.documents = documents
        self.split = split
        self.models = models
        self.extendNamespace = extendNamespace
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
