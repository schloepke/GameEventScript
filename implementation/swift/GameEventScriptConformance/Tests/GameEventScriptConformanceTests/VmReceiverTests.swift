// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import XCTest

@testable import GameEventScriptRuntime

/// Internal receiver ownership controls; message semantics remain in the shared corpus.
final class VmReceiverTests: XCTestCase {
    private weak var borrowedReceiver: Receiver?

    func testContextUsesReceiverWithoutConcreteHost() throws {
        let receiver = Receiver()
        let random = GameEventScriptRandomGenerator(seed: 7)
        let context = GameEventScriptContext(receiver: receiver, random: random, limits: .init(), extensions: nil, observer: nil)
        let message = try GameEventScriptMessage(name: "Test")
        XCTAssertTrue(context.random === random)
        XCTAssertEqual(context.pendingMessageCount, 3)
        XCTAssertFalse(context.isIdle)
        XCTAssertFalse(context.emit(message))
        XCTAssertEqual(receiver.emitted, message)
        let published = context.publish(message)
        XCTAssertTrue(published.localAccepted)
        XCTAssertTrue(published.outboundAttempted)
        XCTAssertFalse(published.outboundAccepted)
        XCTAssertEqual(receiver.published, message)
        XCTAssertTrue(context.send(message, publish: true, microseconds: 42))
        XCTAssertEqual(receiver.sent, message)
        XCTAssertTrue(receiver.sendPublish)
        XCTAssertEqual(receiver.delay, 42)
        // Budget accounting must not add receiver calls to the execution path.
        let calls = receiver.calls
        XCTAssertEqual(context.budget.reserve(10), 10)
        XCTAssertTrue(context.budget.loop())
        XCTAssertTrue(context.budget.enterCall())
        context.budget.exitCall()
        context.budget.complete(executed: 1, reserved: 10, processing: false)
        XCTAssertEqual(receiver.calls, calls)
    }

    func testContextDoesNotRetainReceiver() throws {
        var receiver: Receiver? = Receiver()
        borrowedReceiver = receiver
        let context = GameEventScriptContext(receiver: receiver!, random: .init(seed: 0), limits: .init(), extensions: nil, observer: nil)
        receiver = nil
        XCTAssertNil(borrowedReceiver)
        let message = try GameEventScriptMessage(name: "Test")
        XCTAssertEqual(context.pendingMessageCount, 0)
        XCTAssertTrue(context.isIdle)
        XCTAssertFalse(context.emit(message))
        XCTAssertFalse(context.publish(message).anyAccepted)
        XCTAssertFalse(context.send(message, publish: false, microseconds: 0))
    }
}

private final class Receiver: GesVmReceiver {
    var calls = 0
    var emitted: GameEventScriptMessage?
    var published: GameEventScriptMessage?
    var sent: GameEventScriptMessage?
    var sendPublish = false
    var delay: Int64 = 0
    var pendingMessageCount: Int {
        calls += 1
        return 3
    }
    var isIdle: Bool {
        calls += 1
        return false
    }

    func emit(_ message: GameEventScriptMessage) -> Bool {
        calls += 1
        emitted = message
        return false
    }

    func publish(_ message: GameEventScriptMessage) -> GameEventScriptPublishResult {
        calls += 1
        published = message
        return .init(localAccepted: true, outboundAttempted: true, outboundAccepted: false)
    }

    func send(_ message: GameEventScriptMessage, publish: Bool, microseconds: Int64) -> Bool {
        calls += 1
        sent = message
        sendPublish = publish
        delay = microseconds
        return true
    }
}
