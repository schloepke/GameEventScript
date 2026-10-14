// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

/// Native buffer ownership and reader-retention checks; portable binary assertions live in Python.
final class ProgramBinaryBootstrapTests: XCTestCase {
    func testHostsShareProgramIdentityAndReturnedArraysRemainIsolated() throws {
        let program = try GesProgramBuilder().addScript("on Main() { emit Done(1) }").compile()
        let firstHost = try GameEventScriptHost.makeBuilder().build()
        let secondHost = try GameEventScriptHost.makeBuilder().build()
        let first = try firstHost.load(program)
        let second = try secondHost.load(program)
        XCTAssertTrue(first.program === program)
        XCTAssertTrue(second.program === program)
        let bytes = try program.encode()
        var strings = program.stringConstants.data
        strings.append(0x41)
        var indexes = program.uint16IndexLists.data
        indexes.append(0)
        if !indexes.isEmpty { indexes[0] = UInt16.max }
        var code = program.code.instructions
        code.removeAll()
        var bindings = program.bindings.entries
        bindings.removeAll()
        XCTAssertEqual(try program.encode(), bytes)
        let decoded = try GameEventScriptProgram.read(bytes)
        XCTAssertFalse(decoded === program)
        XCTAssertEqual(try decoded.encode(), bytes)
    }

    func testWriterBuffersAndReaderRetention() throws {
        let builder = GesProgramBuilder()
        builder.addScript("module bootstrap.binary\non Main() { emit Done(1) }", sourceName: "binary.ges")
        let program = try builder.compile()
        let bytes = try program.encode()
        XCTAssertEqual(try program.encodedSize(), bytes.count)
        XCTAssertEqual(try GameEventScriptProgram.read(bytes).encode(), bytes)

        var destination = [UInt8](repeating: 0xaa, count: bytes.count + 3)
        XCTAssertEqual(try program.write(into: &destination), bytes.count)
        XCTAssertEqual(Array(destination.prefix(bytes.count)), bytes)
        XCTAssertEqual(Array(destination.suffix(3)), [0xaa, 0xaa, 0xaa])
        var shortDestination = [UInt8](repeating: 0x5a, count: bytes.count - 1)
        let original = shortDestination
        XCTAssertThrowsError(try program.write(into: &shortDestination))
        XCTAssertEqual(shortDestination, original, "Capacity failure must preserve caller storage")

        XCTAssertNotNil(program.debugSymbols)
        XCTAssertNotNil(program.sourceMap)
        XCTAssertNotNil(program.sourceArchive)
        let runtime = try GameEventScriptProgram.read(bytes, options: .init(retention: .runtimeOnly))
        XCTAssertNil(runtime.debugSymbols)
        XCTAssertNil(runtime.sourceMap)
        XCTAssertNil(runtime.sourceArchive)
        XCTAssertNil(runtime.buildMetadata)
        XCTAssertTrue(runtime.opaqueSections.isEmpty)
        let known = try GameEventScriptProgram.read(bytes, options: .init(retention: .preserveKnown))
        XCTAssertNotNil(known.sourceArchive)
        XCTAssertTrue(known.opaqueSections.isEmpty)
    }
}
