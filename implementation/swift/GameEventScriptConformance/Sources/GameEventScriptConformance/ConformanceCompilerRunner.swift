// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime

/// Compiles original shared sources with Swift and checks their portable expectations.
enum ConformanceCompilerRunner {
    static func compile(_ test: ConformanceCase) throws -> [ConformanceRuntimeProgram] {
        let ids = test.sources.reduce(into: [String]()) { if !$0.contains($1.programID) { $0.append($1.programID) } }
        var options = GameEventScriptCompileOptions()
        if let debug = test.metadata["compile"]?["debugInfo"]?.arrayValue {
            options.debugInfo = []
            for entry in debug {
                switch entry.stringValue {
                case "debugSymbols": options.debugInfo.insert(.symbols)
                case "sourceMap": options.debugInfo.insert(.sourceMap)
                case "sourceArchive": options.debugInfo.insert(.sourceArchive)
                default: break
                }
            }
        }
        options.programVersion = UInt64(test.metadata["binaryFixture"]?["programVersion"]?.numberValue ?? "0") ?? 0
        return try ids.map { id in
            let builder = GameEventScriptBuilder().withExternalTypeCatalog(RuntimeFixtures.catalog)
            for source in test.sources where source.programID == id { builder.addScript(source.text, sourceName: source.name) }
            var program = try builder.compile(options: options)
            if test.metadata["compile"]?["binaryRoundTrip"]?.boolValue == true { program = try GameEventScriptProgramReader.read(GameEventScriptProgramWriter.bytes(program)) }
            return .init(id: id, program: program)
        }
    }

    static func runCase(_ test: ConformanceCase, binaryFixture: [UInt8]? = nil) -> ConformanceCaseResult {
        do {
            let programs = test.kind == "programBinary" && test.metadata["binaryFixture"]?["compareCompiledRuntime"]?.boolValue != true ? [] : try compile(test)
            if test.kind == "compileError" { return result(test, [.init(path: "/error", expected: "compile error", actual: "success")]) }
            if ["bytecode", "compileMetadata"].contains(test.kind), let first = programs.first { return constraints(test, first.program) }
            if test.kind == "bytecodeSnapshot", let first = programs.first { return ConformanceProgramRunner.runDumpCase(test, program: first.program) }
            if test.kind == "programBinary", let binaryFixture { return ConformanceProgramRunner.runBinaryCase(test, bytes: binaryFixture, comparisonProgram: programs.first?.program) }
            return ConformanceRuntimeRunner.runCase(test, programs: programs)
        } catch let failure as GameEventScriptCompileError {
            guard test.kind == "compileError", let expected = test.expectation["error"] else { return result(test, [], technical: String(describing: failure)) }
            let candidates = failure.diagnostics.map { diagnostic -> [ConformanceMismatch] in
                var differences: [ConformanceMismatch] = []

                func check(_ key: String, _ actual: String?) {
                    guard let value = expected[key] else { return }
                    let wanted = value.stringValue ?? value.numberValue
                    if wanted != actual { differences.append(.init(path: "/error/" + key, expected: wanted, actual: actual)) }
                }

                check("phase", String(describing: diagnostic.phase))
                check("code", diagnostic.code)
                check("sourceName", diagnostic.sourceLocation?.sourceName)
                check("symbol", diagnostic.symbol)
                check("programName", diagnostic.programName)
                check("handlerName", diagnostic.handlerName)
                check("symbolKind", String(describing: diagnostic.symbolKind))
                if let location = expected["sourceLocation"] {
                    let values: [String: String?] = [
                        "sourceName": diagnostic.sourceLocation?.sourceName, "moduleName": diagnostic.sourceLocation?.moduleName, "sourceId": diagnostic.sourceLocation?.sourceID.map(String.init),
                        "line": diagnostic.sourceLocation?.line.map(String.init), "column": diagnostic.sourceLocation?.column.map(String.init), "endLine": diagnostic.sourceLocation?.endLine.map(String.init),
                        "endColumn": diagnostic.sourceLocation?.endColumn.map(String.init),
                    ]
                    for entry in location.objectValue ?? [] {
                        let wanted = entry.value.stringValue ?? entry.value.numberValue
                        let actual = values[entry.key] ?? nil
                        if wanted != actual { differences.append(.init(path: "/error/sourceLocation/" + entry.key, expected: wanted, actual: actual)) }
                    }
                }
                return differences
            }
            return result(test, candidates.min(by: { $0.count < $1.count }) ?? [.init(path: "/error", expected: "diagnostic", actual: "none")])
        } catch { return result(test, [], technical: String(describing: error)) }
    }

    static func result(_ test: ConformanceCase, _ differences: [ConformanceMismatch], technical: String? = nil) -> ConformanceCaseResult {
        .init(
            testCase: test,
            status: technical != nil ? "error" : differences.isEmpty ? "passed" : "failed",
            code: technical != nil ? "conformance.runner.unhandledException" : differences.isEmpty ? "conformance.passed" : "conformance.assertion.mismatch",
            missingCapabilities: [],
            mismatches: differences,
            technicalDetails: technical
        )
    }
}

extension ConformanceCompilerRunner {
    static func constraints(_ test: ConformanceCase, _ program: GameEventScriptProgram) -> ConformanceCaseResult {
        var differences: [ConformanceMismatch] = []

        func compare(_ path: String, _ expected: String?, _ actual: String?) { if expected != actual { differences.append(.init(path: path, expected: expected, actual: actual)) } }

        func scalar(_ data: ConformanceData, _ field: String, _ value: Int, _ path: String) { if let expected = data[field] { compare(path + "/" + field, expected.numberValue ?? expected.stringValue, String(value)) } }

        if let expected = test.expectation["opcodes"] {
            let counts = program.code.reduce(into: [String: Int]()) { result, instruction in
                let name = String(describing: instruction.opcode)
                result[name.prefix(1).uppercased() + name.dropFirst(), default: 0] += 1
            }
            for item in expected["contains"]?.arrayValue ?? [] { if let name = item.stringValue, counts[name, default: 0] == 0 { compare("/opcodes/contains/" + name, ">0", "0") } }
            for item in expected["excludes"]?.arrayValue ?? [] { if let name = item.stringValue, counts[name, default: 0] != 0 { compare("/opcodes/excludes/" + name, "0", String(counts[name]!)) } }
            for field in ["counts", "minimumCounts"] {
                for item in expected[field]?.objectValue ?? [] {
                    let expected = Int(item.value.numberValue ?? "") ?? -1
                    let actual = counts[item.key, default: 0]
                    if field == "counts" || actual < expected { compare("/opcodes/" + field + "/" + item.key, String(expected), String(actual)) }
                }
            }
        }
        if let expected = test.expectation["metadata"] {
            if let resource = expected["programResources"] {
                scalar(resource, "requiredRegisterCount", Int(program.requiredRegisterCount), "/metadata/programResources")
                scalar(resource, "requiredCallStackDepth", Int(program.requiredCallStackDepth), "/metadata/programResources")
            }
            let handlers = program.bindings.filter { $0.kind == .messageHandler || $0.kind == .messageNameHandler }

            func signature(_ binding: GameEventScriptBinding) -> String { program.stringConstants[Int(binding.name)] + "(" + binding.argumentNames.map { program.stringConstants[Int($0)] }.joined(separator: ",") + ")" }

            for data in expected["handlerResources"]?.arrayValue ?? [] {
                let name = data["name"]?.stringValue ?? ""
                let binding = handlers.first { b in program.stringConstants[Int(b.name)] == name && (data["signatureId"]?.stringValue == nil || data["signatureId"]?.stringValue == signature(b)) }
                if let binding {
                    scalar(data, "requiredRegisterCount", Int(binding.requiredRegisterCount), "/metadata/handlerResources/" + name)
                    scalar(data, "requiredCallStackDepth", Int(binding.requiredCallStackDepth), "/metadata/handlerResources/" + name)
                } else {
                    compare("/metadata/handlerResources/" + name, "handler", "missing")
                }
            }
            for data in expected["messageDefinitions"]?.arrayValue ?? [] {
                let name = data["name"]?.stringValue ?? ""
                let bindings = handlers.filter { program.stringConstants[Int($0.name)] == name }
                scalar(data, "count", bindings.count, "/metadata/messageDefinitions/" + name)
                if let ids = data["signatureIds"]?.arrayValue { compare("/metadata/messageDefinitions/" + name + "/signatureIds", ids.compactMap(\.stringValue).joined(separator: ";"), bindings.map(signature).joined(separator: ";")) }
            }
        }
        return result(test, differences)
    }
}
