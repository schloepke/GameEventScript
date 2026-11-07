// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// Literals at the MaxLiteralDepth limit parse on a 512 KB thread stack, the stack of Swift concurrency threads that
/// hosts run on; one level more reports the limit instead of exhausting the stack.
final class LiteralStackDepthTests: XCTestCase {
    private static let limit = 64

    private static func repeated(_ text: String, _ count: Int) -> String { String(repeating: text, count: count) }

    // The deepest forms of the recursive literal parser, each nested `n` levels deep, with their value at the limit.
    // Messages are no valid tags, so nested tag forms parse to nothing.
    private static let deepestForms: [(name: String, value: String, text: @Sendable (Int) -> String)] = [
        ("lists", "value", { n in repeated("[", n) + "1" + repeated("]", n) }),
        ("maps", "value", { n in repeated("[a: ", n) + "1" + repeated("]", n) }),
        ("explicit lists", "value", { n in repeated(":List(", n) + "1" + repeated(")", n) }),
        ("messages", "value", { n in repeated(":Message(Foo(a: ", n) + "1" + repeated("))", n) }),
        ("message tags", "nothing", { n in repeated(":Message(Foo() with ", n) + "#a" + repeated(")", n) }),
        ("vectors", "value", { n in repeated("[", n - 1) + ":Vector(x: 1, y: 2, z: 3)" + repeated("]", n - 1) }),
    ]

    func testLiteralsAtTheDepthLimitParseOnASmallStack() {
        for form in Self.deepestForms { XCTAssertEqual(Self.onSmallStack { [Self.parseData(form.text(Self.limit))] }, [form.value], form.name) }
    }

    func testLiteralsBeyondTheDepthLimitReportTheLimit() {
        for form in Self.deepestForms { XCTAssertEqual(Self.onSmallStack { [Self.parseData(form.text(Self.limit + 1))] }, ["MaxLiteralDepth"], form.name) }
    }

    func testDocumentsAtTheDepthLimitParseOnASmallStack() {
        for form in Self.deepestForms where form.name != "message tags" {
            XCTAssertEqual(Self.onSmallStack { [Self.parseDocument("a: " + form.text(Self.limit))] }, ["value"], form.name)
            XCTAssertEqual(Self.onSmallStack { [Self.parseDocument("a: " + form.text(Self.limit + 1))] }, ["document.limitExceeded MaxLiteralDepth"], form.name)
        }
    }

    func testScriptParseAtTheDepthLimitRunsOnASmallStack() {
        for form in Self.deepestForms {
            XCTAssertEqual(Self.onSmallStack { Self.runParse(form.text(Self.limit)) }, [form.value], form.name)
            XCTAssertEqual(Self.onSmallStack { Self.runParse(form.text(Self.limit + 1)) }, ["MaxLiteralDepth"], form.name)
        }
    }

    private static func parseData(_ text: String) -> String {
        do {
            guard let value = try GesValue(strictLiteral: text) else { return "nil" }
            return value == .nothing ? "nothing" : "value"
        } catch let error as GesLiteralLimitError {
            return error.limitName
        } catch {
            return "\(error)"
        }
    }

    private static func parseDocument(_ text: String) -> String {
        do {
            _ = try GesDocument(parsing: text)
            return "value"
        } catch let error as GesDocumentError {
            return "\(error.code) \(error.limitName ?? "")"
        } catch {
            return "\(error)"
        }
    }

    // The result of `parse strict` in a script handler, or the limit that exhausted the handler.
    private static func runParse(_ text: String) -> [String] {
        final class Recorder: GesNativeMessageHandler, GesRuntimeObserver {
            var outputs: [String] = []

            func handle(_ message: GameEventScriptMessage, context: GesContext) throws { outputs.append(message.arguments[0] == .nothing ? "nothing" : "value") }

            func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) { outputs.append(limitName) }
        }

        do {
            let recorder = Recorder()
            let host = try GameEventScriptHost.makeBuilder().withRuntimeObserver(recorder).build().startForTest()
            _ = try host.load(GesProgramBuilder().addScript("on Parse(_ text) { emit Out(parse strict text) }").compile())
            _ = try host.subscribeMessageName("Out", handler: recorder)
            host.receive(try .init(name: "Parse", arguments: [.init(value: .text(text))]))
            _ = try host.runToCompletion()
            return recorder.outputs
        } catch {
            return ["\(error)"]
        }
    }

    // Runs `body` on a thread with a 512 KB stack and returns its outcomes.
    private static func onSmallStack(_ body: @escaping @Sendable () -> [String]) -> [String] {
        // Written by the parsing thread before it signals and read only after the wait.
        final class Outcome: @unchecked Sendable {
            var result: [String] = []
        }

        let outcome = Outcome()
        let done = DispatchSemaphore(value: 0)
        let thread = Thread {
            outcome.result = body()
            done.signal()
        }
        thread.stackSize = 512 * 1024
        thread.start()
        done.wait()
        return outcome.result
    }
}
