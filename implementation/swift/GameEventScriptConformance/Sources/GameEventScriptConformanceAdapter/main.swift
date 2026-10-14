// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime

let capabilities = ["compiler", "program-binary", "bytecode-snapshot", "host", "vm", "observer", "publish-sink", "external-types", "native-handlers", "value-api", "message-api"]
let kinds = ["bytecodeSnapshot", "compileError", "bytecode", "compileMetadata", "programBinary", "scriptApi", "loadError", "externalTypeApi", "valueApi", "messageApi"]
let identity = ["id": "swift", "version": "development"]
let maxBytes = 32 * 1024 * 1024

do {
    let output: [String: Any]
    if CommandLine.arguments.dropFirst().elementsEqual(["--describe"]) {
        output = [
            "formatVersion": 1, "implementation": identity, "capabilities": capabilities, "supportedKinds": kinds, "supportedBinarySteps": true, "performanceProfiles": SwiftPerformanceProvider.available ? [SwiftPerformanceProvider.profile] : [],
        ]
    } else {
        let measuring = CommandLine.arguments.dropFirst().elementsEqual(["--measure"])
        let foreign = CommandLine.arguments.count == 3 && CommandLine.arguments[1] == "--programs"
        if foreign {
            let data = try Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[2]))
            guard data.count <= 128 * 1024 * 1024 else { throw ConformanceExecutionError.invalidInput("Foreign inputs too large") }
            try JSONAudit.check(data)
            ForeignPrograms.inputs = try JSONDecoder().decode(Input.self, from: data)
        }
        guard CommandLine.arguments.count == 1 || measuring || foreign else { throw ConformanceExecutionError.invalidInput("Unknown arguments") }
        var data = Data()
        while let chunk = try FileHandle.standardInput.read(upToCount: 8192), !chunk.isEmpty {
            data.append(chunk)
            if data.count > maxBytes { throw ConformanceExecutionError.invalidInput("Input too large") }
        }
        guard data.count <= maxBytes else { throw ConformanceExecutionError.invalidInput("Input too large") }
        try JSONAudit.check(data)
        let input = try JSONDecoder().decode(Input.self, from: data)
        guard input["formatVersion"]?.integerValue == 1, let digest = input["requestDigest"]?.stringValue, digest.count == 64 else { throw ConformanceExecutionError.invalidInput("Invalid envelope") }
        try Validation.batch(input, measuring: measuring)
        let provider = measuring ? try SwiftPerformanceProvider() : nil
        var observations: [String: Any] = [:]
        for test in input.values("cases") {
            let id = try test.text("id")
            guard observations[id] == nil else { throw ConformanceExecutionError.invalidInput("Duplicate case") }
            let kind = try test.text("kind")
            do {
                if let provider {
                    observations[id] = try provider.observation(test)
                } else if ["loadError", "externalTypeApi", "valueApi", "messageApi"].contains(kind) {
                    observations[id] = try ApiObservations.run(test)
                } else if kind == "programBinary" {
                    observations[id] = try BinaryObservations.run(test)
                } else if kind == "scriptApi" {
                    var programs: [(id: String, program: GameEventScriptProgram)] = []
                    for source in test.values("sources") {
                        let name = try source.text("program")
                        if !programs.contains(where: { $0.id == name }) { programs.append((name, try CompilerObservations.compile(test, programID: name))) }
                    }
                    observations[id] = try ["status": "ok", "runtime": runtime(programs, test.required("runtime"))]
                } else {
                    observations[id] = try CompilerObservations.observe(kind, CompilerObservations.compile(test))
                }
            } catch let error as GesCompileError {
                if ["scriptApi", "loadError", "programBinary"].contains(kind) {
                    observations[id] = ["status": "error", "detail": "Runtime input compilation failed."]
                } else {
                    observations[id] = ["status": "compileError", "diagnostics": error.diagnostics.map(CompilerObservations.diagnostic)]
                }
            } catch { observations[id] = ["status": "error", "detail": String(describing: error)] }
        }
        output = ["formatVersion": 1, "requestDigest": digest, "implementation": identity, "capabilities": capabilities, "observations": observations]
    }
    let data = try JSONSerialization.data(withJSONObject: output, options: [.sortedKeys])
    guard data.count <= maxBytes else { throw ConformanceExecutionError.invalidInput("Output too large") }
    FileHandle.standardOutput.write(data)
} catch {
    FileHandle.standardError.write(Data((String(describing: error) + "\n").utf8))
    exit(2)
}
