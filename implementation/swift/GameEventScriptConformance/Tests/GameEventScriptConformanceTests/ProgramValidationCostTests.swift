// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import XCTest

@testable import GameEventScriptRuntime

/// Untrusted Programs may reference one long list from many instructions. Validation must stay linear in the list and code
/// sizes; scanning the list per reference took hours for files of about a megabyte.
final class ProgramValidationCostTests: XCTestCase {
    func testManyReferencesToOneLongRegisterListAreRejectedQuickly() throws {
        let builder = GesProgramBuilder()
        builder.addScript("module longlist\non Start(values) { for key, value in values { emit Pair(key: key, value: value) } }", sourceName: "longlist.ges")
        builder.withDebugInfo(.none)
        let program = try builder.compile()
        let next = try XCTUnwrap(program.code.instructions.first { $0.opcode == .iteratorNext && $0.unitAndFlags & 0x20 != 0 })
        var lists = program.uint16IndexLists.map { Array($0) }
        // Distinct registers beyond every frame: each reference is invalid, but only the frame check can tell.
        lists.append((0..<20_000).map { UInt16($0 + 64) })
        let reference = GesBytecodeInstruction(opcode: next.opcode, unitAndFlags: next.unitAndFlags, word0: UInt16(lists.count - 1), word1: next.word1, word2: next.word2, payload: next.payload)
        let crafted = GameEventScriptProgram(
            formatVersion: program.formatVersion,
            moduleName: program.moduleName,
            programVersion: program.metadata.programVersion,
            requiredRegisterCount: program.metadata.requiredRegisterCount,
            requiredCallStackDepth: program.metadata.requiredCallStackDepth,
            stringConstants: Array(program.stringConstants),
            uint16IndexLists: lists,
            bindings: program.bindings.entries,
            code: program.code.instructions + Array(repeating: reference, count: 4_000),
            debugSymbols: nil,
            sourceMap: nil,
            sourceArchive: nil,
            buildMetadata: nil,
            opaqueSections: []
        )
        let start = Date()
        XCTAssertThrowsError(try crafted.validate()) { error in XCTAssertEqual((error as? GesProgramFormatError)?.code, .invalidOperand) }
        XCTAssertLessThan(Date().timeIntervalSince(start), 5, "Validation must not scan the long list once per reference.")
    }

    func testManyTagAlternativesAreValidatedQuicklyAndSharedAcrossHandlers() throws {
        let tags = (0..<16_384).map { "#t\($0)" }.joined(separator: " | ")
        let builder = GesProgramBuilder()
        builder.addScript("module shared\non Probe() matching \(tags) {}\n", sourceName: "shared.ges")
        builder.withDebugInfo(.none)
        let program = try builder.compile()
        let handler = try XCTUnwrap(program.bindings.entries.first { !$0.requiredTagAlternatives.isEmpty })
        // Many handlers naming one list: the encoder writes the list once, so the binary stays small.
        let copies = (0..<4_096).map { index in
            GesBindingSegment.Entry(
                kind: handler.kind,
                name: handler.name,
                argumentNames: handler.argumentNames,
                entryAddress: handler.entryAddress,
                id: handler.id == .max ? .max : UInt16(index),
                requiredTags: handler.requiredTags,
                excludedTags: handler.excludedTags,
                requiredRegisterCount: handler.requiredRegisterCount,
                requiredCallStackDepth: handler.requiredCallStackDepth,
                requiredTagAlternatives: handler.requiredTagAlternatives
            )
        }
        let crafted = GameEventScriptProgram(
            formatVersion: program.formatVersion,
            moduleName: program.moduleName,
            programVersion: program.metadata.programVersion,
            requiredRegisterCount: program.metadata.requiredRegisterCount,
            requiredCallStackDepth: program.metadata.requiredCallStackDepth,
            stringConstants: Array(program.stringConstants),
            uint16IndexLists: program.uint16IndexLists.map { Array($0) },
            bindings: program.bindings.entries.filter { $0.requiredTagAlternatives.isEmpty } + copies,
            code: program.code.instructions,
            debugSymbols: nil,
            sourceMap: nil,
            sourceArchive: nil,
            buildMetadata: nil,
            opaqueSections: []
        )
        let bytes = try crafted.encode()
        let start = Date()
        let instance = try GameEventScriptHost.makeBuilder().build().load(try GameEventScriptProgram.read(bytes))
        XCTAssertLessThan(Date().timeIntervalSince(start), 5, "Duplicate tags must be found without comparing every pair.")
        // Handlers naming one list share one group array and one resolved copy instead of a parsed or hashed copy each.
        let handlers = instance.program.bindings.entries.filter { !$0.requiredTagAlternatives.isEmpty }
        XCTAssertEqual(handlers.count, 4_096)
        XCTAssertEqual(Set(handlers.map(\.tagAlternativesIdentity)).count, 1)
        let linked = instance.linked.handlers.compactMap(\.requiredTagAlternatives)
        XCTAssertEqual(linked.count, 4_096)
        XCTAssertEqual(Set(linked.map { $0.withUnsafeBufferPointer { $0.baseAddress } }).count, 1)
    }

    func testCompiledAndDecodedProgramsAreMarkedValidated() throws {
        let builder = GesProgramBuilder()
        builder.addScript("module once\non Start() {}", sourceName: "once.ges")
        builder.withDebugInfo(.none)
        let program = try builder.compile()
        XCTAssertTrue(program.isValidated, "The Compiler validates before it returns the Program.")
        XCTAssertTrue(try GameEventScriptProgram.read(program.encode()).isValidated, "The Reader validates before it returns the Program.")
    }
}
