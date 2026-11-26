// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// Native control that observing delayed messages leaves delivery unchanged; callback traces are covered by the shared corpus.
final class DelayedObservationTests: XCTestCase {
    final class ManualClock: GesClock {
        var elapsedMicroseconds: Int64 = 0
    }

    final class Recorder: GesNativeMessageHandler, GesPublishSink {
        let clock: ManualClock
        var events: [String] = []

        init(clock: ManualClock) { self.clock = clock }

        func handle(_ message: GameEventScriptMessage, context: GesContext) throws { events.append("\(clock.elapsedMicroseconds) \(message.name)") }

        func publish(_ message: GameEventScriptMessage) throws -> Bool {
            events.append("\(clock.elapsedMicroseconds) publish \(message.name)")
            return true
        }
    }

    /// Implements no scheduling callback, so it relies on the protocol's default implementations.
    final class SilentObserver: GesRuntimeObserver {}

    func testDelayedMessagesBehaveIdenticallyWithAndWithoutAnObserver() throws {
        let silent = try runDelayedScenario(observer: nil)
        let observed = try runDelayedScenario(observer: SilentObserver())

        XCTAssertEqual(silent, ["delay 1000000", "1000000 Later", "1000000 Later", "2000000 publish Out", "delay none"])
        XCTAssertEqual(observed, silent)
    }

    /// Resets the host from discard and publish callbacks and records each rejection.
    final class ResettingCallbacks: GesRuntimeObserver, GesPublishSink, GesNativeMessageHandler {
        weak var host: GameEventScriptHost?
        var captured: GesContext?
        var rejected: [String] = []

        func tryReset(_ origin: String) {
            do { try host?.reset() } catch { rejected.append(origin) }
        }

        func messageDiscarded(id: Int64) { tryReset("discarded") }

        func publish(_ message: GameEventScriptMessage) throws -> Bool {
            tryReset("publish")
            return true
        }

        func handle(_ message: GameEventScriptMessage, context: GesContext) throws { captured = context }
    }

    func testResetIsRejectedFromCallbacksOfOtherHostOperations() throws {
        let callbacks = ResettingCallbacks()
        let host = try GameEventScriptHost.makeBuilder().withRuntimeObserver(callbacks).withPublishSink(callbacks).build().startForTest()
        callbacks.host = host
        _ = try host.subscribe(.init(name: "Capture"), handler: callbacks)
        let instance = try host.load(GesProgramBuilder().addScript("on Start { emit every 1s named 'beat' Beat() }\non Beat { }").compile())
        try host.receive(.init(name: "Capture"))
        try host.receive(.init(name: "Start"))
        _ = try host.runToCompletion()

        try callbacks.captured?.publish("Out")
        instance.detach()

        XCTAssertEqual(callbacks.rejected, ["publish", "discarded"])
        XCTAssertTrue(host.isReady)
        try host.reset()
    }

    private func runDelayedScenario(observer: (any GesRuntimeObserver)?) throws -> [String] {
        let clock = ManualClock()
        let recorder = Recorder(clock: clock)
        let builder = GameEventScriptHost.makeBuilder().withClock(clock).withPublishSink(recorder)
        if let observer { builder.withRuntimeObserver(observer) }
        let host = try builder.build().startForTest()
        _ = try host.load(GesProgramBuilder().addScript("on Work { emit after 1s Later(); publish after 2s Out(); emit after 1s Later() }").compile())
        _ = try host.subscribe(.init(name: "Later"), handler: recorder)

        try host.receive(.init(name: "Work"))
        _ = try host.runToCompletion()
        recorder.events.append("delay \(host.nextMessageDelay.map(String.init) ?? "none")")
        clock.elapsedMicroseconds = 1_000_000
        _ = try host.runToCompletion()
        clock.elapsedMicroseconds = 2_000_000
        _ = try host.runToCompletion()
        recorder.events.append("delay \(host.nextMessageDelay.map(String.init) ?? "none")")
        return recorder.events
    }
}
