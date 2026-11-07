// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// Sources at the expression and statement nesting limits compile on a 512 KB thread stack, the stack of Swift
/// concurrency threads that hosts compile on; one level more reports the limit instead of exhausting the stack.
final class CompilerStackDepthTests: XCTestCase {
    private static func repeated(_ text: String, _ count: Int) -> String { String(repeating: text, count: count) }

    // The deepest forms of the recursive parser and compiler passes, each with the nesting count that reaches the
    // limit: calls in nested blocks, labeled constructors, selector pipelines, lists, a predicate of nested sums.
    private static let deepestForms: [(name: String, limit: Int, source: @Sendable (Int) -> String)] = [
        (
            "calls in blocks", 31,
            { n in "function f(_ x) be x\non Start(a) {\n" + repeated("if a > 1 {\n", 31) + "emit Out(" + repeated("f(", n) + "a" + repeated(")", n) + ")\n" + repeated("}\n", 31) + "}" }
        ),
        ("vectors", 31, { n in "on Start() { emit Out(" + repeated(":Vector(x: ", n) + "1" + repeated(", y: 1)", n) + ") }" }),
        ("pipelines", 31, { n in "on Start(a) { emit Out(" + (0..<n).map { "a[:select x\($0) => " }.joined() + "1" + repeated("]", n) + ") }" }),
        ("lists", 31, { n in "on Start() { emit Out(" + repeated("[", n) + "1" + repeated("]", n) + ") }" }),
        ("predicate", 10, { n in "function f(_ x) be x\npredicate blocked(_ x) be " + repeated("f(x + (", n) + "x" + repeated("))", n) + " > 1\non Start(a) { emit Out(blocked(a)) }" }),
        ("blocks", 32, { n in "on Start(a) {\n" + repeated("if a > 1 {\n", n) + "emit Out(a)\n" + repeated("}\n", n) + "}" }),
    ]

    func testSourcesAtTheNestingLimitsCompileOnASmallStack() {
        for form in Self.deepestForms { XCTAssertEqual(Self.compileOnSmallStack(form.source(form.limit)), [], form.name) }
    }

    func testSourcesBeyondTheNestingLimitsReportTheLimit() {
        for form in Self.deepestForms { XCTAssertEqual(Self.compileOnSmallStack(form.source(form.limit + 1)), ["parse.sourceNestingExceeded"], form.name) }
    }

    // The diagnostic codes of compiling `source` on a thread with a 512 KB stack; empty when compilation succeeds.
    private static func compileOnSmallStack(_ source: String) -> [String] {
        // Written by the compiling thread before it signals and read only after the wait.
        final class Outcome: @unchecked Sendable {
            var codes: [String] = []
        }

        let outcome = Outcome()
        let done = DispatchSemaphore(value: 0)
        let thread = Thread {
            do {
                _ = try GesProgramBuilder().addScript(source).compile()
            } catch let error as GesCompileError {
                outcome.codes = error.diagnostics.map(\.code)
            } catch {
                outcome.codes = ["\(error)"]
            }
            done.signal()
        }
        thread.stackSize = 512 * 1024
        thread.start()
        done.wait()
        return outcome.codes
    }
}
