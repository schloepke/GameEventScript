// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One validated extension signature and its synchronous Swift implementation.
public struct GesBridgeExtension: GesExtensionFunction {
    /// Validated portable signature used during host linking.
    public let reference: GesExtensionReference
    private let body: (GesExtensionCall) throws -> Void

    /// Validates an extension signature and retains its synchronous implementation.
    ///
    /// - Throws: An API or message error for invalid names or duplicate named parameters.
    public init(namespace: String, name: String, parameters: [String] = [], body: @escaping (GesExtensionCall) throws -> Void) throws {
        reference = try .init(extensionName: namespace, functionName: name, argumentLabels: parameters)
        let named = reference.argumentLabels.filter { $0 != "_" }
        guard Set(named).count == named.count else { throw GesMessageError.duplicateArgumentName }
        self.body = body
    }

    /// Invokes the stored closure with the borrowed runtime call, propagating its error.
    public func invoke(_ call: GesExtensionCall) throws { try body(call) }
}

/// Immutable extension registrations, indexed once before Host linking.
public struct GesBridgeExtensionRegistry: GesExtensionRegistry {
    private let functions: [String: GesBridgeExtension]
    private let fallback: (any GesExtensionRegistry)?

    /// Indexes unique extension signatures and optionally delegates missing imports to a fallback.
    ///
    /// - Throws: An API error for duplicate signatures.
    public init(_ extensions: [GesBridgeExtension], fallback: (any GesExtensionRegistry)? = nil) throws {
        var functions: [String: GesBridgeExtension] = [:]
        for item in extensions { guard functions.updateValue(item, forKey: item.reference.signatureID) == nil else { throw GesApiError.invalidArgument("Duplicate extension signature: " + item.reference.signatureID) } }
        self.functions = functions
        self.fallback = fallback
    }

    /// Returns the exact registered function, otherwise queries the optional fallback and propagates resolver errors.
    public func resolve(_ reference: GesExtensionReference) throws -> (any GesExtensionFunction)? {
        if let function = functions[reference.signatureID] { return function }
        return try fallback?.resolve(reference)
    }
}

extension GesExtensionCall {
    /// Converts and stores the result without retaining this borrowed call object.
    public func setSwiftValue<T: GesBridgeValueConvertible>(_ value: T) throws { setValue(try value.toGesValue()) }
}

extension GesValueArguments {
    /// Strict native conversion with bounds checking; only use during the active callback.
    public func swiftValue<T: GesBridgeValueConvertible>(at index: Int, as type: T.Type = T.self) throws -> T {
        guard index >= 0 && index < count else { throw GesApiError.invalidArgument("Argument index out of range") }
        return try T.fromGesValue(self[index])
    }
}
