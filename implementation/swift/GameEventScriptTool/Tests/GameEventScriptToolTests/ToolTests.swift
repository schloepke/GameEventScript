// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

@testable import GameEventScriptTool

final class ToolTests: XCTestCase {
    var directory: URL!

    override func setUpWithError() throws {
        var root = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { root.deleteLastPathComponent() }
        directory = root.appendingPathComponent("artifacts/swift/tool-tests/\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws { try FileManager.default.removeItem(at: directory) }

    func file(_ name: String, _ text: String) throws -> String {
        let path = directory.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: path.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data(text.utf8).write(to: path)
        return path.path
    }

    struct Result {
        let code: Int
        let output: String
        let error: String
    }

    func run(_ arguments: [String], input: [String] = [], noColor: Bool = false, beforeInput: ((String) throws -> Void)? = nil) -> Result {
        var stdout = ""
        var stderr = ""
        var lines = input
        let io = ToolIO(
            output: { stdout += $0 },
            error: { stderr += $0 },
            input: {
                guard !lines.isEmpty else { return nil }
                let line = lines.removeFirst()
                try beforeInput?(line)
                return line
            },
            inputTerminal: false,
            outputTerminal: false,
            errorTerminal: false,
            noColor: noColor
        )
        return Result(code: Tool(io: io).run(arguments), output: stdout, error: stderr)
    }

    func testHelpVersionAndUsage() {
        XCTAssertEqual(run([]).code, 0)
        XCTAssertTrue(run(["--version"]).output.contains("(Swift)"))
        for command in ["compile", "check", "run", "dump"] {
            let help = run([command, "--help"])
            XCTAssertEqual(help.code, 0)
            XCTAssertTrue(help.output.contains("ges " + command))
            XCTAssertEqual(help.error, "")
            XCTAssertEqual(run([command]).code, 2)
        }
        XCTAssertEqual(run(["unknown"]).code, 2)
    }

    func testCompileBinaryDebugOptionsAndCheck() throws {
        let source = try file("game.ges", "module cli.test\non Main(args) { emit ConsoleOut(args[:count]) }\n")
        let checked = run(["check", source, "--verbose"])
        XCTAssertEqual(checked.code, 0, checked.error)
        XCTAssertTrue(checked.output.contains("Required registers:"))
        let binary = directory.appendingPathComponent("game.gesb")
        XCTAssertFalse(FileManager.default.fileExists(atPath: binary.path))
        let compile = run(["compile", source])
        XCTAssertEqual(compile.code, 0, compile.error)
        XCTAssertTrue(compile.output.contains("Handlers: 1"))
        let program = try GameEventScriptProgramReader.read(Array(Data(contentsOf: binary)))
        XCTAssertEqual(program.moduleName, "cli.test")
        XCTAssertEqual(program.sourceArchive?.first?.text, try String(contentsOfFile: source, encoding: .utf8))
        XCTAssertNotNil(program.sourceMap)
        XCTAssertNotNil(program.debugSymbols)
        let minimal = run(["compile", source, "--no-debug", "--quiet"])
        XCTAssertEqual(minimal.code, 0, minimal.error)
        XCTAssertEqual(minimal.output, "")
        let decoded = try GameEventScriptProgramReader.read(Array(Data(contentsOf: binary)))
        XCTAssertNil(decoded.sourceArchive)
        XCTAssertNil(decoded.sourceMap)
        XCTAssertNil(decoded.debugSymbols)
    }

    func testJointCompilationOrderWildcardAndDeduplication() throws {
        let a = try file("parts/a.ges", "on Main(args) { emit ConsoleOut(double(21)) }")
        let b = try file("parts/b.ges", "function double(_ x) be x + x")
        let output = directory.appendingPathComponent("nested/game.gesb").path
        XCTAssertEqual(run(["compile", a, b]).code, 2)
        let compiled = run(["compile", directory.appendingPathComponent("parts/*.ges").path, a, "-o", output])
        XCTAssertEqual(compiled.code, 0, compiled.error)
        XCTAssertTrue(compiled.output.contains("Sources (2):"))
        let result = run(["run", output, "-q"])
        XCTAssertEqual(result.output, "42\n", result.error)
        let program = try ToolFiles.read(output)
        XCTAssertEqual(program.sourceArchive?.map(\.sourceName), [a, b])
        XCTAssertEqual(run(["check", directory.appendingPathComponent("*/a.ges").path]).code, 2)
        XCTAssertEqual(run(["check", directory.appendingPathComponent("parts/**.ges").path]).code, 2)
        XCTAssertEqual(run(["check", directory.appendingPathComponent("missing*.ges").path]).code, 1)
    }

    func testAtomicOutputsAndInvalidEncodingPreserveFiles() throws {
        let source = try file("game.ges", "on Main(args) { emit ConsoleOut(1) }")
        let output = try file("output.gesb", "sentinel")
        XCTAssertEqual(run(["compile", source, "-o", source]).code, 2)
        _ = try file("game.ges", "on Main(args) { broken }")
        let failure = run(["compile", source, "-o", output])
        XCTAssertEqual(failure.code, 1)
        XCTAssertTrue(failure.error.contains(source))
        XCTAssertEqual(try String(contentsOfFile: output, encoding: .utf8), "sentinel")
        try Data([0xff, 0xfe]).write(to: URL(fileURLWithPath: source))
        let encoding = run(["check", source])
        XCTAssertEqual(encoding.code, 1)
        XCTAssertTrue(encoding.error.contains("cli.invalidEncoding"))
        XCTAssertTrue(encoding.error.contains(source))
        let bom = try file("bom.ges", "\u{feff}on Main(args) {}")
        XCTAssertEqual(run(["check", bom]).code, 0)
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: directory.path).contains { $0.hasSuffix(".tmp") })
    }

    func testDumpRoundtripOutputAndDecodeContext() throws {
        let source = try file("source.ges", "on Main(args) {\n\temit ConsoleOut(42)\n}\n")
        XCTAssertEqual(run(["compile", source, "-q"]).code, 0)
        let binary = directory.appendingPathComponent("source.gesb").path
        let program = try ToolFiles.read(binary)
        let result = run(["dump", binary])
        XCTAssertEqual(result.code, 0, result.error)
        XCTAssertEqual(result.output, GameEventScriptProgramDumper.dump(program))
        let addresses = run(["dump", binary, "--addresses"])
        XCTAssertEqual(addresses.output, GameEventScriptProgramDumper.dump(program, includeInstructionAddresses: true))
        XCTAssertEqual(run(["dump", binary, "-o", binary]).code, 2)
        let destination = directory.appendingPathComponent("dump/output.gesa").path
        XCTAssertEqual(run(["dump", binary, "-o", destination]).code, 0)
        XCTAssertEqual(try String(contentsOfFile: destination, encoding: .utf8), result.output)
        var bytes = Array(try Data(contentsOf: URL(fileURLWithPath: binary)))
        bytes[0] = 0
        try Data(bytes).write(to: URL(fileURLWithPath: binary))
        let bad = run(["dump", binary, "-o", destination])
        XCTAssertEqual(bad.code, 1)
        XCTAssertTrue(bad.error.contains("decode.invalidMagic"))
        XCTAssertTrue(bad.error.contains(binary))
        XCTAssertEqual(try String(contentsOfFile: destination, encoding: .utf8), result.output)
    }

    func testMainArgumentFormsRemainText() throws {
        let source = try file("args.ges", "on Main(args) { for arg in args { emit ConsoleOut(arg is :Text, \":\", arg) } }")
        let result = run(["run", source, "--arg", "--help", "--args", "12", "-.5", "-", "Hello", "--color", "--", "--quiet", "34"])
        XCTAssertEqual(result.code, 0, result.error)
        let plain = result.output.replacingOccurrences(of: "\u{1b}[35m", with: "").replacingOccurrences(of: "\u{1b}[0m", with: "")
        XCTAssertEqual(plain, "true:--help\ntrue:12\ntrue:-.5\ntrue:-\ntrue:Hello\ntrue:--quiet\ntrue:34\n")
        XCTAssertTrue(result.error.contains("Run completed:"))
        XCTAssertEqual(run(["run", source, "--arg", "", "-q"]).output, "true:\n")
    }

    func testInvalidRunOptionsNeverExecute() throws {
        let source = try file("init.ges", "on initialization { emit ConsoleOut(\"should not run\") }")
        let invalid: [[String]] = [
            ["--args"], ["--args", "--color"], ["--seed", "1.5"], ["--seed", "9223372036854775808"], ["--seed", "1", "--seed", "2"], ["--max-steps", "0"], ["--max-messages", "2147483648"], ["--max-messages", "+1"], ["--quiet", "--verbose"],
            ["--interactive", "--scenario", source], ["--interactive", "--arg", "x"], ["--interactive", "--"], ["--scenario", source, "--args", "x"], ["--args", "12", "--colro"], ["--scenario", ""], ["--scenario", "--color"], ["--arg"],
        ]
        for options in invalid {
            let result = run(["run", source] + options)
            XCTAssertEqual(result.code, 2, "\(options): \(result.error)")
            XCTAssertEqual(result.output, "", "\(options)")
        }
    }

    func testMainRequirementsAndInitializationOrdering() throws {
        let missing = try file("missing.ges", "on initialization { emit ConsoleOut(\"must not run\") }")
        let result = run(["run", missing])
        XCTAssertEqual(result.code, 1)
        XCTAssertTrue(result.error.contains("cli.missingMain"))
        XCTAssertEqual(result.output, "")
        for handler in ["on Main(value) {}", "on Main(args) matching #ready {}"] {
            let path = try file("wrong.ges", handler)
            XCTAssertTrue(run(["run", path]).error.contains("cli.missingMain"))
        }
        let source = try file("main.ges", "on initialization { emit ConsoleOut(\"init\") }\non Main(args) { emit ConsoleOut(\"main\") }")
        XCTAssertEqual(run(["run", source, "-q"]).output, "init\nmain\n")
        let byName = try file("name.ges", "on Main as message { emit ConsoleOut(message) }")
        XCTAssertEqual(run(["run", byName, "-q"]).code, 0)
    }

    func testMultipleBinariesAndScenarioAreLoadedBeforeInitialization() throws {
        let a = try file("first.ges", "on initialization { emit Start() }\non Main(args) { emit ConsoleOut(\"first\") }")
        let b = try file("second.ges", "on Start() { emit ConsoleOut(\"ready\") }\non Main(args) { emit ConsoleOut(\"second\") }")
        XCTAssertEqual(run(["compile", a, "-q"]).code, 0)
        XCTAssertEqual(run(["compile", b, "-q"]).code, 0)
        let binaries = [directory.appendingPathComponent("first.gesb").path, directory.appendingPathComponent("second.gesb").path]
        XCTAssertEqual(run(["run"] + binaries + ["-q"]).output, "ready\nfirst\nsecond\n")
        XCTAssertEqual(run(["run", a, binaries[1]]).code, 2)
        let scenario = try file("scenario.ges", "on initialization { emit Start() }")
        XCTAssertEqual(run(["run", b, "--scenario", scenario, "-q"]).output, "ready\n")
    }

    func testConsoleChannelsColorsExitCodesAndFailureOverride() throws {
        let source = try file("console.ges", "on Main(args) { emit ConsoleOut(\"value=\", 12, true); emit ConsoleErr(\"bad\", 3); emit ErrorCode(code: 7); emit ConsoleOut(\"after\") }")
        let plain = run(["run", source, "-q"])
        XCTAssertEqual(plain.code, 7, plain.error)
        XCTAssertEqual(plain.output, "value=12true\nafter\n")
        XCTAssertEqual(plain.error, "bad3\n")
        let colored = run(["run", source, "-q", "--color"])
        XCTAssertTrue(colored.output.contains("\u{1b}[34m12\u{1b}[0m"))
        XCTAssertTrue(colored.error.contains("\u{1b}[31mbad3\u{1b}[0m"))
        XCTAssertEqual(run(["run", source, "-q", "--color"], noColor: true).output, plain.output)
        let reset = try file("reset.ges", "on Main(args) { emit ErrorCode(255); emit ErrorCode(nothing) }")
        XCTAssertEqual(run(["run", reset, "-q"]).code, 0)
        for value in ["256", "-1", "1.5", "true", "\"2\"", "2m", "20%", "1, 2"] {
            let invalid = try file("invalid.ges", "on Main(args) { emit ErrorCode(\(value)) }")
            let result = run(["run", invalid, "-q"])
            XCTAssertEqual(result.code, 1, result.error)
            XCTAssertTrue(result.error.contains("cli.errorCodeArgument"), result.error)
            XCTAssertTrue(result.error.contains("handler=ErrorCode"), result.error)
        }
        let limit = try file("loop.ges", "on Main(args) { emit ErrorCode(7); emit Tick() }\non Tick() { emit Tick() }")
        let result = run(["run", limit, "--max-messages", "4"])
        XCTAssertEqual(result.code, 1)
        XCTAssertTrue(result.error.contains("cli.runtimeLimit"))
    }

    func testInteractiveRecoveryLoadAndInspection() throws {
        let source = try file("loaded file.ges", "module cli.loaded\non initialization { emit ConsoleOut(\"init\") }\non Start(value) matching #ready { emit ConsoleOut(value) }\non Main(args) { emit ConsoleOut(\"main\") }\n")
        let result = run(
            ["run", "--interactive", "--quiet"],
            input: [
                ":help", ":load \"\(source)\"", ":list", ":handler", ":source @1", ":dump cli.loaded", "emit Start(value: 42) with #ready", "let local be 3", "emit ConsoleOut(local)", ":load missing.ges", "emit ConsoleOut(\"still active\")", ":quit",
            ]
        )
        XCTAssertEqual(result.code, 1)
        XCTAssertEqual(result.output, "init\n42\nstill active\n")
        XCTAssertTrue(result.error.hasPrefix("\nGES event console"))
        XCTAssertTrue(result.error.contains("Loaded programs (1):"))
        XCTAssertTrue(result.error.contains("Registered handlers (5):"))
        XCTAssertTrue(result.error.contains("Start(value) [signature] matching #ready"))
        XCTAssertTrue(result.error.contains("// Source:"))
        XCTAssertTrue(result.error.contains(".segment code"))
        XCTAssertFalse(result.error.contains("ges> "))
        XCTAssertFalse(result.error.contains("Run completed:"))
    }

    func testInteractiveFailuresDoNotRegisterOrConsumeIds() throws {
        let bad = try file("bad.ges", "on Start() { broken }")
        let good = try file("good.ges", "module cli.same\non Start() { emit ConsoleOut(1) }")
        let result = run(["run", "--interactive", "-q"], input: [":load \(bad)", ":load \(good)", ":load \(good)", ":dump cli.same", ":dump @99", ":list", ":unknown", "emit Start()", ":quit"])
        XCTAssertEqual(result.code, 1)
        XCTAssertEqual(result.output, "1\n1\n")
        XCTAssertTrue(result.error.contains("Loaded programs (2):"))
        XCTAssertTrue(result.error.contains("@1  cli.same"))
        XCTAssertTrue(result.error.contains("@2  cli.same"))
        XCTAssertTrue(result.error.contains("more than once"))
        XCTAssertFalse(result.error.contains("@3  "))
    }

    func testSourcePreservesCRLFWithoutExtraBlankLines() throws {
        let text = "module cli.crlf\r\non Main(args) {}\r\n"
        let source = try file("crlf.ges", text)
        let result = run(["run", source, "--interactive", "-q"], input: [":source @1", ":quit"])
        XCTAssertEqual(result.code, 0, result.error)
        XCTAssertEqual(result.error, "\n// Source: " + quoted(source) + "\n" + text + "\n")
    }

    func testInteractiveNoDebugSourceNoticeAndTerminalColors() throws {
        let source = try file("no-debug.ges", "module cli.nodebug\non Start() {}")
        XCTAssertEqual(run(["compile", source, "--no-debug", "-q"]).code, 0)
        let binary = directory.appendingPathComponent("no-debug.gesb").path
        let result = run(["run", binary, "--interactive", "--color", "-v"], input: [":source @1", "emit ConsoleOut(12)", ":quit"])
        XCTAssertEqual(result.code, 0, result.error)
        XCTAssertTrue(result.error.contains("No embedded sources available"))
        XCTAssertTrue(result.error.contains("\u{1b}[33memit "))
        XCTAssertTrue(result.output.contains("\u{1b}[34m12"))
    }

    func testInteractiveRuntimeLimitEndsSessionAndRejectsAdditionalHandlers() throws {
        let source = try file("loop.ges", "on Tick() { emit Tick() }")
        let result = run(["run", source, "--interactive", "--max-messages", "2"], input: ["emit Tick()", "emit ConsoleOut(\"must not run\")"])
        XCTAssertEqual(result.code, 1)
        XCTAssertEqual(result.output, "")
        XCTAssertTrue(result.error.contains("cli.runtimeLimit"))
        let injection = run(["run", "--interactive", "-q"], input: ["}\non Injected() {}\non initialization {", ":handler", ":quit"])
        XCTAssertEqual(injection.code, 1)
        XCTAssertTrue(injection.error.contains("cli.interactiveInput"))
        XCTAssertTrue(injection.error.contains("Registered handlers (3):"))
    }

    func testOutputFailureReturnsFailureAndLoadQuoting() throws {
        let source = try file("output.ges", "on Main(args) { emit ConsoleOut(42); emit ErrorCode(7) }")
        let io = ToolIO(output: { _ in throw ToolError.io("closed") }, error: { _ in }, input: { nil }, inputTerminal: false, outputTerminal: false, errorTerminal: false)
        XCTAssertEqual(Tool(io: io).run(["run", source]), 1)
        XCTAssertEqual(try Tool.loadPath("'it''s.ges'"), "it's.ges")
        XCTAssertEqual(try Tool.loadPath("C:\\games\\x.ges"), "C:\\games\\x.ges")
        XCTAssertThrowsError(try Tool.loadPath("\"missing"))
        XCTAssertThrowsError(try Tool.loadPath("\"\""))
    }
}
