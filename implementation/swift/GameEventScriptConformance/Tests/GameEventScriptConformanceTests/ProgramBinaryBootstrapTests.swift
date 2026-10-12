// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// Native buffer ownership and reader-retention checks; portable binary assertions live in Python.
final class ProgramBinaryBootstrapTests: XCTestCase {
    func testWriterBuffersAndReaderRetention() throws {
        let builder = GameEventScriptBuilder.create()
        builder.addScript("module bootstrap.binary\non Main() { emit Done(1) }", sourceName: "binary.ges")
        let program = try builder.compile()
        let bytes = try GameEventScriptProgramWriter.bytes(program)
        XCTAssertEqual(try GameEventScriptProgramWriter.encodedSize(program), bytes.count)
        XCTAssertEqual(try GameEventScriptProgramWriter.bytes(GameEventScriptProgramReader.read(bytes)), bytes)

        var destination = [UInt8](repeating: 0xaa, count: bytes.count + 3)
        XCTAssertEqual(try GameEventScriptProgramWriter.write(program, into: &destination), bytes.count)
        XCTAssertEqual(Array(destination.prefix(bytes.count)), bytes)
        XCTAssertEqual(Array(destination.suffix(3)), [0xaa, 0xaa, 0xaa])
        var shortDestination = [UInt8](repeating: 0x5a, count: bytes.count - 1)
        let original = shortDestination
        XCTAssertThrowsError(try GameEventScriptProgramWriter.write(program, into: &shortDestination))
        XCTAssertEqual(shortDestination, original, "Capacity failure must preserve caller storage")

        XCTAssertNotNil(program.debugSymbols)
        XCTAssertNotNil(program.sourceMap)
        XCTAssertNotNil(program.sourceArchive)
        let runtime = try GameEventScriptProgramReader.read(bytes, options: .init(retention: .runtimeOnly))
        XCTAssertNil(runtime.debugSymbols)
        XCTAssertNil(runtime.sourceMap)
        XCTAssertNil(runtime.sourceArchive)
        XCTAssertNil(runtime.buildMetadata)
        XCTAssertTrue(runtime.opaqueSections.isEmpty)
        let known = try GameEventScriptProgramReader.read(bytes, options: .init(retention: .preserveKnown))
        XCTAssertNotNil(known.sourceArchive)
        XCTAssertTrue(known.opaqueSections.isEmpty)
    }
}
