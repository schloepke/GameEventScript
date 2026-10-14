// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
