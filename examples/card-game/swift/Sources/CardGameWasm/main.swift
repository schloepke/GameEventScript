// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import CardGameEnvironment
import GameEventScriptCompiler
import GameEventScriptSyntaxHighlighter

// This adapter is invoked serially by one browser worker. No state is shared across workers.
// Reactor exports run without the executable main; use lazy static initialization.
private enum Highlighting {
    static let highlighter = try! GameEventScriptSyntaxHighlighter()
}

nonisolated(unsafe) private var session: CardGame?
nonisolated(unsafe) private var output: UnsafeMutablePointer<UInt8>?
nonisolated(unsafe) private var input: UnsafeMutablePointer<UInt8>?
nonisolated(unsafe) private var inputCapacity = 0

@_cdecl("cardgame_alloc")
func allocateInput(_ count: Int32) -> UnsafeMutablePointer<UInt8>? {
    guard count > 0 && count <= 131_072 else { return nil }
    input?.deallocate()
    input = .allocate(capacity: Int(count))
    inputCapacity = Int(count)
    return input
}

func respond(_ body: String) -> Int32 {
    let bytes = Array(body.utf8)
    output?.deallocate()
    output = .allocate(capacity: bytes.count + 1)
    for (index, byte) in bytes.enumerated() { output![index] = byte }
    output![bytes.count] = 0
    return Int32(bytes.count)
}

@_cdecl("cardgame_output")
func outputPointer() -> UnsafePointer<UInt8>? { output.map { UnsafePointer($0) } }

@_cdecl("cardgame_start")
func start(_ count: Int32, _ seed: Int32, _ playerCount: Int32) -> Int32 {
    session = nil
    guard let input, count > 0, Int(count) <= inputCapacity else { return respond("{\"error\":\"Invalid source buffer\"}") }
    defer {
        input.deallocate()
        selfClearInput()
    }
    do {
        let source = String(decoding: UnsafeBufferPointer(start: input, count: Int(count)), as: UTF8.self)
        guard (2...4).contains(playerCount) else { throw CardGameError("Choose two to four players") }
        let names = (1...Int(playerCount)).map { "Player \($0)" }
        let game = try CardGame(rules: source, players: names, seed: Int64(seed))
        session = game
        return respond("{\"state\":\(game.viewJSON(for: game.currentPlayer))}")
    } catch { return respond("{\"error\":\(jsonString(String(describing: error)))}") }
}

func selfClearInput() {
    input = nil
    inputCapacity = 0
}

@_cdecl("cardgame_action")
func submit(_ player: Int32, _ actionIndex: Int32, _ revision: Int32) -> Int32 {
    guard let game = session else { return respond("{\"error\":\"Start a game first\"}") }
    guard Int(revision) == game.revision, game.actions.indices.contains(Int(actionIndex)) else {
        return respond("{\"accepted\":false,\"reason\":\"Stale or unknown action\",\"state\":\(game.viewJSON(for: game.currentPlayer))}")
    }
    do {
        let result = try game.submit(player: Int(player), action: game.actions[Int(actionIndex)], revision: Int(revision))
        return respond("{\"accepted\":\(result.accepted),\"reason\":\(jsonString(result.reason)),\"state\":\(game.viewJSON(for: game.currentPlayer))}")
    } catch { return respond("{\"error\":\(jsonString(String(describing: error)))}") }
}

// The Wasm reactor initialization calls Swift's entry point once; exported functions own the session.

@_cdecl("cardgame_highlight")
func highlight(_ count: Int32) -> Int32 {
    guard let input, count > 0, Int(count) <= inputCapacity else { return respond("{\"error\":\"Invalid source buffer\"}") }
    defer {
        input.deallocate()
        selfClearInput()
    }
    let source = String(decoding: UnsafeBufferPointer(start: input, count: Int(count)), as: UTF8.self)
    let result = Highlighting.highlighter.highlight(source)
    let spans = result.spans.map { "[\($0.start),\($0.length),\(jsonString($0.kind.rawValue))]" }.joined(separator: ",")
    return respond("{\"complete\":\(result.isComplete),\"spans\":[\(spans)]}")
}

@_cdecl("cardgame_check")
func checkSource(_ count: Int32) -> Int32 {
    guard let input, count > 0, Int(count) <= inputCapacity else { return respond("{\"error\":\"Invalid source buffer\"}") }
    defer {
        input.deallocate()
        selfClearInput()
    }
    let source = String(decoding: UnsafeBufferPointer(start: input, count: Int(count)), as: UTF8.self)
    do {
        _ = try GameEventScriptBuilder.create().addScript(source, sourceName: "rules.ges").compile()
        return respond("{\"diagnostics\":[]}")
    } catch let error as GameEventScriptCompileError {
        let diagnostics = error.diagnostics.map { diagnostic in
            let location = diagnostic.sourceLocation
            return
                "{\"code\":\(jsonString(diagnostic.code)),\"message\":\(jsonString(diagnostic.message)),\"line\":\(location?.line.map(String.init) ?? "null"),\"column\":\(location?.column.map(String.init) ?? "null"),\"endLine\":\(location?.endLine.map(String.init) ?? "null"),\"endColumn\":\(location?.endColumn.map(String.init) ?? "null") }"
        }.joined(separator: ",")
        return respond("{\"diagnostics\":[\(diagnostics)]}")
    } catch { return respond("{\"error\":\(jsonString(String(describing: error)))}") }
}
