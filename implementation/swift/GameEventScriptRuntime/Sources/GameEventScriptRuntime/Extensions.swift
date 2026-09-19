// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

public struct GameEventScriptExtensionReference {
    public let extensionName: String
    public let functionName: String
    public let argumentLabels: [String]
    public let signatureID: String
    public init(extensionName: String, functionName: String, argumentLabels: [String] = []) throws {
        self.extensionName = try GesExternalNames.identifier(extensionName)
        self.functionName = try GesExternalNames.identifier(functionName)
        self.argumentLabels = try argumentLabels.map { try GameEventScriptMessageSignature.normalizeParameterName($0) }
        signatureID =
            self.extensionName + "." + self.functionName + "(" + self.argumentLabels.joined(separator: ",") + ")"
    }
}
public protocol GameEventScriptExtensionRegistry {
    func resolve(_ reference: GameEventScriptExtensionReference) throws -> (any GameEventScriptExtensionFunction)?
}
public protocol GameEventScriptExtensionFunction {
    func invoke(_ call: GesExtensionCall) throws
}
/// Reused for a synchronous callback only; callers must not retain the call or its argument view.
public final class GesExtensionCall {
    private var activeContext: GameEventScriptContext?
    public var context: GameEventScriptContext {
        precondition(activeContext != nil)
        return activeContext!
    }
    public var random: GameEventScriptRandomGenerator { context.random }
    public var runtimeLimits: GameEventScriptRuntimeLimits { context.runtimeLimits }
    public internal(set) var arguments: GesValueArguments = .init()
    public private(set) var result: GesValue = .nothing
    public init() {}
    public func setValue(_ value: GesValue) { result = value }
    public func setNothing() { result = .nothing }
    public func setBoolean(_ value: Bool) { result = .boolean(value) }
    public func setInteger(_ value: Int64, unit: GesUnit = .none) { result = .integer(value, unit: unit) }
    public func setFloat(_ value: Double, unit: GesUnit = .none) { result = .float(value, unit: unit) }
    public func setPercentage(_ value: Double) { result = .percentage(value) }
    public func setText(_ value: String) { result = .text(value) }
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
    public var count: Int { state == nil ? values.count : indexes.count }
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
    public subscript(index: Int) -> GesValue {
        guard index >= 0 && index < count else { return .nothing }
        return state.map { $0.value(Int(indexes[index])) } ?? values[index]
    }
}

enum GesExternalNames {
    static func identifier(_ text: String) throws -> String {
        let text = MessageNames.trim(text)
        guard GesNames.plain(text) else {
            throw GameEventScriptAPIError.invalidArgument("Invalid external field or extension name")
        }
        return text
    }
    static func type(_ text: String) throws -> String {
        var text = MessageNames.trim(text)
        if text.hasPrefix(":") { text.removeFirst() }
        guard GesNames.type(text) else { throw GameEventScriptAPIError.invalidArgument("Invalid external type name") }
        return text
    }
}
