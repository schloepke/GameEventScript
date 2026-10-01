// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

@testable import GameEventScriptTool

final class ProfilerTests: XCTestCase {
    func testInstancesAcrossSingleOpcodeSlicesExcludeOutsideTime() throws {
        var now: UInt64 = 0
        let profiler = RunProfiler(clock: {
            now += 10
            return now
        })
        let program = try GameEventScriptBuilder.create().addScript("function f(_ x) be x + 1\non Tick { emit Result(f(10)) }", sourceName: "sample.ges").compile()
        let host = try GameEventScriptHost.createBuilder().withProfiler(profiler).build()
        try host.load(program)
        try host.load(program)
        _ = try host.start()
        XCTAssertEqual(now, 0)
        host.receive(try .init(name: "Tick"))
        var executed = 0
        while !host.isIdle {
            executed += try host.executeFrame(opcodeBudget: 1).executedOpcodes
            now += 1_000_000
        }
        XCTAssertEqual(profiler.programs.count, 2)
        XCTAssertGreaterThan(executed, 0)
        XCTAssertEqual(UInt64(executed), profiler.programs.reduce(UInt64(0)) { $0 + $1.counts.reduce(0, +) })
        for measurement in profiler.programs {
            XCTAssertGreaterThan(measurement.counts.reduce(0, +), 0)
            for index in measurement.counts.indices { XCTAssertEqual(measurement.counts[index] * 60, measurement.nanoseconds[index]) }
            for address in measurement.counts.indices {
                for phase in 0..<6 { XCTAssertEqual(measurement.counts[address] * 10, measurement.phaseNanoseconds[address * 6 + phase]) }
            }
            XCTAssertGreaterThan(measurement.phaseTotals.reduce(0, +), measurement.nanoseconds.reduce(0, +))
            let stopped = now
            measurement.finishSlice()
            XCTAssertEqual(now, stopped)
        }
        XCTAssertEqual(profiler.programs[0].counts, profiler.programs[1].counts)
        XCTAssertTrue(profiler.markdown().contains("sample.ges:"))
        XCTAssertTrue(profiler.markdown().contains("## Loop phases"))
        XCTAssertTrue(profiler.markdown().contains("| 60.000 | 10.000 | 10.000 | 10.000 | 10.000 | 10.000 | 10.000 |"))
    }

    func testFaultAndTerminalChecksRemainSeparate() throws {
        var now: UInt64 = 0
        let program = try GameEventScriptBuilder.create().addScript("on Tick { emit Result(10) }").compile()
        let measurement = RunProfiler.Measurement(
            program,
            clock: {
                now += 10
                return now
            }
        )
        measurement.phaseStarting(.stateCheck)
        measurement.finishSlice()
        now += 1_000_000
        measurement.phaseStarting(.stateCheck)
        measurement.phaseStarting(.budgetCheck)
        measurement.phaseStarting(.sliceCheck)
        measurement.phaseStarting(.fetch)
        measurement.instructionStarting(0)
        measurement.finishSlice()
        XCTAssertEqual(measurement.nanoseconds[0], 50)
        XCTAssertEqual(measurement.phaseTotals.reduce(0, +), 60)
        XCTAssertEqual(Array(measurement.phaseNanoseconds.prefix(6)), [10, 10, 10, 10, 10, 0])
    }

    func testReportWithoutDebugData() throws {
        let program = try GameEventScriptBuilder.create().withDebugInfo(.none).addScript("on Tick { emit Result(10) }").compile()
        let profiler = RunProfiler()
        let host = try GameEventScriptHost.createBuilder().withProfiler(profiler).build()
        try host.load(program)
        _ = try host.start()
        host.receive(try .init(name: "Tick"))
        _ = try host.runToCompletion()
        XCTAssertTrue(profiler.markdown().contains("[unmapped]"))
        XCTAssertTrue(profiler.markdown().contains("EmitMessage"))
        XCTAssertTrue(profiler.markdown().contains("ReturnVoid"))
    }
}

extension ToolTests {
    func testProfileOutputAndPartialFailure() throws {
        let source = try file("profile.ges", "on Main(args) { emit ConsoleOut(42) }")
        let output = try file("profile.md", "old report")
        let success = run(["run", source, "--profile", output, "--quiet"])
        XCTAssertEqual(success.code, 0, success.error)
        XCTAssertEqual(success.output.trimmingCharacters(in: .whitespacesAndNewlines), "42")
        let report = try String(contentsOfFile: output, encoding: .utf8)
        XCTAssertTrue(report.contains("# GES opcode profile"))
        XCTAssertTrue(report.contains("## Source lines"))
        XCTAssertTrue(report.contains("profile.ges:1"))
        let failure = run(["run", source, "--max-steps", "1", "--profile", output])
        XCTAssertEqual(failure.code, 1)
        XCTAssertTrue(try String(contentsOfFile: output, encoding: .utf8).contains("Instruction starts: 1."))
        XCTAssertEqual(run(["run", "--interactive", "--profile", output]).code, 2)
        XCTAssertEqual(run(["run", source, "--profile", source]).code, 2)
    }
}
