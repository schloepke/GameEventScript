// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

enum BinaryObservations {
    static func run(_ test: Input) throws -> [String: Any] {
        let input = try test.required("binary")
        guard let bytes = Data(base64Encoded: try input.text("data")) else { throw ConformanceExecutionError.invalidInput("Invalid Base64") }
        let program: GameEventScriptProgram
        do { program = try GameEventScriptProgramReader.read(Array(bytes)) } catch let error as GesProgramFormatError {
            return [
                "status": "ok",
                "binary": [
                    "outcome": error.code.rawValue <= GesProgramFormatErrorCode.tooManyEntries.rawValue ? "readError" : "validationError", "errorCode": title(error.code),
                    "byteOffset": nullable(error.byteOffset), "sectionType": nullable(error.sectionType), "entryIndex": nullable(error.entryIndex),
                ],
            ]
        }
        let rewritten = try GameEventScriptProgramWriter.bytes(program)
        let compare = input["compareCompiledRuntime"]?.boolValue == true
        let compiled = compare ? try GameEventScriptProgramWriter.bytes(CompilerObservations.compile(test, version: UInt64(input.text("programVersion"))!)) : []
        var result: [String: Any] = [
            "status": "ok",
            "binary": [
                "outcome": "valid", "moduleName": program.moduleName,
                "requiredRegisterCount": program.requiredRegisterCount, "requiredCallStackDepth": program.requiredCallStackDepth, "opaqueSectionCount": program.opaqueSections.count,
                "programVersion": program.programVersion, "buildMetadata": nullable(program.buildMetadata.map { ["compilerId": $0.compilerID, "compilerVersion": $0.compilerVersion] }),
                "rewritten": Data(rewritten).base64EncodedString(), "runtimeSections": compare ? Data(requiredSections(rewritten)).base64EncodedString() as Any : NSNull(),
                "compiledRuntimeSections": compare ? Data(requiredSections(compiled)).base64EncodedString() as Any : NSNull(),
            ],
        ]
        if input["includeDump"]?.boolValue == true { result["dump"] = GameEventScriptProgramDumper.dump(program) }
        if let request = input["runtime"], request != .null { result["runtime"] = try runtime([(id: "main", program: program)], request) }
        return result
    }

    static func requiredSections(_ bytes: [UInt8]) -> [UInt8] {
        var result: [UInt8] = []
        var offset = 16
        while offset < bytes.count {
            let length = (0..<4).reduce(0) { $0 | Int(bytes[offset + 8 + $1]) << ($1 * 8) } + 12
            if bytes[offset + 2] & 1 != 0 { result.append(contentsOf: bytes[offset..<offset + length]) }
            offset += length
        }
        return result
    }
}
