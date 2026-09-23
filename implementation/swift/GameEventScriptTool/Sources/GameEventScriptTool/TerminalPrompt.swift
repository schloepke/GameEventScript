// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import TerminalSupport

enum TextDisplay {
    static func width(_ character: Character) -> Int {
        if character.unicodeScalars.contains(where: { $0.value == 0x200d || $0.value == 0xfe0f || $0.properties.isEmojiPresentation }) { return 2 }
        return character.unicodeScalars.reduce(0) { $0 + max(0, Int(ges_scalar_width($1.value))) }
    }

    static func expandTabs(_ text: String) -> String {
        var result = ""
        var column = 0
        for character in text {
            if character == "\t" {
                let count = 4 - column % 4
                result += String(repeating: " ", count: count)
                column += count
            } else {
                result.append(character)
                if character == "\n" || character == "\r" || character == "\r\n" { column = 0 } else { column += width(character) }
            }
        }
        return result
    }
}

enum TerminalKey: Equatable {
    case text(String)
    case submit, newline, left, right, up, down, home, end, delete, backspace
    case cancel, eof, clear, wordLeft, wordRight
    case paste(String)
    case unrecognized([UInt8])
}

final class TerminalKeys {
    let read: (Int32) -> Int32

    init(read: @escaping (Int32) -> Int32 = ges_terminal_read) { self.read = read }

    static let sequences: [(String, TerminalKey)] = [
        ("\u{1b}OA", .up), ("\u{1b}OB", .down), ("\u{1b}OC", .right), ("\u{1b}OD", .left),
        ("\u{1b}[A", .up), ("\u{1b}[B", .down), ("\u{1b}[C", .right), ("\u{1b}[D", .left), ("\u{1b}[H", .home), ("\u{1b}[F", .end), ("\u{1b}OH", .home), ("\u{1b}OF", .end), ("\u{1b}[1~", .home), ("\u{1b}[4~", .end), ("\u{1b}[3~", .delete),
        ("\u{1b}[1;5D", .wordLeft), ("\u{1b}[1;5C", .wordRight), ("\u{1b}[27;2;13~", .newline), ("\u{1b}[13;2u", .newline), ("\u{1b}[27;3;13~", .newline), ("\u{1b}[13;3u", .newline), ("\u{1b}\r", .newline), ("\u{1b}\n", .newline),
        ("\u{1b}[200~", .paste("")),
    ]

    func next(idle: (() throws -> Void)? = nil) throws -> TerminalKey {
        var first: Int32
        repeat {
            first = read(idle == nil ? -1 : 20)
            if first == -2 { try idle?() }
        } while first == -2
        if first == -1 { return .eof }
        guard first >= 0 else { throw ToolError.io("Could not read terminal input.") }
        switch first {
        case 10, 13: return .submit
        case 14: return .newline
        case 3: return .cancel
        case 4: return .eof
        case 12: return .clear
        case 1: return .home
        case 5: return .end
        case 8, 127: return .backspace
        case 27:
            var bytes: [UInt8] = [27]
            let deadline = ProcessInfo.processInfo.systemUptime + 0.05
            while true {
                let possible = Self.sequences.filter { Array($0.0.utf8).starts(with: bytes) }
                if let match = possible.first(where: { $0.0.utf8.count == bytes.count }) {
                    if match.1 == .paste("") { return try paste() }
                    return match.1
                }
                if possible.isEmpty { return .unrecognized(bytes) }
                let remaining = max(0, Int32((deadline - ProcessInfo.processInfo.systemUptime) * 1000))
                let value = read(remaining)
                if value == -3 { throw ToolError.io("Could not read terminal input.") }
                if value < 0 { return .unrecognized(bytes) }
                bytes.append(UInt8(value))
            }
        default:
            if first < 32 && first != 9 { return .unrecognized([UInt8(first)]) }
            var bytes = [UInt8(first)]
            let count = first < 0x80 ? 1 : first & 0xe0 == 0xc0 ? 2 : first & 0xf0 == 0xe0 ? 3 : first & 0xf8 == 0xf0 ? 4 : 1
            for _ in 1..<count {
                let next = read(-1)
                guard next >= 0 else { throw ToolError.encoding("<stdin>") }
                bytes.append(UInt8(next))
            }
            guard let text = String(bytes: bytes, encoding: .utf8) else { throw ToolError.encoding("<stdin>") }
            return .text(text)
        }
    }

    private func paste() throws -> TerminalKey {
        let end = Array("\u{1b}[201~".utf8)
        var bytes: [UInt8] = []
        while true {
            let value = read(-1)
            guard value >= 0 else { throw ToolError.io("Terminal paste ended before its closing marker.") }
            bytes.append(UInt8(value))
            if bytes.count >= end.count && bytes.suffix(end.count).elementsEqual(end) {
                bytes.removeLast(end.count)
                break
            }
        }
        guard let text = String(bytes: bytes, encoding: .utf8) else { throw ToolError.encoding("<stdin>") }
        let normalized = text.replacingOccurrences(of: "\r\n", with: "\n").replacingOccurrences(of: "\r", with: "\n")
        return .paste(String(normalized.unicodeScalars.filter { $0.value >= 32 || $0 == "\n" || $0 == "\t" }))
    }
}

// Raw terminal state is acquired only while editing and restored before host execution.
final class TerminalPrompt {
    let io: ToolIO
    let keys = TerminalKeys()
    let highlighting = Highlighting()
    var history: [String] = []

    let color: Bool

    init(io: ToolIO, color: Bool = true) {
        self.io = io
        self.color = color
    }

    func readLine(session: RunSession? = nil) throws -> String? {
        guard ges_terminal_begin() != 0 else { throw ToolError.io("Could not enable terminal editing.") }
        defer {
            io.write("\u{1b}[?2004l", toError: true)
            ges_terminal_end()
        }
        io.write("\u{1b}[?2004h", toError: true)
        var text: [Character] = []
        var cursor = 0
        var historyIndex = history.count
        var draft = ""
        var previousRow = 0

        func position(_ input: ArraySlice<Character>, columns: Int) -> (Int, Int) {
            var row = 0
            var column = 5
            var lineColumn = 0
            for character in input {
                if character == "\n" {
                    row += 1
                    column = 5
                    lineColumn = 0
                    continue
                }
                let width = character == "\t" ? 4 - lineColumn % 4 : TextDisplay.width(character)
                if width > 0 && column + width > columns {
                    row += 1
                    column = 0
                }
                column += width
                lineColumn += width
            }
            return column >= columns ? (row + 1, 0) : (row, column)
        }

        func draw() {
            let columns = max(10, Int(ges_terminal_columns()))
            io.write("\r" + (previousRow > 0 ? "\u{1b}[\(previousRow)A" : "") + "\u{1b}[J", toError: true)
            let visible = TextDisplay.expandTabs(String(text))
            let colored = (color ? highlighting.render(visible) : visible).replacingOccurrences(of: "\n", with: "\n ... ")
            io.write("ges> " + colored, toError: true)
            let end = position(text[...], columns: columns)
            let caret = position(text[..<cursor], columns: columns)
            // Realize the terminal's pending wrap before positioning the cursor.
            if end.1 == 0 { io.write(" \r", toError: true) }
            if end.0 > caret.0 { io.write("\u{1b}[\(end.0 - caret.0)A", toError: true) }
            io.write("\r" + (caret.1 > 0 ? "\u{1b}[\(caret.1)C" : ""), toError: true)
            previousRow = caret.0
        }

        draw()
        while io.outputError == nil {
            let key = try keys.next(
                idle: session.map { session in
                    {
                        guard let delay = session.host.nextMessageDelay, delay == 0 else { return }
                        self.io.write("\r" + (previousRow > 0 ? "\u{1b}[\(previousRow)A" : "") + "\u{1b}[J", toError: true)
                        previousRow = 0
                        guard try session.pump() else { throw ToolError.io("Delayed message processing failed.") }
                        draw()
                    }
                }
            )
            switch key {
            case .text(let value), .paste(let value):
                // Re-segment after insertion so combining marks/emoji remain one editable grapheme.
                let prefix = String(text[..<cursor]) + value
                let suffix = String(text[cursor...])
                text = Array(prefix + suffix)
                cursor = prefix.count
            case .newline:
                text.insert("\n", at: cursor)
                cursor += 1
            case .left: cursor = max(0, cursor - 1)
            case .right: cursor = min(text.count, cursor + 1)
            case .home: cursor = 0
            case .end: cursor = text.count
            case .wordLeft:
                while cursor > 0 && text[cursor - 1].isWhitespace { cursor -= 1 }
                while cursor > 0 && !text[cursor - 1].isWhitespace { cursor -= 1 }
            case .wordRight:
                while cursor < text.count && !text[cursor].isWhitespace { cursor += 1 }
                while cursor < text.count && text[cursor].isWhitespace { cursor += 1 }
            case .backspace:
                if cursor > 0 {
                    text.remove(at: cursor - 1)
                    cursor -= 1
                }
            case .delete: if cursor < text.count { text.remove(at: cursor) }
            case .up, .down:
                if historyIndex == history.count { draft = String(text) }
                historyIndex = key == .up ? max(0, historyIndex - 1) : min(history.count, historyIndex + 1)
                text = Array(historyIndex == history.count ? draft : history[historyIndex])
                cursor = text.count
            case .clear:
                io.write("\u{1b}[2J\u{1b}[H", toError: true)
                previousRow = 0
            case .cancel:
                cursor = text.count
                draw()
                io.line("^C", toError: true)
                text = []
                cursor = 0
                previousRow = 0
                historyIndex = history.count
                draft = ""
            case .eof:
                if text.isEmpty {
                    io.line(toError: true)
                    return nil
                }
            case .submit:
                cursor = text.count
                draw()
                io.line(toError: true)
                let value = String(text)
                if !value.isEmpty && history.last != value { history.append(value) }
                return value
            case .unrecognized: continue
            }
            draw()
        }
        return nil
    }
}
