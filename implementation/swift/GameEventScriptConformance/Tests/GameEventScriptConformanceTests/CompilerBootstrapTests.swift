// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// Native compiler ownership and source-map representation controls.
final class CompilerBootstrapTests: XCTestCase {
    func testCompilerBuilderOwnershipAndOptions() throws {
        let builder = GesProgramBuilder().addScript("on Start() { emit First() }").addScript("on Start() { emit Second(1, 2, 3) }")
        let program = try builder.compile()
        XCTAssertEqual(program.sourceArchive?.map(\.sourceID), [0, 1])
        XCTAssertEqual(program.sourceArchive?.map(\.sourceName), ["UnknownSource", "UnknownSource"])
        XCTAssertNoThrow(try GameEventScriptProgram.read(program.encode()))
        let minimal = try builder.withDebugInfo(.none).withProgramVersion(42).compile()
        XCTAssertNil(minimal.sourceArchive)
        XCTAssertNil(minimal.sourceMap)
        XCTAssertNil(minimal.debugSymbols)
        XCTAssertEqual(minimal.programVersion, 42)
        XCTAssertNotNil(program.sourceArchive)
        XCTAssertThrowsError(try builder.compile(options: .init(debugInfo: .init(rawValue: 128)))) { guard case GesApiError.invalidArgument = $0 else { return XCTFail("Unexpected error: \($0)") } }
    }

    func testSourceMapCoalescesSpansAndKeepsConstantUseLocation() throws {
        let source = "constant $limit be 7\non Start(value) { let sorted be value[:order by item => item ascending]; emit Done(value + $limit) }"
        let program = try GesProgramBuilder().addScript(source).compile()
        let entries = try XCTUnwrap(program.sourceMap?.entries)
        XCTAssertTrue(entries.contains { $0.codeLength > 1 })
        for (previous, current) in zip(entries, entries.dropFirst()) {
            XCTAssertFalse(
                previous.codeStart + previous.codeLength == current.codeStart && previous.sourceID == current.sourceID
                    && previous.sourceStartByteOffset == current.sourceStartByteOffset && previous.sourceByteLength == current.sourceByteLength
            )
        }
        let address = try XCTUnwrap(program.code.firstIndex { $0.opcode == .loadInteger })
        let span = try XCTUnwrap(entries.first { $0.codeStart <= address && address < $0.codeStart + $0.codeLength })
        let bytes = Array(source.utf8)
        XCTAssertEqual(String(decoding: bytes[Int(span.sourceStartByteOffset)..<Int(span.sourceStartByteOffset + span.sourceByteLength)], as: UTF8.self), "$limit")
    }

}
