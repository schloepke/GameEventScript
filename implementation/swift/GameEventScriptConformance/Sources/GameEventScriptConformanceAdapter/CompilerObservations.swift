// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime

func nullable<T>(_ value: T?) -> Any { value.map { $0 as Any } ?? NSNull() }

func title(_ value: Any) -> String {
    let s = String(describing: value)
    return s.prefix(1).uppercased() + s.dropFirst()
}

enum CompilerObservations {
    static func compile(_ test: Input, programID: String? = nil, version: UInt64 = 0) throws -> GameEventScriptProgram {
        if ForeignPrograms.inputs != nil { return try ForeignPrograms.read(test, group: programID) }
        let builder = GameEventScriptBuilder.create()
        if ["scriptApi", "loadError"].contains(test["kind"]?.stringValue ?? "") { builder.withExternalTypeCatalog(RuntimeFixtures.catalog) }
        for source in test.values("sources") where programID == nil || source["program"]?.stringValue == programID {
            builder.addScript(try source.text("text"), sourceName: try source.text("name"))
        }
        var options = GameEventScriptCompileOptions()
        options.debugInfo = []
        options.programVersion = version
        for item in test["compile"]?.values("debugInfo") ?? [] {
            switch item.stringValue {
            case "debugSymbols": options.debugInfo.insert(.symbols)
            case "sourceMap": options.debugInfo.insert(.sourceMap)
            case "sourceArchive": options.debugInfo.insert(.sourceArchive)
            default: throw ConformanceExecutionError.invalidInput("Unknown debug flag")
            }
        }
        let program = try builder.compile(options: options)
        return test["compile"]?["binaryRoundTrip"]?.boolValue == true ? try GameEventScriptProgramReader.read(GameEventScriptProgramWriter.bytes(program)) : program
    }

    static func diagnostic(_ value: GameEventScriptDiagnostic) -> [String: Any] {
        [
            "phase": String(describing: value.phase), "code": value.code, "symbol": nullable(value.symbol),
            "symbolKind": value.symbolKind == .unknown ? NSNull() : String(describing: value.symbolKind) as Any,
            "sourceName": nullable(value.sourceLocation?.sourceName), "line": nullable(value.sourceLocation?.line), "column": nullable(value.sourceLocation?.column),
            "endLine": nullable(value.sourceLocation?.endLine), "endColumn": nullable(value.sourceLocation?.endColumn),
            "programName": nullable(value.programName), "handlerName": nullable(value.handlerName),
        ]
    }

    static func observe(_ kind: String, _ program: GameEventScriptProgram) -> [String: Any] {
        if kind == "bytecodeSnapshot" { return ["status": "ok", "dump": GameEventScriptProgramDumper.dump(program)] }
        if kind == "compileError" { return ["status": "ok"] }
        if kind == "bytecode" {
            var counts: [String: Int] = [:]
            for instruction in program.code { counts[title(instruction.opcode), default: 0] += 1 }
            return ["status": "ok", "opcodes": counts]
        }
        var signatures: [String: [String]] = [:]
        var handlers: [[String: Any]] = []
        for bind in program.bindings where bind.kind == .messageHandler || bind.kind == .messageNameHandler {
            let name = program.stringConstants[Int(bind.name)]
            let parameters = bind.argumentNames.map { program.stringConstants[Int($0)] }
            let signature = try! GameEventScriptMessageSignature(name: name, parameters: parameters).signatureId
            signatures[name, default: []].append(signature)
            handlers.append(["name": name, "signatureId": signature, "requiredRegisterCount": bind.requiredRegisterCount, "requiredCallStackDepth": bind.requiredCallStackDepth])
        }
        return [
            "status": "ok",
            "metadata": [
                "messageDefinitions": signatures.map { ["name": $0.key, "count": $0.value.count, "signatureIds": $0.value] as [String: Any] },
                "programResources": ["requiredRegisterCount": program.requiredRegisterCount, "requiredCallStackDepth": program.requiredCallStackDepth], "handlerResources": handlers,
            ],
        ]
    }
}
