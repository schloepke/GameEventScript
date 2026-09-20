// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

public protocol GameEventScriptExternalTypeRegistry {
    func resolve(_ reference: GameEventScriptExternalTypeConstructorReference) throws -> (
        any GameEventScriptExternalTypeConstructor
    )?
}
public protocol GameEventScriptExternalTypeCatalogProtocol {
    var types: [GameEventScriptExternalTypeDefinition] { get }
    func resolve(_ typeName: String) throws -> GameEventScriptExternalTypeDefinition?
}
/// An embedding-provided value. Field access is synchronous and may report an explicit application fault.
public protocol GameEventScriptExternalValue: AnyObject {
    var definition: GameEventScriptExternalTypeDefinition { get }
    func field(_ name: String) throws -> GesValue?
}
public protocol GameEventScriptExternalTypeConstructor {
    var definition: GameEventScriptExternalTypeConstructorDefinition { get }
    func invoke(_ call: GesExternalTypeConstructorCall) throws
}
public struct GameEventScriptExternalTypeFieldDefinition {
    public let name: String
    public let typeName: String
    public let kind: GameEventScriptBytecodeTypeKind?
    public let unit: GesUnit
    public init(name: String, typeName: String) throws {
        self.name = try GesExternalNames.identifier(name)
        self.typeName = try GesExternalNames.type(typeName)
        kind = GesExternalNames.kind(self.typeName)
        unit = .none
    }
    public init(name: String, kind: GameEventScriptBytecodeTypeKind, unit: GesUnit = .none) throws {
        self.name = try GesExternalNames.identifier(name)
        self.kind = kind
        self.unit = unit
        typeName = try GesExternalNames.typeName(kind, unit)
    }
}
public struct GameEventScriptExternalTypeParameterDefinition {
    public let name: String
    public let typeName: String
    public let kind: GameEventScriptBytecodeTypeKind?
    public let unit: GesUnit
    public init(name: String, typeName: String) throws {
        self.name = try GesExternalNames.identifier(name)
        self.typeName = try GesExternalNames.type(typeName)
        kind = GesExternalNames.kind(self.typeName)
        unit = .none
    }
    public init(name: String, kind: GameEventScriptBytecodeTypeKind, unit: GesUnit = .none) throws {
        self.name = try GesExternalNames.identifier(name)
        self.kind = kind
        self.unit = unit
        typeName = try GesExternalNames.typeName(kind, unit)
    }
}
public struct GameEventScriptExternalTypeConstructorReference {
    public let typeName: String
    public let argumentLabels: [String]
    public let signatureID: String
    public init(typeName: String, argumentLabels: [String] = []) throws {
        self.typeName = try GesExternalNames.type(typeName)
        self.argumentLabels = try argumentLabels.map(GesExternalNames.identifier).sorted()
        signatureID = self.typeName + "(" + self.argumentLabels.joined(separator: ",") + ")"
    }
}
public struct GameEventScriptExternalTypeConstructorDefinition {
    public let typeName: String
    public let parameters: [GameEventScriptExternalTypeParameterDefinition]
    public let signatureID: String
    public init(typeName: String, parameters: [GameEventScriptExternalTypeParameterDefinition] = []) throws {
        let reference = try GameEventScriptExternalTypeConstructorReference(
            typeName: typeName, argumentLabels: parameters.map(\.name))
        if Set(reference.argumentLabels).count != parameters.count {
            throw GameEventScriptAPIError.invalidArgument("Duplicate external constructor parameter")
        }
        self.typeName = reference.typeName
        self.parameters = parameters
        signatureID = reference.signatureID
    }
}
public struct GameEventScriptExternalTypeDefinition {
    public let name: String
    public let fields: [GameEventScriptExternalTypeFieldDefinition]
    public let constructors: [GameEventScriptExternalTypeConstructorDefinition]
    public init(
        name: String, fields: [GameEventScriptExternalTypeFieldDefinition],
        constructors: [GameEventScriptExternalTypeConstructorDefinition]
    ) throws {
        self.name = try GesExternalNames.type(name)
        self.fields = fields
        self.constructors = constructors
        let fieldNames = Set(fields.map(\.name))
        if fieldNames.count != fields.count || Set(constructors.map(\.signatureID)).count != constructors.count {
            throw GameEventScriptAPIError.invalidArgument("Duplicate external definition")
        }
        for constructor in constructors {
            if constructor.typeName != self.name
                || constructor.parameters.contains(where: { !fieldNames.contains($0.name) })
            {
                throw GameEventScriptAPIError.invalidArgument("Constructor does not match its type or fields")
            }
        }
    }
}
public struct GameEventScriptExternalTypeCatalog: GameEventScriptExternalTypeCatalogProtocol {
    public let types: [GameEventScriptExternalTypeDefinition]
    public init(_ types: [GameEventScriptExternalTypeDefinition]) throws {
        if Set(types.map(\.name)).count != types.count {
            throw GameEventScriptAPIError.invalidArgument("Duplicate external type")
        }
        self.types = types
    }
    public func resolve(_ typeName: String) throws -> GameEventScriptExternalTypeDefinition? {
        let name = try GesExternalNames.type(typeName)
        return types.first { $0.name == name }
    }
}
public final class GesExternalTypeConstructorCall {
    public internal(set) var arguments: GesValueArguments = .init()
    public private(set) var result: GesValue = .nothing
    private var expectedTypeName: String?
    public init(arguments: GesValueArguments = .init()) { self.arguments = arguments }
    public func setNothing() { result = .nothing }
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
        do {
            return try value.field(name).map { GesExternalNames.coerce($0, kind: field.kind, unit: field.unit) }
        } catch let fault as GameEventScriptExtensionFault { throw fault } catch let fault as GesRuntimeError {
            throw fault
        } catch {
            throw GesRuntimeError(
                diagnostic: .init(
                    phase: .runtime, code: "runtime.externalFieldAccessFailed", symbol: definition.name + "." + name,
                    technicalDetails: String(describing: error)))
        }
    }
    func map() throws -> GesValueMap {
        if let cachedMap { return cachedMap }
        var entries: [GesMapEntry] = []
        for field in definition.fields {
            do {
                if let result = try self.field(field.name) { entries.append(.init(key: field.name, value: result)) }
            } catch let fault as GameEventScriptExtensionFault { throw fault } catch let error as GesRuntimeError {
                throw error
            } catch {
                throw GesRuntimeError(
                    diagnostic: .init(
                        phase: .runtime, code: "runtime.externalFieldAccessFailed",
                        symbol: definition.name + "." + field.name, technicalDetails: String(describing: error)))
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
        guard
            [
                .nothing, .tag, .text, .percentage, .vector, .point, .float, .boolean, .series, .range, .handler, .list,
                .map, .dice,
            ].contains(kind),
            unit == .none || kind == .float || kind == .vector || kind == .point
        else {
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
        case .vector where value.spatialValue != nil:
            return .vector(x: value.x, y: value.y, z: value.z, unit: unit == .none ? value.unit : unit)
        case .point where value.spatialValue != nil:
            return .point(x: value.x, y: value.y, z: value.z, unit: unit == .none ? value.unit : unit)
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
