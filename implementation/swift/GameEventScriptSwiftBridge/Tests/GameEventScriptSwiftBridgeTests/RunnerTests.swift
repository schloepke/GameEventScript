// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Dispatch
import Foundation
import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

final class RunnerTests: XCTestCase {
    final class Trace: @unchecked Sendable {
        private let gate = NSLock()
        private var entries: [Int] = []
        func append(_ value: Int) {
            gate.lock()
            defer { gate.unlock() }
            entries.append(value)
        }
        var values: [Int] {
            gate.lock()
            defer { gate.unlock() }
            return entries
        }
    }

    func testRunnerWaitsForExplicitStart() throws {
        let runner = try GameEventScriptSwiftHostRunner(GameEventScriptHost.createBuilder().build())
        defer { runner.close() }
        let trace = Trace()
        _ = try runner.subscribe(.init(name: "Tick")) { _, _ in trace.append(1) }
        XCTAssertFalse(runner.isReady)
        XCTAssertFalse(try runner.receive(.init(name: "Tick")))
        XCTAssertTrue(trace.values.isEmpty)
        XCTAssertEqual(try runner.start().state, .ready)
        XCTAssertTrue(try runner.receive(.init(name: "Tick")))
        while !runner.isIdle { _ = try runner.runToCompletion() }
        XCTAssertEqual(trace.values, [1])
    }

    func testConcurrentReceivesSerializeMessageDispatch() throws {
        let runner = try GameEventScriptSwiftHostRunner(GameEventScriptHost(seed: 1).startForTest())
        let trace = Trace()
        let start = try GameEventScriptMessageSignature(name: "Start", parameters: ["value"])
        let done = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
        let first = try runner.subscribe(start) { message, context in
            let number = Int(message.arguments[0].asInteger)
            trace.append(number)
            try context.emit("Done", swiftArguments: [("value", number)])
        }
        let second = try runner.subscribe(done) { message, _ in trace.append(Int(message.arguments[0].asInteger)) }
        DispatchQueue.concurrentPerform(iterations: 100) { index in
            do { try runner.receive(.init(name: "Start", swiftArguments: [("value", index)])) } catch {
                XCTFail("\(error)")
            }
        }
        while !runner.isIdle { _ = try runner.runToCompletion() }
        let values = trace.values
        XCTAssertEqual(values.count, 200)
        for value in 0..<100 { XCTAssertEqual(values.filter { $0 == value }.count, 2) }
        XCTAssertEqual(Set(values), Set(0..<100))
        XCTAssertTrue(runner.isIdle)
        XCTAssertEqual(runner.lastResult?.state, .completed)
        XCTAssertTrue(first.detach())
        XCTAssertFalse(first.detach())
        XCTAssertFalse(first.isAttached)
        runner.close()
        XCTAssertFalse(second.isAttached)
        XCTAssertFalse(second.detach())
        XCTAssertThrowsError(try runner.receive(.init(name: "Start")))
        XCTAssertThrowsError(try runner.runToCompletion())
        runner.close()
    }

    func testRecursiveReceivesEnqueueWithoutRecursiveDispatch() throws {
        let runner = try GameEventScriptSwiftHostRunner(GameEventScriptHost(seed: 1).startForTest())
        defer { runner.close() }
        let trace = Trace()
        _ = try runner.subscribe(.init(name: "Start", parameters: ["value"])) { message, _ in
            let number = Int(message.arguments[0].asInteger)
            trace.append(number)
            if number == 0 {
                XCTAssertThrowsError(try runner.runToCompletion())
                try runner.receive(.init(name: "Start", swiftArguments: [("value", 1)]))
                trace.append(2)
            }
        }
        try runner.receive(.init(name: "Start", swiftArguments: [("value", 0)]))
        while !runner.isIdle { _ = try runner.runToCompletion() }
        XCTAssertEqual(trace.values, [0, 2, 1])
        XCTAssertEqual(runner.lastResult?.processedMessages, 2)
    }

    func testTransferCanDrainPreexistingWorkAndReportsFaults() throws {
        let trace = Trace()
        let host = try GameEventScriptHost(seed: 1).startForTest()
        _ = try host.subscribeMessageName("Start") { _, _ in
            trace.append(1)
            throw try GameEventScriptExtensionFault(code: "app.runner", message: "Failed")
        }
        host.receive(try .init(name: "Start"))
        let runner = GameEventScriptSwiftHostRunner(host)
        while !runner.isIdle { _ = try runner.runToCompletion() }
        XCTAssertEqual(runner.lastResult?.diagnostic?.code, "app.runner")
        XCTAssertEqual(trace.values, [1])
        XCTAssertEqual(runner.lastResult?.state, .runtimeError)
        runner.close()
    }
}
