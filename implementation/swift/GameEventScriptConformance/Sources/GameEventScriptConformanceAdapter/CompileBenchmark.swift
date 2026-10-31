// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime

// Compiles every source group of a request repeatedly; inputs are prepared outside the timed rounds.
enum CompileBenchmark {
    private struct Unit {
        let sources: [(text: String, name: String)]
        let externalTypes: Bool
        let options: GesCompileOptions
    }

    private static let compiledKinds: Set<String> = ["bytecodeSnapshot", "compileError", "bytecode", "compileMetadata", "programBinary", "scriptApi", "loadError"]

    static func run(_ input: Input, rounds: Int) throws -> [String: Any] {
        var units: [Unit] = []
        for test in input.values("cases") {
            let kind = try test.text("kind")
            guard compiledKinds.contains(kind) else { continue }
            var options = GesCompileOptions()
            options.debugInfo = []
            for item in test["compile"]?.values("debugInfo") ?? [] {
                switch item.stringValue {
                case "debugSymbols": options.debugInfo.insert(.symbols)
                case "sourceMap": options.debugInfo.insert(.sourceMap)
                case "sourceArchive": options.debugInfo.insert(.sourceArchive)
                default: throw ConformanceExecutionError.invalidInput("Unknown debug flag")
                }
            }
            var groups: [(program: String, sources: [(text: String, name: String)])] = []
            for source in test.values("sources") {
                let program = try source.text("program")
                let entry = (text: try source.text("text"), name: try source.text("name"))
                if let index = groups.firstIndex(where: { $0.program == program }) { groups[index].sources.append(entry) } else { groups.append((program, [entry])) }
            }
            for group in groups { units.append(Unit(sources: group.sources, externalTypes: ["scriptApi", "loadError"].contains(kind), options: options)) }
        }

        let failures = compileAll(units)
        var nanoseconds: [UInt64] = []
        for _ in 0..<rounds {
            let start = DispatchTime.now().uptimeNanoseconds
            _ = compileAll(units)
            nanoseconds.append(DispatchTime.now().uptimeNanoseconds - start)
        }
        return ["units": units.count, "failures": failures, "nanoseconds": nanoseconds, "allocatedBytes": NSNull()]
    }

    private static func compileAll(_ units: [Unit]) -> Int {
        var failures = 0
        for unit in units {
            let builder = GesProgramBuilder()
            if unit.externalTypes { builder.withExternalTypeCatalog(RuntimeFixtures.catalog) }
            for source in unit.sources { builder.addScript(source.text, sourceName: source.name) }
            do { _ = try builder.compile(options: unit.options) } catch { failures += 1 }
        }
        return failures
    }
}
