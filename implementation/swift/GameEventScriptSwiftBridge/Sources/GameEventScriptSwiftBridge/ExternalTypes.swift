// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One declared field and a typed Swift getter. Getters run synchronously under the owning Host.
public struct GesBridgeFieldBinding<Root> {
    /// Portable field declaration independent of the executable Swift getter.
    public let definition: GesExternalTypeFieldDefinition
    let read: (Root) throws -> GesValue

    /// Binds a field to a typed KeyPath using strict native value conversion.
    ///
    /// - Throws: An API error for invalid field/type names; getter conversion runs later during access.
    public init<Value: GesBridgeValueConvertible>(_ name: String, typeName: String, keyPath: KeyPath<Root, Value>) throws {
        definition = try .init(name: name, typeName: typeName)
        read = { try $0[keyPath: keyPath].toGesValue() }
    }

    /// Binds a source type declaration to a synchronous throwing getter.
    ///
    /// - Throws: An API error for invalid field/type names.
    public init(_ name: String, typeName: String, get: @escaping (Root) throws -> GesValue) throws {
        definition = try .init(name: name, typeName: typeName)
        read = get
    }

    /// Binds a built-in field kind and optional unit to a synchronous throwing getter.
    ///
    /// - Throws: An API error for invalid names or unsupported kind/unit combinations.
    public init(_ name: String, kind: GesBytecodeTypeKind, unit: GesQuantityUnit = .none, get: @escaping (Root) throws -> GesValue) throws {
        definition = try .init(name: name, kind: kind, unit: unit)
        read = get
    }
}

/// A constructor's labels refer to declared fields; the callback receives values in this order.
public struct GesBridgeConstructorBinding<Root> {
    let parameters: [String]
    let create: (GesValueArguments) throws -> Root

    /// Retains ordered parameter labels and a factory. Labels are validated against fields when the owning type
    /// descriptor is created; borrowed callback arguments must not escape.
    public init(parameters: [String] = [], create: @escaping (GesValueArguments) throws -> Root) {
        self.parameters = parameters
        self.create = create
    }
}

/// Immutable explicit Swift binding, reusable for the compiler catalog and runtime registry.
/// Struct roots keep Swift value semantics; class roots retain their original identity and remain host-owned.
public final class GesBridgeTypeBinding<Root> {
    /// Immutable portable declaration for the compiler catalog and runtime linker.
    public let definition: GesExternalTypeDefinition
    private let fields: [String: GesBridgeFieldBinding<Root>]
    private let constructors: [GesBridgeConstructorBinding<Root>]

    /// Validates and indexes fields and constructor bindings for one native type.
    ///
    /// - Throws: An API error for invalid names, duplicate declarations or constructor labels without matching fields.
    public init(_ name: String, fields: [GesBridgeFieldBinding<Root>], constructors: [GesBridgeConstructorBinding<Root>] = []) throws {
        var byName: [String: GesBridgeFieldBinding<Root>] = [:]
        for field in fields { guard byName.updateValue(field, forKey: field.definition.name) == nil else { throw GesApiError.invalidArgument("Duplicate external field: " + field.definition.name) } }
        let definitions = try constructors.map { constructor in
            let parameters = try constructor.parameters.map { label in
                let normalized = try GesExternalTypeParameterDefinition(name: label, typeName: "Text").name
                guard let field = byName[normalized]?.definition else { throw GesApiError.invalidArgument("Constructor parameter has no field: " + normalized) }
                if let kind = field.kind { return try GesExternalTypeParameterDefinition(name: normalized, kind: kind, unit: field.unit) }
                return try GesExternalTypeParameterDefinition(name: normalized, typeName: field.typeName)
            }
            return try GesExternalTypeConstructorDefinition(typeName: name, parameters: parameters)
        }
        definition = try .init(name: name, fields: fields.map(\.definition), constructors: definitions)
        self.fields = byName
        self.constructors = constructors
    }

    /// Type-erased registration retains this descriptor, its getters and factories.
    public var binding: GesBridgeAnyTypeBinding { .init(definition: definition, constructors: zip(definition.constructors, constructors).map { GesBridgeConstructor(definition: $0.0, type: self, create: $0.1.create) }) }

    /// Wraps a native value using this descriptor; structs retain value semantics and class roots retain their native
    /// object identity.
    public func wrap(_ value: Root) -> GesValue { .external(GesBridgeExternalValue(value, type: self)) }

    /// Only values created by this exact descriptor may be unwrapped.
    public func unwrap(_ value: GesValue) throws -> Root {
        guard let box = value.externalValue as? GesBridgeExternalValue<Root>, box.type === self else { throw GesBridgeConversionError.typeMismatch(expected: definition.name, actual: value.customTypeName ?? String(describing: value.valueKind)) }
        return box.value
    }

    fileprivate func read(_ value: Root, field: String) throws -> GesValue? { try fields[field]?.read(value) }
}

/// Heterogeneous registry entry, produced by a typed descriptor rather than an unchecked constructor.
public struct GesBridgeAnyTypeBinding {
    /// Portable declaration associated with the retained executable bindings.
    public let definition: GesExternalTypeDefinition
    fileprivate let constructors: [any GesExternalTypeConstructor]
}

/// One validated catalog for compilation and constructor registry for execution; no Compiler dependency.
public struct GesBridgeExternalTypeRegistry: GesExternalTypeCatalog, GesExternalTypeRegistry {
    private let catalog: GesDeclaredExternalTypeCatalog
    private let constructors: [String: any GesExternalTypeConstructor]
    /// Portable type declarations in registration order.
    public var types: [GesExternalTypeDefinition] { catalog.types }

    /// Builds a compiler catalog and runtime constructor index from explicit bindings.
    ///
    /// - Throws: An API error for duplicate type or constructor identities.
    public init(_ bindings: [GesBridgeAnyTypeBinding]) throws {
        catalog = try .init(bindings.map(\.definition))
        var constructors: [String: any GesExternalTypeConstructor] = [:]
        for binding in bindings {
            for constructor in binding.constructors { guard constructors.updateValue(constructor, forKey: constructor.definition.signatureID) == nil else { throw GesApiError.invalidArgument("Duplicate external constructor") } }
        }
        self.constructors = constructors
    }

    /// Returns a type declaration for a normalized name or an executable constructor for an exact reference, otherwise
    /// nil. The type-name overload throws for invalid identifiers.
    public func resolve(_ typeName: String) throws -> GesExternalTypeDefinition? { try catalog.resolve(typeName) }

    /// Returns a type declaration for a normalized name or an executable constructor for an exact reference, otherwise
    /// nil. The type-name overload throws for invalid identifiers.
    public func resolve(_ reference: GesExternalTypeConstructorReference) -> (any GesExternalTypeConstructor)? { constructors[reference.signatureID] }
}

private final class GesBridgeExternalValue<Root>: GesExternalValue {
    let value: Root
    let type: GesBridgeTypeBinding<Root>
    var definition: GesExternalTypeDefinition { type.definition }

    init(_ value: Root, type: GesBridgeTypeBinding<Root>) {
        self.value = value
        self.type = type
    }

    func field(_ name: String) throws -> GesValue? { try type.read(value, field: name) }
}

private struct GesBridgeConstructor<Root>: GesExternalTypeConstructor {
    let definition: GesExternalTypeConstructorDefinition
    let type: GesBridgeTypeBinding<Root>
    let create: (GesValueArguments) throws -> Root

    func invoke(_ call: GesExternalTypeConstructorCall) throws { try call.setExternalValue(GesBridgeExternalValue(create(call.arguments), type: type)) }
}
