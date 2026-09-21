// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import XCTest

@testable import GameEventScriptTool

extension ToolTests {
    func testUnloadKeepsNativeHandlersAndDoesNotReuseIDs() throws {
        let one = try file("one.ges", "module one\non Start { emit ConsoleOut('one') }")
        let two = try file("two.ges", "module two\non Start { emit ConsoleOut('two') }")
        let result = run(
            ["run", "--interactive", "-q"],
            input: [":load \(one)", ":load \(two)", ":unload @1", "emit Start", ":load \(one)", ":list", ":unload two", ":unloadAll", ":reload", ":handler", ":list", ":load \(two)", ":list", "emit Start", "emit ConsoleOut('native')"]
        )
        XCTAssertEqual(result.code, 0, result.error)
        XCTAssertEqual(result.output, "two\ntwo\nnative\n")
        XCTAssertTrue(result.error.contains("@3  one"))
        XCTAssertTrue(result.error.contains("@4  two"))
        XCTAssertTrue(result.error.contains("Loaded programs (0):"))
        XCTAssertTrue(result.error.contains("Registered handlers (3):"))
    }

    func testInvalidLifecycleCommandsPreservePrograms() throws {
        let one = try file("one.ges", "module one\non Start { emit ConsoleOut('one') }")
        let result = run(["run", "--interactive", "-q"], input: [":load \(one)", ":load \(one)", ":unload one", ":unload @99", ":unload", ":unloadAll extra", ":reload extra", "emit Start"])
        XCTAssertEqual(result.code, 1)
        XCTAssertEqual(result.output, "one\none\n")
        XCTAssertTrue(result.error.contains("@1, @2"))
        XCTAssertTrue(result.error.contains("cli.consoleCommand"))
    }

    func testReloadRereadsJointSourcesAndKeepsID() throws {
        let first = try file("first.ges", "module duo\nfunction value() be 7")
        let second = try file("second.ges", "module duo\non Start { emit ConsoleOut(value()) }")
        let result = run(
            ["run", first, second, "--interactive", "-q"],
            input: ["emit Start", ":reload", ":list", "emit Start"],
            beforeInput: { line in if line == ":reload" { XCTAssertNoThrow(try self.file("first.ges", "module duo\nfunction value() be 12")) } }
        )
        XCTAssertEqual(result.code, 0, result.error)
        XCTAssertEqual(result.output, "7\n12\n")
        XCTAssertTrue(result.error.contains("Loaded programs (1):"))
        XCTAssertTrue(result.error.contains("@1  duo"))
    }

    func testReloadRereadsBinariesAndInitializesAsGroup() throws {
        let first = try file("first.ges", "module one\non initialization { emit ConsoleOut('init one'); emit Ready }")
        let second = try file("second.ges", "module two\non initialization { emit ConsoleOut('init two') }\non Ready { emit ConsoleOut('ready') }\non Main(args) { emit ConsoleOut('not main') }")
        XCTAssertEqual(run(["compile", first, "-q"]).code, 0)
        XCTAssertEqual(run(["compile", second, "-q"]).code, 0)
        let binaries = [directory.appendingPathComponent("first.gesb").path, directory.appendingPathComponent("second.gesb").path]
        let result = run(
            ["run"] + binaries + ["--interactive", "-q"],
            input: [":reload", ":list"],
            beforeInput: { line in
                if line == ":reload" {
                    XCTAssertNoThrow(try self.file("first.ges", "module one\non initialization { emit ConsoleOut('changed'); emit Ready }"))
                    XCTAssertEqual(self.run(["compile", first, "-q"]).code, 0)
                }
            }
        )
        XCTAssertEqual(result.code, 0, result.error)
        XCTAssertEqual(result.output, "init one\ninit two\nready\nchanged\ninit two\nready\n")
        XCTAssertTrue(result.error.contains("@1  one"))
        XCTAssertTrue(result.error.contains("@2  two"))
    }

    func testFailedReloadPreservesWholeSession() throws {
        for failure in ["read", "compile", "decode", "link"] {
            let one = try file("one.ges", "module one\non Start { emit ConsoleOut('old') }")
            let source = try file("two.ges", "module two\non Other {}")
            XCTAssertEqual(run(["compile", source, "-q"]).code, 0)
            let two = failure == "decode" ? directory.appendingPathComponent("two.gesb").path : source
            let result = run(
                ["run", "--interactive", "-q"],
                input: [":load \(one)", ":load \(two)", ":reload", ":list", "emit Start", "emit ConsoleOut('native')"],
                beforeInput: { line in
                    guard line == ":reload" else { return }
                    XCTAssertNoThrow(try self.file("one.ges", "module one\non initialization { emit ConsoleOut('must not run') }"))
                    switch failure {
                    case "read": XCTAssertNoThrow(try FileManager.default.removeItem(atPath: two))
                    case "compile": XCTAssertNoThrow(try self.file("two.ges", "on Broken("))
                    case "decode": XCTAssertNoThrow(try Data([0xff]).write(to: URL(fileURLWithPath: two)))
                    default: XCTAssertNoThrow(try self.file("two.ges", "on Start { emit ConsoleOut(:missing.extension()) }"))
                    }
                }
            )
            XCTAssertEqual(result.code, 1, failure)
            if failure == "decode" {
                XCTAssertTrue(result.error.contains("decode."), result.error)
                XCTAssertTrue(result.error.contains(two), result.error)
            }
            if failure == "link" { XCTAssertTrue(result.error.contains("link."), result.error) }
            XCTAssertEqual(result.output, "old\nnative\n", "\(failure): \(result.error)")
            XCTAssertTrue(result.error.contains("Loaded programs (2):"))
        }
    }

    func testReloadRestartsSeedAndResetsExitCode() throws {
        let path = try file("random.ges", "on Draw { emit ConsoleOut(random from 1 to 255) }")
        let result = run(["run", path, "--interactive", "--seed", "42", "-q"], input: ["emit Draw", "emit Draw", "emit ErrorCode(7)", ":reload", "emit Draw"])
        XCTAssertEqual(result.code, 0, result.error)
        let draws = result.output.split(separator: "\n")
        XCTAssertEqual(draws.count, 3)
        XCTAssertEqual(draws.first, draws.last)
        let unloaded = run(["run", path, "--interactive", "-q"], input: ["emit ErrorCode(7)", ":unloadAll"])
        XCTAssertEqual(unloaded.code, 7, unloaded.error)
    }

    func testReloadRuntimeFailureEndsSession() throws {
        let path = try file("init.ges", "on initialization {}")
        let result = run(
            ["run", path, "--interactive", "-q"],
            input: [":reload", "emit ConsoleOut('must not run')"],
            beforeInput: { line in if line == ":reload" { XCTAssertNoThrow(try self.file("init.ges", "on initialization { emit ErrorCode(256) }")) } }
        )
        XCTAssertEqual(result.code, 1)
        XCTAssertEqual(result.output, "")
        XCTAssertTrue(result.error.contains("cli.errorCodeArgument"))
    }

    func testLifecycleHelpAndQuietStatus() {
        let result = run(["run", "--interactive", "-q"], input: [":help unload", ":help unloadAll", ":help reload", ":unloadAll", ":reload"])
        XCTAssertEqual(result.code, 0, result.error)
        XCTAssertTrue(result.error.contains(":unload <module|@ID>"))
        XCTAssertFalse(result.error.contains("Reloaded all"))
        XCTAssertFalse(result.error.contains("Unloaded 0"))
        let reported = run(["run", "--interactive"], input: [":unloadAll", ":reload"])
        XCTAssertEqual(reported.code, 0, reported.error)
        XCTAssertTrue(reported.error.contains("Reloaded all"))
        XCTAssertTrue(reported.error.contains("Unloaded 0"))
    }
}
