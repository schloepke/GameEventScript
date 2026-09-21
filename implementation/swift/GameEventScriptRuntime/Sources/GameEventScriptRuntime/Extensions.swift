// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Normalized extension namespace, function and ordered argument labels used during host linking.
public struct GameEventScriptExtensionReference {
    /// Normalized lowercase extension namespace.
    public let extensionName: String
    /// Normalized lowercase function name.
    public let functionName: String
    /// Ordered external labels; _ denotes a positional argument.
    public let argumentLabels: [String]
    /// Canonical namespace.function(labels) identity.
    public let signatureID: String

    /// Validates and normalizes namespace, function and parameter labels.
    ///
    /// - Throws: An API or message error for invalid identifiers.
    public init(extensionName: String, functionName: String, argumentLabels: [String] = []) throws {
        self.extensionName = try GesExternalNames.identifier(extensionName)
        self.functionName = try GesExternalNames.identifier(functionName)
        self.argumentLabels = try argumentLabels.map { try GameEventScriptMessageSignature.normalizeParameterName($0) }
        signatureID = self.extensionName + "." + self.functionName + "(" + self.argumentLabels.joined(separator: ",") + ")"
    }
}

/// Host-side resolver for executable extension imports.
public protocol GameEventScriptExtensionRegistry {
    /// Resolves the exact extension reference, or returns nil when unavailable. Resolver errors propagate through
    /// linking.
    func resolve(_ reference: GameEventScriptExtensionReference) throws -> (any GameEventScriptExtensionFunction)?
}

/// Synchronous portable extension callback.
public protocol GameEventScriptExtensionFunction {
    /// Reads borrowed arguments and writes the result through the call object. Do not retain the call or its argument
    /// view after returning. Thrown faults are classified at the runtime boundary.
    func invoke(_ call: GesExtensionCall) throws
}

/// Reused for a synchronous callback only; callers must not retain the call or its argument view.
public final class GesExtensionCall {
    private var activeContext: GameEventScriptContext?
    /// Active host context; accessing it outside the synchronous callback is a precondition violation.
    public var context: GameEventScriptContext {
        precondition(activeContext != nil)
        return activeContext!
    }
    /// Private random stream of the active host.
    public var random: GameEventScriptRandomGenerator { context.random }
    /// Limits of the active host.
    public var runtimeLimits: GameEventScriptRuntimeLimits { context.runtimeLimits }
    /// Ordered borrowed arguments, valid only during this invocation.
    public internal(set) var arguments: GesValueArguments = .init()
    /// Result currently set by the callback; initially Nothing.
    public private(set) var result: GesValue = .nothing

    /// Creates an inactive call container. The runtime supplies context and arguments before invocation.
    public init() {}

    /// Replaces the callback result with the supplied value.
    public func setValue(_ value: GesValue) { result = value }

    /// Sets the callback result to Nothing.
    public func setNothing() { result = .nothing }

    /// Sets a Boolean result.
    public func setBoolean(_ value: Bool) { result = .boolean(value) }

    /// Sets an exact Int64 result with an optional unit.
    public func setInteger(_ value: Int64, unit: GesUnit = .none) { result = .integer(value, unit: unit) }

    /// Sets a numeric result using the canonical numeric factory, including NaN-to-Nothing conversion.
    public func setFloat(_ value: Double, unit: GesUnit = .none) { result = .float(value, unit: unit) }

    /// Sets a Percentage result from a ratio; 1 represents 100%.
    public func setPercentage(_ value: Double) { result = .percentage(value) }

    /// Sets a verbatim Text result.
    public func setText(_ value: String) { result = .text(value) }

    /// Sets a Tag result from a bare lowercase name.
    ///
    /// - Throws: `GesValueError.invalidTag` for an invalid name.
    public func setTag(_ value: String) throws { result = try .tag(value) }

    func begin(_ context: GameEventScriptContext, _ arguments: GesValueArguments) {
        activeContext = context
        self.arguments = arguments
        result = .nothing
    }

    func end() {
        activeContext = nil
        arguments = .init()
        result = .nothing
    }
}

/// Ordered callback arguments, optionally borrowed from an active VM register frame.
public struct GesValueArguments {
    private let values: [GesValue]
    private let state: GesVmState?
    private let indexes: [UInt16]
    /// Number of arguments in the current view.
    public var count: Int { state == nil ? values.count : indexes.count }

    /// Creates an owned argument view over the supplied values.
    public init(_ values: [GesValue] = []) {
        self.values = values
        state = nil
        indexes = []
    }

    init(state: GesVmState, indexes: [UInt16]) {
        self.state = state
        self.indexes = indexes
        values = []
    }

    /// Reads a zero-based argument; returns Nothing for an invalid index.
    public subscript(index: Int) -> GesValue {
        guard index >= 0 && index < count else { return .nothing }
        return state.map { $0.value(Int(indexes[index])) } ?? values[index]
    }
}

enum GesExternalNames {
    static func identifier(_ text: String) throws -> String {
        let text = MessageNames.trim(text)
        guard GesNames.plain(text) else { throw GameEventScriptAPIError.invalidArgument("Invalid external field or extension name") }
        return text
    }

    static func type(_ text: String) throws -> String {
        var text = MessageNames.trim(text)
        if text.hasPrefix(":") { text.removeFirst() }
        guard GesNames.type(text) else { throw GameEventScriptAPIError.invalidArgument("Invalid external type name") }
        return text
    }
}
