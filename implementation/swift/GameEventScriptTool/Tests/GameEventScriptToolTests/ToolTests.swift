// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

@testable import GameEventScriptTool

final class ToolTests: XCTestCase {
    func testInteractiveUnknownTypeHasReadableDiagnostic() {
        let result = run(["run", "--interactive", "-q"], input: ["let x be [1, 2, 3]; let y be :Data(input: x)", ":quit"])
        XCTAssertEqual(result.code, 1)
        XCTAssertTrue(result.error.contains("validate.invalidTypeConstructor: Unknown type ':Data' in this program."), result.error)
        XCTAssertFalse(result.error.contains("program=,"), result.error)
    }

    func testCompilerErrorsExplainTheirCause() throws {
        let cases = [
            ("on Main(args) { let x be 1; let x be 2 }", "validate.duplicateVariable", "already declared in the current scope"),
            ("on Main(args) { emit ConsoleOut(missing) }", "validate.unresolvedVariable", "Unknown variable"),
            ("on Main(args) { emit ConsoleOut($missing) }", "compile.unresolvedSymbol", "Unknown symbol"),
            ("on Main(args) { emit after 10 Tick() }", "validate.invalidTypeConstructor", "requires a time quantity"),
            ("record :Data as { input: :List }\non Main(args) { let y be :Data(wrong: []) }", "validate.invalidTypeConstructor", "argument count and labels"),
        ]
        for (index, entry) in cases.enumerated() {
            let path = try file("diagnostic\(index).ges", entry.0)
            let result = run(["check", path])
            XCTAssertEqual(result.code, 1, result.error)
            XCTAssertTrue(result.error.contains(entry.1), result.error)
            XCTAssertTrue(result.error.contains(entry.2), result.error)
            XCTAssertFalse(result.error.contains("\(entry.1): \(entry.1)"), result.error)
        }
    }

    func testNumericProgramSelectorsAndLegacyAliases() throws {
        let source = try file("numeric.ges", "module inspect.selection\non Start { emit ConsoleOut('active') }")
        for selector in ["1", "@1"] {
            let result = run(["run", source, "--interactive", "-q"], input: [":list", ":source \(selector)", ":dump \(selector)", ":unload \(selector)", ":list", ":quit"])
            XCTAssertEqual(result.code, 0, result.error)
            XCTAssertTrue(result.error.contains("  1  inspect.selection"))
            XCTAssertTrue(result.error.contains("on Start"))
            XCTAssertTrue(result.error.contains(".segment code"))
            XCTAssertTrue(result.error.contains("Loaded programs (0):"))
        }
        for selector in ["0", "999999999999999999999999999999"] {
            let result = run(["run", source, "--interactive", "-q"], input: [":unload \(selector)", "emit Start", ":quit"])
            XCTAssertEqual(result.code, 1)
            XCTAssertEqual(result.output, "active\n")
        }
    }

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

    func testRunReportsEveryMissingExtensionCallSite() throws {
        let source = try file("game.ges", "on initialization {\n    publish First(value: :probe.missing(1))\n    publish Second(value: :probe.missing(2))\n}\n")
        let result = run(["run", source])
        XCTAssertEqual(result.code, 1)
        XCTAssertTrue(result.error.contains("game.ges(2,26): error link.missingExtension: Unknown extension :probe.missing(_)"), result.error)
        XCTAssertTrue(result.error.contains("game.ges(3,27): error link.missingExtension: Unknown extension :probe.missing(_)"), result.error)
        XCTAssertEqual(result.output, "")
    }

    func testSchemaLayoutCoversSeveralRoots() throws {
        let nested = try file("nested.gess", "parameter width: :Quantity(m)\nschema :Inner as {\n  x: :Quantity(m) between 0m and width step 1m\n}\nschema :Outer as {\n  inner: :Inner\n}\nschema :Other as {\n  inner: :Inner\n}\n")
        let layout = run(["schema", "layout", nested, "--root", "Outer", "--root", "Other", "--param", "width=8m"])
        XCTAssertEqual(layout.code, 0, layout.error)
        XCTAssertTrue(layout.output.hasPrefix("{\n  \"layout\": 1,\n  \"roots\": [\n    \"Outer\",\n    \"Other\"\n  ],"))
        XCTAssertEqual(layout.output.components(separatedBy: "\"name\": \"Inner\",\n      \"kind\"").count, 2)
        XCTAssertEqual(
            layout.output.replacingOccurrences(of: "\"Outer\",\n    \"Other\"", with: "\"Inner\",\n    \"Outer\",\n    \"Other\""),
            run(["schema", "layout", nested, "--all-roots", "--param", "width=8m"]).output
        )
        let output = directory.appendingPathComponent("layout.json").path
        XCTAssertEqual(run(["schema", "layout", nested, "--root", "Outer", "--param", "width=8m", "-o", output]).code, 0)
        XCTAssertTrue(FileManager.default.fileExists(atPath: output))
        XCTAssertEqual(run(["schema", "layout", nested, "--param", "width=8m"]).code, 2)
        XCTAssertEqual(run(["schema", "layout", nested, "--root", "Outer", "--all-roots", "--param", "width=8m"]).code, 2)
        XCTAssertEqual(run(["schema", "layout", nested, "--root", "Outer", "--root", "Outer", "--param", "width=8m"]).code, 2)
        XCTAssertEqual(run(["schema", "hash", nested, "--all-roots"]).code, 2)
        let unknown = run(["schema", "layout", nested, "--root", "Outer", "--param", "width=8m", "--param", "height=1m"])
        XCTAssertEqual(unknown.code, 1)
        XCTAssertTrue(unknown.error.contains("error schema.unknownName: No root reaches the parameter 'height'."))
        let missing = run(["schema", "layout", nested, "--root", "Outer"])
        XCTAssertEqual(missing.code, 1)
        XCTAssertTrue(missing.error.contains("error schema.unknownName"))
    }

    func testSchemaGenerateWritesAndChecksCode() throws {
        let drone = try file("drone.gess", "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  on: :Boolean\n}\n")
        let csharp = run(["schema", "generate", drone, "--root", "Drone", "--language", "csharp", "--namespace", "Game.Net"])
        XCTAssertEqual(csharp.code, 0, csharp.error)
        XCTAssertTrue(csharp.output.contains("public partial struct Drone"))
        let swift = run(["schema", "generate", drone, "--all-roots", "--language", "swift", "--access", "internal"])
        XCTAssertTrue(swift.output.contains("struct Drone: Sendable {"))
        XCTAssertFalse(swift.output.contains("public "))
        let output = directory.appendingPathComponent("Drone.g.cs").path
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone", "--language", "csharp", "-o", output, "--check"]).code, 1)
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone", "--language", "csharp", "-o", output]).code, 0)
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone", "--language", "csharp", "-o", output, "--check"]).code, 0)
        try (try String(contentsOfFile: output, encoding: .utf8) + "// edited\n").write(toFile: output, atomically: true, encoding: .utf8)
        let outdated = run(["schema", "generate", drone, "--root", "Drone", "--language", "csharp", "-o", output, "--check"])
        XCTAssertEqual(outdated.code, 1)
        XCTAssertTrue(outdated.error.contains("error cli.outdated: \(output) differs from the generated code."))
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone"]).code, 2)
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone", "--language", "java"]).code, 2)
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone", "--language", "swift", "--check"]).code, 2)
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone", "--language", "swift", "--access", "private"]).code, 2)
        XCTAssertTrue(run(["schema", "generate", drone, "--root", "Drone", "--language", "swift", "--values"]).output.contains("func toValue() throws -> GesValue"))
        XCTAssertEqual(run(["schema", "layout", drone, "--root", "Drone", "--values"]).code, 2)
        XCTAssertEqual(run(["schema", "generate", drone, "--root", "Drone", "--language", "swift", "--checked-exclusivity"]).code, 0)
        XCTAssertEqual(run(["schema", "layout", drone, "--root", "Drone", "--checked-exclusivity"]).code, 2)
        XCTAssertEqual(run(["schema", "layout", drone, "--root", "Drone", "--language", "swift"]).code, 2)
        let extra = try file("extra.gess", "schema :Extra as {\n  on: :Boolean\n}\n")
        XCTAssertEqual(run(["schema", "generate", drone, extra, "--all-roots", "--language", "csharp", "-o", extra]).code, 2)
        XCTAssertEqual(try String(contentsOfFile: extra, encoding: .utf8), "schema :Extra as {\n  on: :Boolean\n}\n")
        let invalid = run(["schema", "generate", drone, "--root", "Drone", "--language", "swift", "--namespace", "1x"])
        XCTAssertEqual(invalid.code, 1)
        XCTAssertTrue(invalid.error.contains("error generate.invalidNamespace"))
    }

    func testSchemaCommandsCheckHashFormatAndDump() throws {
        let source = try file("drone.gess", "// note\nschema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between 0m and 2km step 1cm\n}\n")
        XCTAssertEqual(run(["schema", "check", source, "--quiet"]).code, 0)
        XCTAssertTrue(run(["schema", "canonical", source, "--root", "Drone"]).output.contains("field x Quantity(meter) between 0e0 2e3 step 1e-2"))
        XCTAssertEqual(run(["schema", "hash", source, "--root", "Drone"]).output.count, 17)
        let dice = try file("dice.gess", "schema :S as { d: :Dice max 2 between 1 and 6, e: :Dice, t: :Tag max 3, u: :Text max 1 | :Nothing }\n")
        XCTAssertEqual(run(["schema", "format", dice]).output, "schema :S as {\n  d: :Dice max 2 between 1 and 6\n  e: :Dice\n  t: :Tag max 3\n  u: :Text max 1 | :Nothing\n}\n")
        let format = run(["schema", "format", source])
        XCTAssertTrue(format.output.contains("x: :Quantity(m) between 0m and 2km step 1cm"))
        XCTAssertFalse(format.output.contains("// note"))
        let data = directory.appendingPathComponent("data.bin")
        try Data([7, 0xE8, 3, 0]).write(to: data)
        XCTAssertTrue(run(["schema", "dump", source, "--root", "Drone", "--data", data.path]).output.contains("id bit=0 width=8 value=7"))
        let delta = directory.appendingPathComponent("delta.bin")
        try Data([7, 0]).write(to: delta)
        XCTAssertTrue(run(["schema", "dump", source, "--root", "Drone", "--data", delta.path, "--baseline", data.path]).output.hasPrefix("delta bytes 2\nid bit=0 width=8 value=7"))
        XCTAssertEqual(run(["schema", "hash", source, "--root", "Drone", "--baseline", data.path]).code, 2)
        XCTAssertEqual(run(["schema", "format", source, "-o", source]).code, 2)
        XCTAssertEqual(run(["schema", "format", source, "-o", ""]).code, 2)
        XCTAssertEqual(run(["schema", "format", "", "-o", source + ".out"]).code, 2)
        XCTAssertEqual(run(["schema", "check", ""]).code, 2)
        XCTAssertTrue(try String(contentsOfFile: source, encoding: .utf8).contains("// note"))
        XCTAssertEqual(run(["schema", "check", source, "--root", "Drone"]).code, 2)
        XCTAssertEqual(run(["schema", "canonical", source, "--root", "Drone", "--param", "a=1"]).code, 2)
        XCTAssertEqual(run(["schema", "hash", source, "--root", "Drone", "--data", data.path]).code, 2)
        XCTAssertEqual(run(["schema", "format", source, "--root", "Drone"]).code, 2)
        let unknown = run(["schema", "hash", source, "--root", "Drone", "--param", "wrong=1"])
        XCTAssertEqual(unknown.code, 1)
        XCTAssertTrue(unknown.error.contains("error schema.unknownName: The root 'Drone' has no parameter 'wrong'."))
        let nested = try file("nested.gess", "parameter width: :Quantity(m)\nschema :Inner as {\n  x: :Quantity(m) between 0m and width step 1m\n}\nschema :Outer as {\n  inner: :Inner\n}\n")
        XCTAssertEqual(run(["schema", "hash", nested, "--root", "Outer", "--param", "width=8m"]).code, 0)
        let twice = run(["schema", "hash", nested, "--root", "Outer", "--param", "width=8m", "--param", "width=9m"])
        XCTAssertEqual(twice.code, 2)
        XCTAssertTrue(twice.error.contains("The parameter 'width' is given twice."))
        XCTAssertEqual(run(["schema", "dump", nested, "--root", "Outer", "--param", "width=8m", "--param", "widht=9m", "--data", data.path]).code, 1)
        let broken = try file("broken.gess", "schema :A as {\n  a: :Quantity(kN)\n}\n")
        let failed = run(["schema", "check", broken])
        XCTAssertEqual(failed.code, 1)
        XCTAssertTrue(failed.error.contains("(2,16): error schema.syntax"), failed.error)
    }

    func testHelpVersionAndUsage() {
        XCTAssertEqual(run([]).code, 0)
        XCTAssertTrue(run(["--version"]).output.contains("(Swift)"))
        for command in ["compile", "check", "run", "dump", "document"] {
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
        let program = try GameEventScriptProgram.read(Array(Data(contentsOf: binary)))
        XCTAssertEqual(program.moduleName, "cli.test")
        XCTAssertEqual(program.sourceArchive?.first?.text, try String(contentsOfFile: source, encoding: .utf8))
        XCTAssertNotNil(program.sourceMap)
        XCTAssertNotNil(program.debugSymbols)
        let minimal = run(["compile", source, "--no-debug", "--quiet"])
        XCTAssertEqual(minimal.code, 0, minimal.error)
        XCTAssertEqual(minimal.output, "")
        let decoded = try GameEventScriptProgram.read(Array(Data(contentsOf: binary)))
        XCTAssertNil(decoded.sourceArchive)
        XCTAssertNil(decoded.sourceMap)
        XCTAssertNil(decoded.debugSymbols)
    }

    func testVerboseTraceReportsScheduledMessages() throws {
        let source = try file("game.ges", "on Main(args) { emit after 0.001s Later(); publish after 0.002s Out() }\non Later {}\n")
        let result = run(["run", source, "--verbose"])
        XCTAssertEqual(result.code, 0, result.error)
        let time = #"(\d{4}-\d{2}-\d{2} )?\d{2}:\d{2}:\d{2}\.\d{3}"#
        let trace = #"dispatch #1 Main\(args\) <- Main\(args: \[\]\)\nschedule #2 emit Later at "# + time + #"\nemit #2 Later\nschedule #3 publish Out at "# + time
        XCTAssertNotNil(result.error.range(of: trace, options: .regularExpression), result.error)
    }

    func testVerboseTraceReportsDiscardedScheduledMessages() throws {
        let source = try file("game.ges", "on initialization {\n  emit after 1s Tick()\n  for item in [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] { let doubled be item * 2 }\n}\non Tick {}\non Main(args) {}\n")
        let result = run(["run", source, "--verbose", "--max-steps", "10"])
        XCTAssertEqual(result.code, 1, result.error)
        XCTAssertNotNil(result.error.range(of: #"emit #2 Tick\nerror cli\.runtimeLimit: MaxExecutionSteps=10: [^\n]*\ndiscard #2\n"#, options: .regularExpression), result.error)
    }

    func testRunClockShowsDeadlinesAsLocalTimes() {
        let clock = RunClock()
        XCTAssertNotNil(clock.formatDeadline(172_800_000_000).range(of: #"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}$"#, options: .regularExpression))
        XCTAssertTrue(clock.formatDeadline(.max).hasPrefix("in "))
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
        XCTAssertEqual(result.output, program.dump())
        let addresses = run(["dump", binary, "--addresses"])
        XCTAssertEqual(addresses.output, program.dump(includeInstructionAddresses: true))
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
        XCTAssertTrue(result.error.contains("1  cli.same"))
        XCTAssertTrue(result.error.contains("2  cli.same"))
        XCTAssertTrue(result.error.contains("more than once"))
        XCTAssertFalse(result.error.contains("3  "))
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
