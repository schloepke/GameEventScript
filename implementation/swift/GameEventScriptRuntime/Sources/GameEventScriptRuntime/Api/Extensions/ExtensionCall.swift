// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
