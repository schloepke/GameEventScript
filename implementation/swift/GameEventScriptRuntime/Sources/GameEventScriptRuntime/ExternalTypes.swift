// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host-side resolver for executable external constructors.
public protocol GameEventScriptExternalTypeRegistry {
    /// Resolves a constructor reference, or returns nil when unavailable. Resolver errors propagate through linking.
    func resolve(_ reference: GameEventScriptExternalTypeConstructorReference) throws -> (any GameEventScriptExternalTypeConstructor)?
}

/// Declarative external type information available to the compiler without executable bindings.
public protocol GameEventScriptExternalTypeCatalogProtocol {
    /// Available type definitions.
    var types: [GameEventScriptExternalTypeDefinition] { get }

    /// Looks up a declared type, returning nil when unavailable; implementations may reject invalid names.
    func resolve(_ typeName: String) throws -> GameEventScriptExternalTypeDefinition?
}

/// An embedding-provided value. Field access is synchronous and may report an explicit application fault.
public protocol GameEventScriptExternalValue: AnyObject {
    /// Immutable field and constructor declaration associated with this value.
    var definition: GameEventScriptExternalTypeDefinition { get }

    /// Reads a named field synchronously, returning nil for absence. Thrown application faults retain their
    /// classification at the runtime boundary.
    func field(_ name: String) throws -> GesValue?
}

/// Host implementation of one declared external constructor.
public protocol GameEventScriptExternalTypeConstructor {
    /// Declarative constructor signature implemented by this callback.
    var definition: GameEventScriptExternalTypeConstructorDefinition { get }

    /// Reads borrowed arguments and sets an external value or Nothing. Do not retain the call object; thrown faults are
    /// classified by the runtime.
    func invoke(_ call: GesExternalTypeConstructorCall) throws
}

/// Declarative name and type of an external field.
public struct GameEventScriptExternalTypeFieldDefinition {
    /// Normalized lowercase field name.
    public let name: String
    /// Normalized source type name, without the leading colon.
    public let typeName: String
    /// Built-in storage kind when the type name maps to one, otherwise nil.
    public let kind: GameEventScriptBytecodeTypeKind?
    /// Declared quantity unit, or none when unspecified.
    public let unit: GesUnit

    /// Validates the name and source type identifier.
    ///
    /// - Throws: An API error for an invalid identifier.
    public init(name: String, typeName: String) throws {
        self.name = try GesExternalNames.identifier(name)
        self.typeName = try GesExternalNames.type(typeName)
        kind = GesExternalNames.kind(self.typeName)
        unit = .none
    }

    /// Creates a built-in declaration with an optional quantity unit.
    ///
    /// - Throws: An API error for an invalid name or unsupported kind/unit combination.
    public init(name: String, kind: GameEventScriptBytecodeTypeKind, unit: GesUnit = .none) throws {
        self.name = try GesExternalNames.identifier(name)
        self.kind = kind
        self.unit = unit
        typeName = try GesExternalNames.typeName(kind, unit)
    }
}

/// Declarative name and type of an external constructor parameter.
public struct GameEventScriptExternalTypeParameterDefinition {
    /// Normalized lowercase constructor parameter name.
    public let name: String
    /// Normalized source type name, without the leading colon.
    public let typeName: String
    /// Built-in storage kind when the type name maps to one, otherwise nil.
    public let kind: GameEventScriptBytecodeTypeKind?
    /// Declared quantity unit, or none when unspecified.
    public let unit: GesUnit

    /// Validates the name and source type identifier.
    ///
    /// - Throws: An API error for an invalid identifier.
    public init(name: String, typeName: String) throws {
        self.name = try GesExternalNames.identifier(name)
        self.typeName = try GesExternalNames.type(typeName)
        kind = GesExternalNames.kind(self.typeName)
        unit = .none
    }

    /// Creates a built-in declaration with an optional quantity unit.
    ///
    /// - Throws: An API error for an invalid name or unsupported kind/unit combination.
    public init(name: String, kind: GameEventScriptBytecodeTypeKind, unit: GesUnit = .none) throws {
        self.name = try GesExternalNames.identifier(name)
        self.kind = kind
        self.unit = unit
        typeName = try GesExternalNames.typeName(kind, unit)
    }
}

/// Normalized external constructor identity used by the runtime linker.
public struct GameEventScriptExternalTypeConstructorReference {
    /// Normalized external type name.
    public let typeName: String
    /// Normalized argument labels sorted for constructor identity.
    public let argumentLabels: [String]
    /// Canonical typeName(labels) constructor identity.
    public let signatureID: String

    /// Validates names and sorts labels to construct the canonical signature.
    ///
    /// - Throws: An API error for invalid names.
    public init(typeName: String, argumentLabels: [String] = []) throws {
        self.typeName = try GesExternalNames.type(typeName)
        self.argumentLabels = try argumentLabels.map(GesExternalNames.identifier).sorted()
        signatureID = self.typeName + "(" + self.argumentLabels.joined(separator: ",") + ")"
    }
}

/// Declarative external constructor signature with parameters in invocation order.
public struct GameEventScriptExternalTypeConstructorDefinition {
    /// Normalized owning type name.
    public let typeName: String
    /// Declared parameters in invocation order.
    public let parameters: [GameEventScriptExternalTypeParameterDefinition]
    /// Canonical constructor identity with sorted labels.
    public let signatureID: String

    /// Creates a validated constructor declaration.
    ///
    /// - Throws: An API error for invalid names or duplicate parameter labels.
    public init(typeName: String, parameters: [GameEventScriptExternalTypeParameterDefinition] = []) throws {
        let reference = try GameEventScriptExternalTypeConstructorReference(typeName: typeName, argumentLabels: parameters.map(\.name))
        if Set(reference.argumentLabels).count != parameters.count { throw GameEventScriptAPIError.invalidArgument("Duplicate external constructor parameter") }
        self.typeName = reference.typeName
        self.parameters = parameters
        signatureID = reference.signatureID
    }
}

/// Declarative fields and constructors for a host-defined type; contains no native objects or callbacks.
public struct GameEventScriptExternalTypeDefinition {
    /// Normalized external type name.
    public let name: String
    /// Declared fields in definition order.
    public let fields: [GameEventScriptExternalTypeFieldDefinition]
    /// Available constructor signatures.
    public let constructors: [GameEventScriptExternalTypeConstructorDefinition]

    /// Validates field uniqueness and constructor consistency.
    ///
    /// - Throws: An API error for invalid names, duplicate definitions, foreign constructors or parameters without
    /// matching fields.
    public init(name: String, fields: [GameEventScriptExternalTypeFieldDefinition], constructors: [GameEventScriptExternalTypeConstructorDefinition]) throws {
        self.name = try GesExternalNames.type(name)
        self.fields = fields
        self.constructors = constructors
        let fieldNames = Set(fields.map(\.name))
        if fieldNames.count != fields.count || Set(constructors.map(\.signatureID)).count != constructors.count { throw GameEventScriptAPIError.invalidArgument("Duplicate external definition") }
        for constructor in constructors {
            if constructor.typeName != self.name || constructor.parameters.contains(where: { !fieldNames.contains($0.name) }) { throw GameEventScriptAPIError.invalidArgument("Constructor does not match its type or fields") }
        }
    }
}

/// Immutable collection of unique external type declarations.
public struct GameEventScriptExternalTypeCatalog: GameEventScriptExternalTypeCatalogProtocol {
    /// Type definitions in supplied order.
    public let types: [GameEventScriptExternalTypeDefinition]

    /// Creates a catalog.
    ///
    /// - Throws: An API error for duplicate type names.
    public init(_ types: [GameEventScriptExternalTypeDefinition]) throws {
        if Set(types.map(\.name)).count != types.count { throw GameEventScriptAPIError.invalidArgument("Duplicate external type") }
        self.types = types
    }

    /// Normalizes a type name and returns its declaration, or nil when absent.
    ///
    /// - Throws: An API error for an invalid type name.
    public func resolve(_ typeName: String) throws -> GameEventScriptExternalTypeDefinition? {
        let name = try GesExternalNames.type(typeName)
        return types.first { $0.name == name }
    }
}

/// Reused synchronous constructor call; arguments must not be retained beyond the callback.
public final class GesExternalTypeConstructorCall {
    /// Borrowed constructor arguments in declared invocation order.
    public internal(set) var arguments: GesValueArguments = .init()
    /// Constructed external value or Nothing.
    public private(set) var result: GesValue = .nothing
    private var expectedTypeName: String?

    /// Creates a constructor call with an owned argument view and an initial Nothing result.
    public init(arguments: GesValueArguments = .init()) { self.arguments = arguments }

    /// Clears the constructor result to Nothing.
    public func setNothing() { result = .nothing }

    /// Sets the constructed external value.
    ///
    /// - Throws: A runtime binding fault when its declared type differs from the constructor's expected type; the
    /// result becomes Nothing.
    public func setExternalValue(_ value: any GameEventScriptExternalValue) throws {
        if let expectedTypeName, value.definition.name != expectedTypeName {
            result = .nothing
            throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.invalidExternalTypeBinding"))
        }
        result = .external(value)
    }

    func begin(arguments: GesValueArguments, typeName: String) {
        self.arguments = arguments
        expectedTypeName = typeName
        result = .nothing
    }

    func end() {
        arguments = .init()
        expectedTypeName = nil
        result = .nothing
    }
}

final class GesExternalStorage: Hashable {
    let value: any GameEventScriptExternalValue
    let definition: GameEventScriptExternalTypeDefinition
    private var cachedMap: GesValueMap?

    init(value: any GameEventScriptExternalValue) {
        self.value = value
        definition = value.definition
    }

    static func == (lhs: GesExternalStorage, rhs: GesExternalStorage) -> Bool { lhs === rhs }

    func hash(into hasher: inout Hasher) { hasher.combine(ObjectIdentifier(self)) }

    func field(_ name: String) throws -> GesValue? {
        guard let field = definition.fields.first(where: { $0.name == name }) else { return nil }
        do { return try value.field(name).map { GesExternalNames.coerce($0, kind: field.kind, unit: field.unit) } } catch let fault as GameEventScriptExtensionFault { throw fault } catch let fault as GesRuntimeError { throw fault } catch {
            throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.externalFieldAccessFailed", symbol: definition.name + "." + name, technicalDetails: String(describing: error)))
        }
    }

    func map() throws -> GesValueMap {
        if let cachedMap { return cachedMap }
        var entries: [GesMapEntry] = []
        for field in definition.fields {
            do { if let result = try self.field(field.name) { entries.append(.init(key: field.name, value: result)) } } catch let fault as GameEventScriptExtensionFault { throw fault } catch let error as GesRuntimeError { throw error } catch {
                throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.externalFieldAccessFailed", symbol: definition.name + "." + field.name, technicalDetails: String(describing: error)))
            }
        }
        let map = GesValueMap(entries)
        cachedMap = map
        return map
    }
}

extension GesExternalNames {
    static func kind(_ name: String) -> GameEventScriptBytecodeTypeKind? {
        switch name {
        case "Nothing": .nothing
        case "Boolean": .boolean
        case "Number": .float
        case "Percentage": .percentage
        case "Tag": .tag
        case "Text": .text
        case "Vector": .vector
        case "Point": .point
        case "Series": .series
        case "Range": .range
        case "Message": .message
        case "Handler": .handler
        case "List": .list
        case "Map": .map
        case "Dice": .dice
        default: nil
        }
    }

    static func typeName(_ kind: GameEventScriptBytecodeTypeKind, _ unit: GesUnit) throws -> String {
        guard [.nothing, .tag, .text, .percentage, .vector, .point, .float, .boolean, .series, .range, .handler, .list, .map, .dice].contains(kind), unit == .none || kind == .float || kind == .vector || kind == .point else {
            throw GameEventScriptAPIError.invalidArgument("Unsupported external value kind or unit")
        }
        if (kind == .float || kind == .integer) && unit != .none { return "Quantity(" + unit.suffix + ")" }
        switch kind {
        case .float, .integer: return "Number"
        default:
            let name = String(describing: kind)
            return name.prefix(1).uppercased() + name.dropFirst()
        }
    }

    static func coerce(_ value: GesValue, kind: GameEventScriptBytecodeTypeKind?, unit: GesUnit) -> GesValue {
        switch kind {
        case .float: return .float(value.asNumber, unit: unit)
        case .vector where value.spatialValue != nil: return .vector(x: value.x, y: value.y, z: value.z, unit: unit == .none ? value.unit : unit)
        case .point where value.spatialValue != nil: return .point(x: value.x, y: value.y, z: value.z, unit: unit == .none ? value.unit : unit)
        case .tag:
            if value.kind == .boolean { return try! .tag(value.asBoolean ? "true" : "false") }
            var name = value.toText
            if value.kind == .text {
                if name == "True" { name = "true" }
                if name == "False" { name = "false" }
            }
            return (try? .tag(name)) ?? .nothing
        case .text: return .text(value.toText)
        case .boolean: return .boolean(value.asBoolean)
        case .percentage: return .percentage(value.asNumber)
        case .nothing: return .nothing
        default: return value
        }
    }
}
