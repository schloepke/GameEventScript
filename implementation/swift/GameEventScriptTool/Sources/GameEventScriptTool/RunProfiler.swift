// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Dispatch
import Foundation
import GameEventScriptRuntime

final class RunProfiler: GameEventScriptProfiler {
    final class Measurement: GameEventScriptProgramProfiler {
        let program: GameEventScriptProgram
        var counts: [UInt64]
        var nanoseconds: [UInt64]
        var phaseNanoseconds: [UInt64]
        var phaseTotals = [UInt64](repeating: 0, count: 6)
        var phaseCounts = [UInt64](repeating: 0, count: 6)
        private var prelude = [UInt64](repeating: 0, count: 6)
        private var phase = -1
        private let clock: () -> UInt64
        private var pending = -1
        private var start: UInt64 = 0

        init(_ program: GameEventScriptProgram, clock: @escaping () -> UInt64) {
            self.program = program
            self.clock = clock
            phaseNanoseconds = .init(repeating: 0, count: program.code.count * 6)
            counts = .init(repeating: 0, count: program.code.count)
            nanoseconds = .init(repeating: 0, count: program.code.count)
        }

        func phaseStarting(_ next: GameEventScriptProfilePhase) {
            closePhase()
            if next == .stateCheck { pending = -1 }
            phase = next.rawValue
            start = clock()
        }

        func instructionStarting(_ address: Int) {
            closePhase()
            counts[address] += 1
            pending = address
            for index in 0..<6 {
                phaseNanoseconds[address * 6 + index] += prelude[index]
                nanoseconds[address] += prelude[index]
                prelude[index] = 0
            }
            phase = GameEventScriptProfilePhase.execute.rawValue
            start = clock()
        }

        private func closePhase() {
            guard phase >= 0 else { return }
            let end = clock()
            let elapsed = end >= start ? end - start : 0
            phaseTotals[phase] += elapsed
            phaseCounts[phase] += 1
            if pending >= 0 {
                phaseNanoseconds[pending * 6 + phase] += elapsed
                nanoseconds[pending] += elapsed
            } else {
                prelude[phase] += elapsed
            }
            phase = -1
        }

        func finishSlice() {
            closePhase()
            pending = -1
            for index in 0..<6 { prelude[index] = 0 }
        }
    }

    var outcome = "Incomplete or failed"
    private(set) var programs: [Measurement] = []
    private let clock: () -> UInt64

    init(clock: @escaping () -> UInt64 = { DispatchTime.now().uptimeNanoseconds }) { self.clock = clock }

    func createProgramProfiler(_ program: GameEventScriptProgram) -> any GameEventScriptProgramProfiler {
        let result = Measurement(program, clock: clock)
        programs.append(result)
        return result
    }

    private struct Row {
        let name: String
        var count: UInt64
        var ns: Double
        var phases: [Double]
    }

    func markdown() -> String {
        var instructions: [Row] = []
        var opcodes: [String: Row] = [:]
        var sources: [String: Row] = [:]

        func add(_ rows: inout [String: Row], _ key: String, _ count: UInt64, _ ns: Double, _ phases: [Double]) {
            var row = rows[key] ?? Row(name: key, count: 0, ns: 0, phases: .init(repeating: 0, count: 6))
            row.count += count
            row.ns += ns
            for index in 0..<6 { row.phases[index] += phases[index] }
            rows[key] = row
        }

        for (index, measurement) in programs.enumerated() {
            let program = measurement.program
            for address in program.code.indices {
                let count = measurement.counts[address]
                if count == 0 { continue }
                let ns = Double(measurement.nanoseconds[address])
                let phases = (0..<6).map { Double(measurement.phaseNanoseconds[address * 6 + $0]) }
                let rawName = String(describing: program.code[address].opcode)
                let opcode = rawName.prefix(1).uppercased() + rawName.dropFirst()
                var source = "[unmapped]"
                var sourceID = "none"
                if let map = program.sourceMap,
                    let entry = map.entries.first(where: { address >= Int($0.codeStart) && address < Int($0.codeStart + $0.codeLength) }),
                    let document = map.sources.first(where: { $0.sourceID == entry.sourceID })
                {
                    let line = document.lineStartByteOffsets.filter { $0 <= entry.sourceStartByteOffset }.count
                    source = "\(document.sourceName):\(line)"
                    sourceID = String(document.sourceID)
                }
                add(&opcodes, opcode, count, ns, phases)
                add(&sources, "\(index + 1)/\(sourceID) / \(source)", count, ns, phases)
                instructions.append(.init(name: "\(index + 1):\(address) / \(opcode) / \(source)", count: count, ns: ns, phases: phases))
            }
        }
        let totals = (0..<6).map { phase in programs.reduce(0.0) { $0 + Double($1.phaseTotals[phase]) } }
        let total = totals.reduce(0, +)
        var output = "# GES opcode profile\n\n"
        output += "Run status: \(Self.escape(outcome)).\n\n"
        output += "Implementation: Swift CLI \(Self.escape(ToolBuildInfo.version)). Platform: \(Self.escape(ProcessInfo.processInfo.operatingSystemVersionString)).\n"
        output += "\nInstruction starts: \(instructions.reduce(UInt64(0)) { $0 + $1.count }). Measured VM time: \(Self.number(total / 1_000_000)) ms. Clock frequency: 1000000000 Hz.\n"
        output +=
            "\nInstrumented wall time, not uninstrumented execution cost. Profiler bookkeeping is excluded; clock/callback overhead remains and can dominate short phases. Calls are exclusive; synchronous extension work is charged to its calling opcode.\n"
        output +=
            "\nCompilation, loading, native message handlers, queue waits and time between slices are excluded. Initialization is included. Faulting instructions count as starts; a failure report may be partial. No source map means [unmapped]. Shares use total measured VM time.\n"
        output += "\n## Programs\n\n| ID | Module | Code instructions |\n| --- | --- | ---: |\n"
        for (index, measurement) in programs.enumerated() { output += "| \(index + 1) | \(Self.escape(measurement.program.moduleName)) | \(measurement.program.code.count) |\n" }

        func table(_ title: String, _ rows: [Row], limit: Int? = nil) {
            let sorted = rows.sorted { $0.ns == $1.ns ? $0.name.utf8.lexicographicallyPrecedes($1.name.utf8) : $0.ns > $1.ns }
            output += "\n## \(title)\n\n"
            if let limit { output += "Top \(min(limit, sorted.count)) of \(sorted.count), sorted by measured time. Counts are instruction starts.\n\n" }
            output += "| Location | Executions | Total ms | Share | Mean ns | State ns | Budget ns | Slice ns | Fetch ns | Execute ns | Advance ns |\n| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |\n"
            for row in sorted.prefix(limit ?? sorted.count) {
                output +=
                    "| \(Self.escape(row.name)) | \(row.count) | \(Self.number(row.ns / 1_000_000)) | \(Self.number(total == 0 ? 0 : row.ns / total * 100))% | \(Self.number(row.ns / Double(row.count))) | \(row.phases.map { Self.number($0 / Double(row.count)) }.joined(separator: " | ")) |\n"
            }
        }

        output += "\n## Loop phases\n\n| Phase | Visits | Total ms |\n| --- | ---: | ---: |\n"
        for phase in GameEventScriptProfilePhase.allCases {
            let visits = programs.reduce(UInt64(0)) { $0 + $1.phaseCounts[phase.rawValue] }
            output += "| \(phase) | \(visits) | \(Self.number(totals[phase.rawValue] / 1_000_000)) |\n"
        }
        output +=
            "\nTerminal checks without an instruction: \(Self.number((total - instructions.reduce(0) { $0 + $1.ns }) / 1_000_000)) ms. Phase columns below are mean nanoseconds per instruction start. Execute includes opcode dispatch. Budget reservation/completion is outside the loop and excluded.\n"
        table("Opcodes", Array(opcodes.values))
        table("Source lines", Array(sources.values), limit: 100)
        table("Instructions", instructions, limit: 100)
        return output
    }

    private static func number(_ value: Double) -> String { String(format: "%.3f", locale: Locale(identifier: "en_US_POSIX"), value) }

    private static func escape(_ value: String) -> String {
        value.replacingOccurrences(of: "&", with: "&amp;").replacingOccurrences(of: "<", with: "&lt;").replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "[", with: "&#91;").replacingOccurrences(of: "]", with: "&#93;").replacingOccurrences(of: "|", with: "&#124;").replacingOccurrences(of: "`", with: "&#96;").replacingOccurrences(of: "\r", with: " ")
            .replacingOccurrences(of: "\n", with: " ")
    }

    func write(_ path: String) throws { try ToolFiles.write(Data(markdown().utf8), to: path) }
}
