// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

// Executable-only probe: unexpected errors and writer failures must escape.
enum BinaryFuzzProbe {
    static func run(input: String, output: String) throws {
        var limits = GameEventScriptProgramReadLimits()
        limits.maxFileBytes = 32768
        limits.maxSectionCount = 64
        limits.maxSourceArchiveBytes = 16384
        limits.maxRetainedOpaqueBytes = 16384
        limits.maxInstructions = 2048
        limits.maxBindings = 256
        limits.maxStringEntries = 2048
        limits.maxIndexLists = 2048
        let options = GameEventScriptProgramReadOptions(limits: limits)
        let lines = try String(contentsOfFile: input, encoding: .utf8).components(separatedBy: "\n").dropLast()
        try Data().write(to: URL(fileURLWithPath: output))
        let file = try FileHandle(forWritingTo: URL(fileURLWithPath: output))
        defer { file.closeFile() }
        for line in lines {
            guard let data = Data(base64Encoded: line) else { throw ToolError.invalidArguments("Invalid fuzz transport") }
            let program: GameEventScriptProgram
            do {
                program = try GameEventScriptProgramReader.read(Array(data), options: options)
            } catch let error as GameEventScriptProgramFormatError {
                file.write(Data(("reject:" + String(describing: error.code) + "\n").utf8))
                continue
            }
            let canonical = try GameEventScriptProgramWriter.bytes(program)
            let second = try GameEventScriptProgramWriter.bytes(GameEventScriptProgramReader.read(canonical, options: options))
            guard canonical == second else { throw ToolError.invalidFixture("Unstable accepted binary rewrite") }
            file.write(Data(("accept:" + Data(canonical).base64EncodedString() + "\n").utf8))
        }
    }
}
