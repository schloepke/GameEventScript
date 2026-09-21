// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import TerminalSupport

final class ToolIO {
    var output: (String) throws -> Void
    var error: (String) throws -> Void
    var input: () throws -> String?
    let inputTerminal: Bool
    let outputTerminal: Bool
    let errorTerminal: Bool
    let noColor: Bool
    private(set) var outputError: (any Error)?

    init(
        output: @escaping (String) throws -> Void = { try FileHandle.standardOutput.write(contentsOf: Data($0.utf8)) },
        error: @escaping (String) throws -> Void = { try FileHandle.standardError.write(contentsOf: Data($0.utf8)) },
        input: (() throws -> String?)? = nil,
        inputTerminal: Bool = ges_terminal_isatty(0) != 0,
        outputTerminal: Bool = ges_terminal_isatty(1) != 0,
        errorTerminal: Bool = ges_terminal_isatty(2) != 0,
        noColor: Bool = ProcessInfo.processInfo.environment["NO_COLOR"] != nil
    ) {
        self.output = output
        self.error = error
        let reader = ToolLineReader()
        self.input = input ?? { try reader.readLine() }
        self.inputTerminal = inputTerminal
        self.outputTerminal = outputTerminal
        self.errorTerminal = errorTerminal
        self.noColor = noColor
    }

    func write(_ text: String, toError: Bool = false) {
        guard outputError == nil else { return }
        do { try (toError ? error : output)(text) } catch { outputError = error }
    }

    func line(_ text: String = "", toError: Bool = false) { write(text + "\n", toError: toError) }

    func diagnostic(_ diagnostic: GameEventScriptDiagnostic, fallback: String) {
        var prefix = diagnostic.sourceLocation?.sourceName ?? fallback
        if let line = diagnostic.sourceLocation?.line { prefix += "(\(line),\(diagnostic.sourceLocation?.column ?? 1))" }
        let context = [diagnostic.programName.map { "program=" + $0 }, diagnostic.handlerName.map { "handler=" + $0 }, diagnostic.symbol.map { "symbol=" + $0 }].compactMap { $0 }
        let suffix = context.isEmpty ? "" : " [" + context.joined(separator: ", ") + "]"
        line("\(prefix): error \(diagnostic.code): \(diagnostic.message)\(suffix)", toError: true)
    }

    func report(_ failure: any Error, fallback: String = "compile", argumentCode: String = "cli.usage") {
        switch failure {
        case let error as GameEventScriptCompileError: for value in error.diagnostics { diagnostic(value, fallback: fallback) }
        case let error as GameEventScriptDynamicLinkError: diagnostic(error.diagnostic, fallback: "link")
        case let error as GameEventScriptProgramFormatError:
            line("\(fallback): error decode.\(error.code): \(error.message)", toError: true)
            if let offset = error.byteOffset { line("  byteOffset=\(offset)", toError: true) }
            if let section = error.sectionType { line(String(format: "  sectionType=0x%04X", section), toError: true) }
            if let entry = error.entryIndex { line("  entryIndex=\(entry)", toError: true) }
        case ToolError.usage(let message): line("error \(argumentCode): \(message)", toError: true)
        case ToolError.encoding(let path): line("\(path): error cli.invalidEncoding: Expected valid UTF-8.", toError: true)
        case ToolError.io(let message): line("error cli.io: \(message)", toError: true)
        default: line("error cli.io: \(failure.localizedDescription)", toError: true)
        }
    }
}

enum ToolError: Error {
    case usage(String)
    case encoding(String)
    case io(String)
}

// Reads bounded chunks and accepts LF, CRLF and CR. Invalid UTF-8 is never repaired silently.
final class ToolLineReader {
    var buffer: [UInt8] = []
    var skipLF = false
    var eof = false

    func readLine() throws -> String? {
        while true {
            if skipLF && !buffer.isEmpty {
                if buffer[0] == 10 { buffer.removeFirst() }
                skipLF = false
            }
            if let end = buffer.firstIndex(where: { $0 == 10 || $0 == 13 }) {
                let bytes = Array(buffer[..<end])
                skipLF = buffer[end] == 13
                buffer.removeFirst(end + 1)
                guard let text = String(bytes: bytes, encoding: .utf8) else { throw ToolError.encoding("<stdin>") }
                return text
            }
            if eof {
                if buffer.isEmpty { return nil }
                guard let text = String(bytes: buffer, encoding: .utf8) else { throw ToolError.encoding("<stdin>") }
                buffer.removeAll()
                return text
            }
            let bytes = try FileHandle.standardInput.read(upToCount: 4096) ?? Data()
            eof = bytes.isEmpty
            buffer.append(contentsOf: bytes)
        }
    }
}

func quoted(_ text: String) -> String {
    // Foundation's JSON writer provides portable escaping for paths and verbose Text arguments.
    let data = try? JSONSerialization.data(withJSONObject: [text], options: [.fragmentsAllowed, .withoutEscapingSlashes])
    guard let data, let array = String(data: data, encoding: .utf8) else { return "\"\"" }
    return String(array.dropFirst().dropLast())
}
