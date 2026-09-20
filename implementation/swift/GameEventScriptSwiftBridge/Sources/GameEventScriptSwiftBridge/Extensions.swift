// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One validated extension signature and its synchronous Swift implementation.
public struct GameEventScriptSwiftExtension: GameEventScriptExtensionFunction {
    public let reference: GameEventScriptExtensionReference
    private let body: (GesExtensionCall) throws -> Void
    public init(
        namespace: String, name: String, parameters: [String] = [], body: @escaping (GesExtensionCall) throws -> Void
    ) throws {
        reference = try .init(extensionName: namespace, functionName: name, argumentLabels: parameters)
        let named = reference.argumentLabels.filter { $0 != "_" }
        guard Set(named).count == named.count else { throw GameEventScriptMessageError.duplicateArgumentName }
        self.body = body
    }
    public func invoke(_ call: GesExtensionCall) throws { try body(call) }
}

/// Immutable extension registrations, indexed once before Host linking.
public struct GameEventScriptSwiftExtensionRegistry: GameEventScriptExtensionRegistry {
    private let functions: [String: GameEventScriptSwiftExtension]
    private let fallback: (any GameEventScriptExtensionRegistry)?
    public init(_ extensions: [GameEventScriptSwiftExtension], fallback: (any GameEventScriptExtensionRegistry)? = nil)
        throws
    {
        var functions: [String: GameEventScriptSwiftExtension] = [:]
        for item in extensions {
            guard functions.updateValue(item, forKey: item.reference.signatureID) == nil else {
                throw GameEventScriptAPIError.invalidArgument(
                    "Duplicate extension signature: " + item.reference.signatureID)
            }
        }
        self.functions = functions
        self.fallback = fallback
    }
    public func resolve(_ reference: GameEventScriptExtensionReference) throws -> (
        any GameEventScriptExtensionFunction
    )? {
        if let function = functions[reference.signatureID] { return function }
        return try fallback?.resolve(reference)
    }
}

extension GesExtensionCall {
    /// Converts and stores the result without retaining this borrowed call object.
    public func setSwiftValue<T: GameEventScriptSwiftValueConvertible>(_ value: T) throws {
        setValue(try value.toGesValue())
    }
}
extension GesValueArguments {
    /// Strict native conversion with bounds checking; only use during the active callback.
    public func swiftValue<T: GameEventScriptSwiftValueConvertible>(at index: Int, as type: T.Type = T.self) throws -> T
    {
        guard index >= 0 && index < count else {
            throw GameEventScriptAPIError.invalidArgument("Argument index out of range")
        }
        return try T.fromGesValue(self[index])
    }
}
