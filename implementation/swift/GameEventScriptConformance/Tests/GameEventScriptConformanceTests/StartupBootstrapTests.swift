// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

final class StartupBootstrapTests: XCTestCase {
    final class Counter: GameEventScriptNativeMessageHandler {
        var delivered = 0
        func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws { delivered += 1 }
    }
    func testBuilderStartIsExplicitAndDoesNotDrainMessages() throws {
        let builder = GameEventScriptHost.createBuilder().withRandomSeed(42)
        let host = try builder.build()
        let other = try builder.build()
        XCTAssertFalse(host === other)
        let counter = Counter()
        _ = try host.subscribe(.init(name: "Tick"), handler: counter)
        let instance = try host.load(
            GameEventScriptBuilder.create().addScript("on initialization { emit Tick() }").compile())
        XCTAssertFalse(host.isReady)
        XCTAssertNil(instance.startResult)
        XCTAssertFalse(host.receive(try .init(name: "Tick")))
        XCTAssertThrowsError(try host.runToCompletion())
        let start = try host.start()
        XCTAssertEqual(start.state, .ready)
        XCTAssertGreaterThan(start.executedOpcodes, 0)
        XCTAssertEqual(start.processedMessages, 1)
        XCTAssertEqual(start.emittedMessages, 1)
        XCTAssertEqual(instance.startResult?.executedOpcodes, start.executedOpcodes)
        XCTAssertEqual(instance.startResult?.state, .ready)
        XCTAssertEqual(counter.delivered, 0)
        XCTAssertEqual(host.pendingMessageCount, 1)
        XCTAssertEqual(try host.start().state, .ready)
        XCTAssertEqual(host.pendingMessageCount, 1)
        _ = try host.runToCompletion()
        XCTAssertEqual(counter.delivered, 1)
        XCTAssertFalse(other.isReady)
    }
}
