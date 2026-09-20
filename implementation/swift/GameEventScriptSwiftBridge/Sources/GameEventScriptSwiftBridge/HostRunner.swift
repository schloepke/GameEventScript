// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Dispatch
import Foundation
import GameEventScriptRuntime

/// A runner-owned registration. All lifecycle access uses the same serialization gate as execution.
public struct GameEventScriptSwiftRegistration: Sendable {
    public let registrationID: Int64
    private let owner: GameEventScriptSwiftHostRunner
    fileprivate init(_ id: Int64, owner: GameEventScriptSwiftHostRunner) {
        registrationID = id
        self.owner = owner
    }
    public var isAttached: Bool { owner.isAttached(registrationID) }
    public var startResult: GameEventScriptStartResult? { owner.startResult(registrationID) }
    @discardableResult public func detach() -> Bool { owner.detach(registrationID) }
}

/// Owns a transferred Host and serializes all access. Ready hosts pump on a shared serial dispatcher.
/// Start is explicit for a host still in its initial loading phase; recursive receives only enqueue.
/// Callbacks, external objects and the Host must not be accessed outside the runner after transfer.
public final class GameEventScriptSwiftHostRunner: @unchecked Sendable {
    private let gate = NSRecursiveLock()
    private var host: GameEventScriptHost?
    private var instances: [Int64: GameEventScriptInstance] = [:]
    private var subscriptions: [Int64: GameEventScriptSubscription] = [:]
    private var pumping = false
    private var result: GameEventScriptExecutionResult?
    private var scheduled = false
    private static let dispatcher = DispatchQueue(label: "GameEventScript shared dispatch pump")

    /// Transfers exclusive ownership. An already ready host schedules pending work; a loading host waits for start.
    public init(_ host: sending GameEventScriptHost) {
        self.host = host
        gate.lock()
        if host.isReady && !host.isIdle { schedule() }
        gate.unlock()
    }
    public var lastResult: GameEventScriptExecutionResult? { locked { result } }
    public var isIdle: Bool { locked { host?.isIdle ?? true } }
    public var isReady: Bool { locked { host?.isReady ?? false } }

    public func start() throws -> GameEventScriptStartResult {
        try locked {
            let host = try activeHost()
            let result = try host.start()
            if host.isReady && !host.isIdle { schedule() }
            return result
        }
    }
    fileprivate func startResult(_ id: Int64) -> GameEventScriptStartResult? {
        locked { instances[id]?.startResult }
    }

    @discardableResult public func receive(_ message: sending GameEventScriptMessage) throws -> Bool {
        try locked {
            let host = try activeHost()
            let accepted = host.receive(message)
            if accepted { schedule() }
            return accepted
        }
    }
    /// Programs contain immutable transport data and remain reusable across independent runners.
    public func load(_ program: GameEventScriptProgram, priority: Int = 0) throws
        -> GameEventScriptSwiftRegistration
    {
        try locked {
            let host = try activeHost()
            let instance = try host.load(program, priority: priority)
            instances[instance.registrationID] = instance
            if host.isReady && !host.isIdle { schedule() }
            return .init(instance.registrationID, owner: self)
        }
    }
    public func subscribe(
        _ signature: GameEventScriptMessageSignature, matchingTags: [String] = [], withoutTags: [String] = [],
        priority: Int = 0,
        handler: sending @escaping (GameEventScriptMessage, GameEventScriptContext) throws -> Void
    ) throws -> GameEventScriptSwiftRegistration {
        try locked {
            let subscription = try activeHost().subscribe(
                signature, matchingTags: matchingTags, withoutTags: withoutTags,
                priority: priority, handler: handler)
            subscriptions[subscription.registrationID] = subscription
            return .init(subscription.registrationID, owner: self)
        }
    }
    /// Drains existing work; recursive pump requests are rejected before altering host state.
    @discardableResult public func runToCompletion() throws -> GameEventScriptExecutionResult {
        try locked {
            guard !pumping else {
                throw GameEventScriptAPIError.invalidOperation("A runner cannot be pumped recursively")
            }
            try pump(activeHost())
            return result!
        }
    }
    /// Stops accepting work and releases the owned Host. A synchronous pump already running may finish.
    public func close() {
        locked {
            for instance in instances.values { instance.detach() }
            for subscription in subscriptions.values { subscription.unsubscribe() }
            instances.removeAll()
            subscriptions.removeAll()
            host = nil
        }
    }
    fileprivate func isAttached(_ id: Int64) -> Bool {
        locked { instances[id]?.isAttached ?? subscriptions[id]?.isSubscribed ?? false }
    }
    fileprivate func detach(_ id: Int64) -> Bool {
        locked {
            if let instance = instances.removeValue(forKey: id) { return instance.detach() }
            return subscriptions.removeValue(forKey: id)?.unsubscribe() ?? false
        }
    }
    private func schedule() {
        guard !scheduled else { return }
        scheduled = true
        Self.dispatcher.async { self.automaticPump() }
    }
    private func automaticPump() {
        locked {
            defer {
                scheduled = false
                if let host, host.isReady && !host.isIdle { schedule() }
            }
            guard let host, host.isReady, !host.isIdle else { return }
            // A valid ready host cannot fail the pump API preconditions under this gate.
            do { try pump(host) } catch { return }
        }
    }
    private func pump(_ host: GameEventScriptHost) throws {
        guard !pumping else { return }
        pumping = true
        defer { pumping = false }
        result = try host.runToCompletion()
    }
    private func activeHost() throws -> GameEventScriptHost {
        guard let host else { throw GameEventScriptAPIError.invalidOperation("The runner is closed") }
        return host
    }
    private func locked<T>(_ body: () throws -> T) rethrows -> T {
        gate.lock()
        defer { gate.unlock() }
        return try body()
    }
}
