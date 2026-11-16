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
    var colorErrors = false
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

    func line(_ text: String = "", toError: Bool = false) {
        let isDiagnostic = text.hasPrefix("error ") || text.contains(": error ")
        write((toError && colorErrors && !noColor && isDiagnostic ? RunHighlighting.paint(text, 31) : text) + "\n", toError: toError)
    }

    func diagnostic(_ diagnostic: GesDiagnostic, fallback: String) {
        var prefix = diagnostic.sourceLocation?.sourceName ?? fallback
        if let line = diagnostic.sourceLocation?.line { prefix += "(\(line),\(diagnostic.sourceLocation?.column ?? 1))" }
        let context = [diagnostic.programName.flatMap { $0.isEmpty ? nil : "program=" + $0 }, diagnostic.handlerName.map { "handler=" + $0 }, diagnostic.symbol.map { "symbol=" + $0 }].compactMap { $0 }
        let suffix = context.isEmpty ? "" : " [" + context.joined(separator: ", ") + "]"
        line("\(prefix): error \(diagnostic.code): \(diagnostic.message)\(suffix)", toError: true)
    }

    func report(_ failure: any Error, fallback: String = "compile", argumentCode: String = "cli.usage") {
        switch failure {
        case let error as GesCompileError: for value in error.diagnostics { diagnostic(value, fallback: fallback) }
        case let error as GesDynamicLinkError: for value in error.diagnostics { diagnostic(value, fallback: "link") }
        case let error as GesProgramFormatError:
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
            // read(2) returns what is available, so a driver that keeps the pipe open gets each line answered; a
            // FileHandle read of a fixed count would wait for 4096 bytes or the end of the input.
            var chunk = [UInt8](repeating: 0, count: 4096)
            var count: Int
            repeat {
                count = chunk.withUnsafeMutableBytes { read(0, $0.baseAddress, $0.count) }
            } while count < 0 && errno == EINTR
            if count < 0 { throw ToolError.io("<stdin>: " + String(cString: strerror(errno))) }
            eof = count == 0
            buffer.append(contentsOf: chunk[..<count])
        }
    }
}

func quoted(_ text: String) -> String {
    // A display string in JSON quotes, as the C# CLI writes it: only quotes, backslashes and control characters are
    // escaped, so Unicode text and paths stay readable. Unicode scalars, since C# escapes UTF-16 units below U+0020 alike.
    var result = "\""
    for scalar in text.unicodeScalars {
        switch scalar {
        case "\"": result += "\\\""
        case "\\": result += "\\\\"
        case "\n": result += "\\n"
        case "\r": result += "\\r"
        case "\t": result += "\\t"
        case "\u{8}": result += "\\b"
        case "\u{c}": result += "\\f"
        case _ where scalar.value < 0x20:
            let hex = String(scalar.value, radix: 16)
            result += "\\u" + String(repeating: "0", count: 4 - hex.count) + hex
        default: result.unicodeScalars.append(scalar)
        }
    }
    return result + "\""
}

/// Compares Unicode scalar sequences exactly, without Swift String canonical equivalence; matches the Runtime's
/// identifier and source comparison rules.
func scalarEqual(_ left: String, _ right: String) -> Bool { left.utf8.elementsEqual(right.utf8) }
