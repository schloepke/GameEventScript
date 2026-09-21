// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// A closure retained once at subscription time; dispatch invokes it synchronously.
public struct GameEventScriptSwiftMessageHandler: GameEventScriptNativeMessageHandler {
    private let body: (GameEventScriptMessage, GameEventScriptContext) throws -> Void

    /// Retains a synchronous throwing handler closure once at registration time.
    public init(_ body: @escaping (GameEventScriptMessage, GameEventScriptContext) throws -> Void) { self.body = body }

    /// Invokes the stored closure synchronously with the message and owning host context, propagating its error.
    public func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws { try body(message, context) }
}

/// A synchronous outbound publish callback; errors retain the Runtime's classification.
public struct GameEventScriptSwiftPublishSink: GameEventScriptPublishSink {
    private let body: (GameEventScriptMessage) throws -> Bool

    /// Retains a synchronous outbound callback returning whether delivery was accepted.
    public init(_ body: @escaping (GameEventScriptMessage) throws -> Bool) { self.body = body }

    /// Invokes the stored callback and returns its acceptance flag, propagating its error.
    public func publish(_ message: GameEventScriptMessage) throws -> Bool { try body(message) }
}

extension GameEventScriptHost {
    /// Registers a synchronous native handler for an exact signature and optional tag filters. Higher priority runs
    /// first, then registration order.
    ///
    /// - Returns: An idempotently detachable subscription.
    /// - Throws: An API error for invalid tag names or exhausted registration IDs.
    public func subscribe(_ signature: GameEventScriptMessageSignature, matchingTags: [String] = [], withoutTags: [String] = [], priority: Int = 0, handler: @escaping (GameEventScriptMessage, GameEventScriptContext) throws -> Void) throws
        -> GameEventScriptSubscription
    { try subscribe(signature, handler: GameEventScriptSwiftMessageHandler(handler), matchingTags: matchingTags, withoutTags: withoutTags, priority: priority) }

    /// Registers a native handler matching a message name with any argument labels and optional tag filters.
    ///
    /// - Returns: An idempotently detachable subscription.
    /// - Throws: An API error for an empty name, invalid tags or exhausted registration IDs.
    public func subscribeMessageName(_ name: String, matchingTags: [String] = [], withoutTags: [String] = [], priority: Int = 0, handler: @escaping (GameEventScriptMessage, GameEventScriptContext) throws -> Void) throws -> GameEventScriptSubscription
    { try subscribeMessageName(name, handler: GameEventScriptSwiftMessageHandler(handler), matchingTags: matchingTags, withoutTags: withoutTags, priority: priority) }
}

extension GameEventScriptMessage {
    /// Ordered pairs preserve positional arguments and external label order.
    public init(name: String, swiftArguments: [(String?, any GameEventScriptSwiftValueConvertible)], tags: [String] = []) throws {
        try self.init(name: name, arguments: swiftArguments.map { try GameEventScriptMessageArgument(name: $0.0, value: $0.1.toGesValue()) }, tags: tags)
    }
}

extension GameEventScriptMessageSignature {
    /// Binds a complete named dictionary in signature order; positional signatures cannot use dictionaries.
    public func withSwiftArguments(_ values: [String: any GameEventScriptSwiftValueConvertible]) throws -> GameEventScriptMessage {
        guard !parameters.contains("_") else { throw GameEventScriptMessageError.invalidParameterName }
        var normalized: [String: any GameEventScriptSwiftValueConvertible] = [:]
        for (key, value) in values {
            let name = try Self.normalizeParameterName(key)
            guard normalized.updateValue(value, forKey: name) == nil else { throw GameEventScriptMessageError.duplicateArgumentName }
        }
        guard normalized.count == parameters.count else { throw GameEventScriptMessageError.argumentCountMismatch }
        return try withArguments(
            parameters.map {
                guard let value = normalized[$0] else { throw GameEventScriptMessageError.missingArgument }
                return try value.toGesValue()
            }
        )
    }
}

extension GameEventScriptContext {
    /// Enqueues local delivery without pumping. Returns false if the host no longer exists or rejects the message. The
    /// name overload validates message arguments and may throw.
    @discardableResult public func emit(_ name: String, swiftArguments: [(String?, any GameEventScriptSwiftValueConvertible)], tags: [String] = []) throws -> Bool { emit(try .init(name: name, swiftArguments: swiftArguments, tags: tags)) }

    /// Enqueues local delivery and attempts outbound delivery, or stages publication during initialization. Returns
    /// separate acceptance flags. The name overload validates the message and may throw.
    @discardableResult public func publish(_ name: String, swiftArguments: [(String?, any GameEventScriptSwiftValueConvertible)], tags: [String] = []) throws -> GameEventScriptPublishResult {
        publish(try .init(name: name, swiftArguments: swiftArguments, tags: tags))
    }
}
