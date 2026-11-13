// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// An extension's argument view borrows the VM's registers for the call only; a retained view must read as empty instead
/// of reading another handler's registers.
final class ExtensionArgumentLifetimeTests: XCTestCase {
    private final class RetainingExtension: GesExtensionRegistry, GesExtensionFunction {
        var retained = GesValueArguments()
        var seenDuringCall: GesValue = .nothing

        func resolve(_ reference: GesExtensionReference) throws -> (any GesExtensionFunction)? { self }

        func invoke(_ call: GesExtensionCall) throws {
            call.setInteger(-1)
            seenDuringCall = call.arguments[0]
            retained = call.arguments
            call.setValue(seenDuringCall)
        }
    }

    func testRetainedArgumentsReadAsEmptyAfterTheirCall() throws {
        let extensions = RetainingExtension()
        let program = try GesProgramBuilder().addScript("on Start(a) { let value be :app.keep(a); emit Done(value: value) }\non Other(b) { let value be b + 1; emit Done(value: value) }").compile()
        let host = try GameEventScriptHost.makeBuilder().withExtensionRegistry(extensions).build().startForTest()
        _ = try host.load(program)
        XCTAssertTrue(try host.receive(.init(name: "Start", arguments: [.init(name: "a", value: .integer(7))])))
        _ = try host.runToCompletion()
        XCTAssertEqual(extensions.seenDuringCall, .integer(7))
        XCTAssertTrue(try host.receive(.init(name: "Other", arguments: [.init(name: "b", value: .integer(40))])))
        _ = try host.runToCompletion()
        XCTAssertEqual(extensions.retained.count, 0)
        XCTAssertEqual(extensions.retained[0], .nothing)
    }
}
