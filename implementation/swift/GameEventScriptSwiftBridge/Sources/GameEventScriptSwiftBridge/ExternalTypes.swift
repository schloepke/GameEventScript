// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One declared field and a typed Swift getter. Getters run synchronously under the owning Host.
public struct GameEventScriptSwiftField<Root> {
    public let definition: GameEventScriptExternalTypeFieldDefinition
    let read: (Root) throws -> GesValue

    public init<Value: GameEventScriptSwiftValueConvertible>(
        _ name: String, typeName: String, keyPath: KeyPath<Root, Value>
    ) throws {
        definition = try .init(name: name, typeName: typeName)
        read = { try $0[keyPath: keyPath].toGesValue() }
    }
    public init(
        _ name: String, typeName: String, get: @escaping (Root) throws -> GesValue
    ) throws {
        definition = try .init(name: name, typeName: typeName)
        read = get
    }
    public init(
        _ name: String, kind: GameEventScriptBytecodeTypeKind, unit: GesUnit = .none,
        get: @escaping (Root) throws -> GesValue
    ) throws {
        definition = try .init(name: name, kind: kind, unit: unit)
        read = get
    }
}

/// A constructor's labels refer to declared fields; the callback receives values in this order.
public struct GameEventScriptSwiftConstructor<Root> {
    let parameters: [String]
    let create: (GesValueArguments) throws -> Root
    public init(parameters: [String] = [], create: @escaping (GesValueArguments) throws -> Root) {
        self.parameters = parameters
        self.create = create
    }
}

/// Immutable explicit Swift binding, reusable for the compiler catalog and runtime registry.
/// Struct roots keep Swift value semantics; class roots retain their original identity and remain host-owned.
public final class GameEventScriptSwiftType<Root> {
    public let definition: GameEventScriptExternalTypeDefinition
    private let fields: [String: GameEventScriptSwiftField<Root>]
    private let constructors: [GameEventScriptSwiftConstructor<Root>]

    public init(
        _ name: String, fields: [GameEventScriptSwiftField<Root>],
        constructors: [GameEventScriptSwiftConstructor<Root>] = []
    ) throws {
        var byName: [String: GameEventScriptSwiftField<Root>] = [:]
        for field in fields {
            guard byName.updateValue(field, forKey: field.definition.name) == nil else {
                throw GameEventScriptAPIError.invalidArgument("Duplicate external field: " + field.definition.name)
            }
        }
        let definitions = try constructors.map { constructor in
            let parameters = try constructor.parameters.map { label in
                let normalized = try GameEventScriptExternalTypeParameterDefinition(name: label, typeName: "Text").name
                guard let field = byName[normalized]?.definition else {
                    throw GameEventScriptAPIError.invalidArgument("Constructor parameter has no field: " + normalized)
                }
                if let kind = field.kind {
                    return try GameEventScriptExternalTypeParameterDefinition(
                        name: normalized, kind: kind, unit: field.unit)
                }
                return try GameEventScriptExternalTypeParameterDefinition(name: normalized, typeName: field.typeName)
            }
            return try GameEventScriptExternalTypeConstructorDefinition(typeName: name, parameters: parameters)
        }
        definition = try .init(name: name, fields: fields.map(\.definition), constructors: definitions)
        self.fields = byName
        self.constructors = constructors
    }

    /// Type-erased registration retains this descriptor, its getters and factories.
    public var binding: GameEventScriptSwiftTypeBinding {
        .init(
            definition: definition,
            constructors: zip(definition.constructors, constructors).map {
                SwiftConstructor(definition: $0.0, type: self, create: $0.1.create)
            })
    }
    public func wrap(_ value: Root) -> GesValue { .external(SwiftExternalValue(value, type: self)) }

    /// Only values created by this exact descriptor may be unwrapped.
    public func unwrap(_ value: GesValue) throws -> Root {
        guard let box = value.externalValue as? SwiftExternalValue<Root>, box.type === self else {
            throw GameEventScriptSwiftConversionError.typeMismatch(
                expected: definition.name, actual: value.customTypeName ?? String(describing: value.kind))
        }
        return box.value
    }
    fileprivate func read(_ value: Root, field: String) throws -> GesValue? { try fields[field]?.read(value) }
}

/// Heterogeneous registry entry, produced by a typed descriptor rather than an unchecked constructor.
public struct GameEventScriptSwiftTypeBinding {
    public let definition: GameEventScriptExternalTypeDefinition
    fileprivate let constructors: [any GameEventScriptExternalTypeConstructor]
}

/// One validated catalog for compilation and constructor registry for execution; no Compiler dependency.
public struct GameEventScriptSwiftExternalTypeRegistry: GameEventScriptExternalTypeCatalogProtocol,
    GameEventScriptExternalTypeRegistry
{
    private let catalog: GameEventScriptExternalTypeCatalog
    private let constructors: [String: any GameEventScriptExternalTypeConstructor]
    public var types: [GameEventScriptExternalTypeDefinition] { catalog.types }
    public init(_ bindings: [GameEventScriptSwiftTypeBinding]) throws {
        catalog = try .init(bindings.map(\.definition))
        var constructors: [String: any GameEventScriptExternalTypeConstructor] = [:]
        for binding in bindings {
            for constructor in binding.constructors {
                guard constructors.updateValue(constructor, forKey: constructor.definition.signatureID) == nil else {
                    throw GameEventScriptAPIError.invalidArgument("Duplicate external constructor")
                }
            }
        }
        self.constructors = constructors
    }
    public func resolve(_ typeName: String) throws -> GameEventScriptExternalTypeDefinition? {
        try catalog.resolve(typeName)
    }
    public func resolve(_ reference: GameEventScriptExternalTypeConstructorReference) -> (
        any GameEventScriptExternalTypeConstructor
    )? {
        constructors[reference.signatureID]
    }
}

private final class SwiftExternalValue<Root>: GameEventScriptExternalValue {
    let value: Root
    let type: GameEventScriptSwiftType<Root>
    var definition: GameEventScriptExternalTypeDefinition { type.definition }
    init(_ value: Root, type: GameEventScriptSwiftType<Root>) {
        self.value = value
        self.type = type
    }
    func field(_ name: String) throws -> GesValue? { try type.read(value, field: name) }
}
private struct SwiftConstructor<Root>: GameEventScriptExternalTypeConstructor {
    let definition: GameEventScriptExternalTypeConstructorDefinition
    let type: GameEventScriptSwiftType<Root>
    let create: (GesValueArguments) throws -> Root
    func invoke(_ call: GesExternalTypeConstructorCall) throws {
        try call.setExternalValue(SwiftExternalValue(create(call.arguments), type: type))
    }
}
