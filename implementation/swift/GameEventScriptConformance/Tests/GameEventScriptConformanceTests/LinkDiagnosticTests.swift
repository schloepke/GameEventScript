// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

final class LinkDiagnosticTests: XCTestCase {
    static let source = "on Boot(bot) {\n  publish Probe(value: :navigation.nope(x: bot))\n}\nfunction probe(_ value) be [1, :navigation.nope(x: value)]\n"

    func linkError(_ program: GameEventScriptProgram) throws -> GesDynamicLinkError {
        let host = try GameEventScriptHost.makeBuilder().build().startForTest()
        var failure: GesDynamicLinkError?
        XCTAssertThrowsError(try host.load(program)) { failure = $0 as? GesDynamicLinkError }
        return try XCTUnwrap(failure)
    }

    func testMissingExtensionNamesTheExtensionAtEachCallSite() throws {
        let program = try GesProgramBuilder().addScript(Self.source, sourceName: "bot.ges").compile()
        let error = try linkError(program)
        XCTAssertEqual(error.diagnostics.count, 2)
        XCTAssertEqual(error.diagnostic.sourceLocation?.line, error.diagnostics[0].sourceLocation?.line)
        let locations = error.diagnostics.map { $0.sourceLocation }
        XCTAssertEqual(locations.map { $0?.sourceName }, ["bot.ges", "bot.ges"])
        XCTAssertEqual(locations.map { [$0?.line, $0?.column, $0?.endLine, $0?.endColumn] }, [[2, 24, 2, 48], [4, 32, 4, 58]])
        XCTAssertEqual(locations.map { $0?.sourceID }, [0, 0])
        for diagnostic in error.diagnostics {
            XCTAssertEqual(diagnostic.code, GesDiagnosticCodes.linkMissingExtension)
            XCTAssertEqual(diagnostic.message, "Unknown extension :navigation.nope(x)")
            XCTAssertEqual(diagnostic.symbol, "navigation.nope(x)")
        }
    }

    // Columns count Unicode scalars after a leading byte order mark, and a carriage return ends no column early.
    func testCallSiteColumnsSkipTheByteOrderMarkAndCountScalars() throws {
        let source = "\u{FEFF}on Boot() { emit A(value: :x.f(1)) }\r\non Tick() {\r\n  emit B(t: 'äö𝄞', value: :x.f(2))\r\n}\r\n"
        let error = try linkError(GesProgramBuilder().addScript(source, sourceName: "bot.ges").compile())
        let locations = error.diagnostics.map { $0.sourceLocation }
        XCTAssertEqual(locations.map { [$0?.line, $0?.column, $0?.endLine, $0?.endColumn] }, [[1, 27, 1, 34], [3, 27, 3, 34]])
    }

    func testHostLimitDiagnosticsHaveAMessage() throws {
        let program = try GesProgramBuilder().addScript("on Start(a, b, c) { emit Done(value: a + b + c) }\n", sourceName: "bot.ges").compile()
        var limits = GesRuntimeLimits()
        limits.maxRegisterValues = 1
        let host = try GameEventScriptHost.makeBuilder().withRuntimeLimits(limits).build().startForTest()
        var failure: GesDynamicLinkError?
        XCTAssertThrowsError(try host.load(program)) { failure = $0 as? GesDynamicLinkError }
        XCTAssertEqual(try XCTUnwrap(failure).diagnostic.code, GesDiagnosticCodes.linkRequiredRegisterCountExceeded)
        XCTAssertTrue(try XCTUnwrap(failure).diagnostic.message.contains("registers"))
    }

    func testMissingExtensionWithoutDebugInformationKeepsTheSymbol() throws {
        let program = try GesProgramBuilder().addScript(Self.source, sourceName: "bot.ges").withDebugInfo(.none).compile()
        let error = try linkError(program)
        XCTAssertEqual(error.diagnostics.count, 1)
        XCTAssertEqual(error.diagnostic.symbol, "navigation.nope(x)")
        XCTAssertNil(error.diagnostic.sourceLocation)
    }
}
