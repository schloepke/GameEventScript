// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

/// Native binding tests live outside the portable Markdown oracle. Compiler is a test-only dependency of the bridge consumer.
final class SwiftBridgeIntegrationTests: XCTestCase {
    func testRunnerWakesForDelayedMessagesWithoutFurtherInput() throws {
        let program = try GameEventScriptBuilder().addScript("on Start { emit after 0.02s Done() }").compile()
        let runner = try GameEventScriptSwiftHostRunner(GameEventScriptHost.createBuilder().build())
        defer { runner.close() }
        let done = expectation(description: "Delayed delivery without another Receive")
        _ = try runner.subscribe(.init(name: "Done")) { _, _ in done.fulfill() }
        _ = try runner.load(program)
        XCTAssertEqual(try runner.start().state, .ready)
        XCTAssertTrue(try runner.receive(.init(name: "Start")))
        wait(for: [done], timeout: 5)
    }

    struct Player {
        let name: String
        let score: Int
    }

    enum Failure: Error { case unexpected }

    func testTypedCatalogExtensionAndExternalConstructorExecuteFromBinary() throws {
        let player = try GameEventScriptSwiftType<Player>(
            "Player",
            fields: [.init("name", typeName: "Text", keyPath: \Player.name), .init("score", typeName: "Number", keyPath: \Player.score)],
            constructors: [.init(parameters: ["score", "name"]) { args in try Player(name: args.swiftValue(at: 1), score: args.swiftValue(at: 0)) }]
        )
        let types = try GameEventScriptSwiftExternalTypeRegistry([player.binding])
        let extensions = try GameEventScriptSwiftExtensionRegistry([
            .init(namespace: "app", name: "double", parameters: ["_"]) { call in
                let number: Int = try call.arguments.swiftValue(at: 0)
                try call.setSwiftValue(number * 2)
            }
        ])
        let source = """
            module bridge.example
            on Start() {
                let player be :Player(name: "Ada", score: :app.double(21))
                emit Done(name: player.name, score: player.score)
            }
            """
        let compiled = try GameEventScriptBuilder().withExternalTypeCatalog(types).addScript(source).compile()
        let bytes = try GameEventScriptProgramWriter.bytes(compiled)
        let program = try GameEventScriptProgramReader.read(bytes)
        let host = try GameEventScriptHost(seed: 1, extensions: extensions, externalTypes: types).startForTest()
        var result: GameEventScriptMessage?
        _ = try host.subscribe(.init(name: "Done", parameters: ["name", "score"])) { message, _ in result = message }
        _ = try host.load(program)
        host.receive(try .init(name: "Start"))
        let execution = try host.runToCompletion()
        XCTAssertEqual(execution.state, .completed, "\(String(describing: execution.diagnostic))")
        XCTAssertEqual(result?.arguments[0], .text("Ada"))
        XCTAssertEqual(result?.arguments[1], .integer(42))
    }

    func testExtensionAndConstructorFailuresRetainDeclaredAndUnexpectedClassification() throws {
        for explicit in [true, false] {
            let fail: () throws -> Void = {
                if explicit { throw try GameEventScriptExtensionFault(code: "app.failure", message: "Failed") }
                throw Failure.unexpected
            }
            let extensions = try GameEventScriptSwiftExtensionRegistry([.init(namespace: "app", name: "fail") { _ in try fail() }])
            let type = try GameEventScriptSwiftType<Player>(
                "Player",
                fields: [],
                constructors: [
                    .init { _ in
                        try fail()
                        return Player(name: "", score: 0)
                    }
                ]
            )
            let types = try GameEventScriptSwiftExternalTypeRegistry([type.binding])
            for (expression, unexpected) in [(":app.fail()", "runtime.extensionCallFailed"), (":Player()", "runtime.externalConstructorFailed")] {
                let program = try GameEventScriptBuilder().withExternalTypeCatalog(types).addScript("on Start() { emit Done(\(expression)) }").compile()
                let host = try GameEventScriptHost(seed: 1, extensions: extensions, externalTypes: types).startForTest()
                _ = try host.load(program)
                host.receive(try .init(name: "Start"))
                let result = try host.runToCompletion()
                XCTAssertEqual(result.state, .runtimeError)
                XCTAssertEqual(result.diagnostic?.code, explicit ? "app.failure" : unexpected)
                XCTAssertEqual(result.diagnostic?.handlerName, "Start()")
            }
        }
    }

    func testRunnerSchedulesInitializationAndDetachesProgram() throws {
        let program = try GameEventScriptBuilder().addScript("on initialization { emit Ready() }\non Start() { emit Done() }").compile()
        let runner = try GameEventScriptSwiftHostRunner(GameEventScriptHost(seed: 1).startForTest())
        defer { runner.close() }
        let instance = try runner.load(program)
        while !runner.isIdle { _ = try runner.runToCompletion() }
        XCTAssertEqual(runner.lastResult?.state, .completed)
        XCTAssertEqual(runner.lastResult?.emittedMessages, 1)
        XCTAssertTrue(instance.isAttached)
        try runner.receive(.init(name: "Start"))
        while !runner.isIdle { _ = try runner.runToCompletion() }
        XCTAssertEqual(runner.lastResult?.emittedMessages, 1)
        XCTAssertTrue(instance.detach())
        XCTAssertFalse(instance.isAttached)
        XCTAssertFalse(try runner.receive(.init(name: "Start")))
        XCTAssertEqual(try runner.runToCompletion().emittedMessages, 0)
    }

    func testImmutableProgramRemainsReusableAcrossRunners() throws {
        let program = try GameEventScriptBuilder().addScript("on initialization { emit Ready() }").compile()
        let first = try GameEventScriptSwiftHostRunner(GameEventScriptHost(seed: 1).startForTest())
        let second = try GameEventScriptSwiftHostRunner(GameEventScriptHost(seed: 2).startForTest())
        defer {
            first.close()
            second.close()
        }
        let firstInstance = try first.load(program)
        let secondInstance = try second.load(program)
        while !first.isIdle { _ = try first.runToCompletion() }
        while !second.isIdle { _ = try second.runToCompletion() }
        XCTAssertEqual(first.lastResult?.emittedMessages, 1)
        XCTAssertEqual(second.lastResult?.emittedMessages, 1)
        XCTAssertTrue(firstInstance.detach())
        XCTAssertTrue(secondInstance.isAttached)
        XCTAssertNoThrow(try GameEventScriptProgramWriter.bytes(program))
    }
}
