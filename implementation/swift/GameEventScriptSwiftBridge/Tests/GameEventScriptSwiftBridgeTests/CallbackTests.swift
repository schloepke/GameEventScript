// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

final class CallbackTests: XCTestCase {
    enum Failure: Error { case unexpected }

    func testClosureSubscriptionsPreservePriorityTagsAndCapturedDelivery() throws {
        let host = try GameEventScriptHost(seed: 1).startForTest()
        let signature = try GameEventScriptMessageSignature(name: "Start", parameters: ["value"])
        var received: [String] = []
        let low = try host.subscribe(signature) { message, _ in received.append("low:\(message.arguments[0])") }
        _ = try host.subscribe(signature, matchingTags: ["ready"], withoutTags: ["skip"], priority: 5) { _, _ in received.append("high") }
        _ = try host.subscribeMessageName("Start", priority: -1) { _, _ in received.append("name") }
        host.receive(try .init(name: "Start", swiftArguments: [("value", 42)], tags: ["ready"]))
        XCTAssertTrue(low.unsubscribe())
        XCTAssertEqual(try host.runToCompletion().state, .completed)
        XCTAssertEqual(received, ["high", "low:42", "name"])
        received.removeAll()
        host.receive(try .init(name: "Start", swiftArguments: [("value", 43)], tags: ["skip"]))
        _ = try host.runToCompletion()
        XCTAssertEqual(received, ["name"])
    }

    func testClosureFailuresRetainRuntimeClassification() throws {
        for explicit in [false, true] {
            let host = try GameEventScriptHost(seed: 1).startForTest()
            _ = try host.subscribeMessageName("Start") { _, _ in
                if explicit { throw try GameEventScriptExtensionFault(code: "app.failed", message: "Failed") }
                throw Failure.unexpected
            }
            host.receive(try .init(name: "Start"))
            let result = try host.runToCompletion()
            XCTAssertEqual(result.state, .runtimeError)
            XCTAssertEqual(result.diagnostic?.phase, .runtime)
            XCTAssertEqual(result.diagnostic?.code, explicit ? "app.failed" : "runtime.nativeHandlerFailure")
            XCTAssertEqual(result.diagnostic?.handlerName, "Start(*)")
        }
    }

    func testNamedDictionaryBindingUsesSignatureOrderAndRejectsMissingOrPositionalLabels() throws {
        let signature = try GameEventScriptMessageSignature(name: "Done", parameters: ["z", "a"])
        let message = try signature.withSwiftArguments(["a": "hello", " z ": 12])
        XCTAssertEqual(message.signatureId, "Done(z,a)")
        XCTAssertEqual(message.arguments[0], .integer(12))
        XCTAssertEqual(message.arguments[1], .text("hello"))
        XCTAssertThrowsError(try signature.withSwiftArguments(["a": 1]))
        XCTAssertThrowsError(try signature.withSwiftArguments(["a": 1, "other": 2]))
        XCTAssertThrowsError(try signature.withSwiftArguments(["a": 1, " a ": 2]))
        XCTAssertThrowsError(try GameEventScriptMessageSignature(name: "Done", parameters: ["_"]).withSwiftArguments(["_": 1]))
        let positional = try GameEventScriptMessage(name: "Done", swiftArguments: [(nil, 1), (nil, "two")])
        XCTAssertEqual(positional.signatureId, "Done(_,_)")
    }

    func testContextAndPublishClosureUsePortableDelivery() throws {
        var outbound: [GameEventScriptMessage] = []
        var local: [GameEventScriptMessage] = []
        let sink = GameEventScriptSwiftPublishSink {
            outbound.append($0)
            return true
        }
        let host = try GameEventScriptHost(seed: 1, publishSink: sink).startForTest()
        _ = try host.subscribeMessageName("Done") { message, _ in local.append(message) }
        _ = try host.subscribeMessageName("Start") { _, context in
            try context.emit("Done", swiftArguments: [("value", 1)])
            let result = try context.publish("Done", swiftArguments: [("value", "published")], tags: ["ready"])
            XCTAssertTrue(result.localAccepted)
            XCTAssertTrue(result.outboundAccepted)
        }
        host.receive(try .init(name: "Start"))
        XCTAssertEqual(try host.runToCompletion().state, .completed)
        XCTAssertEqual(local.count, 2)
        XCTAssertEqual(outbound, [local[1]])
    }

    func testExtensionRegistryValidatesIdentityAndFallsBack() throws {
        let function = try GameEventScriptSwiftExtension(namespace: "app", name: "value", parameters: ["a", "b"]) { try $0.setSwiftValue(42) }
        let fallback = try GameEventScriptSwiftExtensionRegistry([function])
        let registry = try GameEventScriptSwiftExtensionRegistry([], fallback: fallback)
        let call = GesExtensionCall()
        try XCTUnwrap(registry.resolve(function.reference)).invoke(call)
        XCTAssertEqual(call.result, .integer(42))
        XCTAssertNil(try registry.resolve(.init(extensionName: "app", functionName: "value", argumentLabels: ["b", "a"])))
        XCTAssertThrowsError(try GameEventScriptSwiftExtensionRegistry([function, function]))
        XCTAssertThrowsError(try GameEventScriptSwiftExtension(namespace: "app", name: "value", parameters: ["a", "a"]) { _ in })
        XCTAssertNoThrow(try GameEventScriptSwiftExtension(namespace: "app", name: "value", parameters: ["_", "_"]) { _ in })
        XCTAssertThrowsError(try GesValueArguments().swiftValue(at: 0, as: Int?.self))
    }
}
