// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

final class RunSession {
    private(set) var host: GameEventScriptHost
    private(set) var observer: RunObserver
    private(set) var inventory: RunInventory
    let io: ToolIO
    let options: RunOptions
    var messages = 0, opcodes = 0, emits = 0, publishes = 0

    init(options: RunOptions, io: ToolIO, nextID: Int = 1) throws {
        self.options = options
        self.io = io
        observer = RunObserver(io: io, options: options)
        host = try GameEventScriptHost(seed: options.seed, limits: options.limits, observer: observer)
        inventory = RunInventory(host: host, io: io, nextID: nextID)
        try observer.subscribe(inventory)
    }

    func reload(activePath: inout String) throws -> Bool {
        let replacement = try RunSession(options: options, io: io, nextID: inventory.nextID)
        // Link the complete group before initialization; preparation failures leave this session intact.
        for entry in inventory.active {
            activePath = entry.paths[0]
            let program = try entry.paths.count == 1 ? ToolFiles.read(entry.paths[0]) : ToolFiles.compile(entry.paths)
            try replacement.inventory.load(program, paths: entry.paths, id: entry.id)
        }
        guard try replacement.pump() else { return false }
        inventory.unloadAll()
        host = replacement.host
        observer = replacement.observer
        inventory = replacement.inventory
        messages += replacement.messages
        opcodes += replacement.opcodes
        emits += replacement.emits
        publishes += replacement.publishes
        return true
    }

    func pump() throws -> Bool {
        if !host.isReady {
            let start = try host.start()
            messages += start.processedMessages
            opcodes += start.executedOpcodes
            emits += start.emittedMessages
            publishes += start.publishedMessages
            guard start.state == .ready else { return false }
        }
        let result = try host.runToCompletion()
        messages += result.processedMessages
        opcodes += result.executedOpcodes
        emits += result.emittedMessages
        publishes += result.publishedMessages
        return !observer.failed && io.outputError == nil && result.state == .completed
    }

    func summary() { io.line("Run completed: \(messages) messages, \(opcodes) opcodes, \(emits) emits, \(publishes) publishes.", toError: true) }
}

final class RunObserver: GameEventScriptRuntimeObserver {
    let io: ToolIO
    let options: RunOptions
    private lazy var highlighting = Highlighting()
    var failed = false
    var exitCode = 0

    init(io: ToolIO, options: RunOptions) {
        self.io = io
        self.options = options
    }

    func subscribe(_ inventory: RunInventory) throws {
        try inventory.subscribe("ConsoleOut", handler: ConsoleHandler(observer: self, error: false))
        try inventory.subscribe("ConsoleErr", handler: ConsoleHandler(observer: self, error: true))
        try inventory.subscribe("ErrorCode", handler: ErrorCodeHandler(observer: self))
    }

    func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool) { trace("emit " + format(message) + (accepted ? "" : " [not queued]")) }

    func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult) { trace("publish " + format(message) + (result.localAccepted ? "" : " [not queued]")) }

    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) { trace("dispatch \(signatureID) <- " + format(message)) }

    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) {
        failed = true
        io.line("error cli.runtimeLimit: \(limitName)=\(limit): \(detail)", toError: true)
    }

    func runtimeError(_ diagnostic: GameEventScriptDiagnostic) {
        failed = true
        io.diagnostic(diagnostic, fallback: "runtime")
    }

    private func trace(_ text: String) { if options.verbose { io.line(options.color && options.interactive ? Highlighting.paint(text, 33) : text, toError: true) } }

    private func format(_ message: GameEventScriptMessage) -> String {
        var text = message.name
        if !message.arguments.isEmpty {
            text += "(" + (0..<message.arguments.count).map { message.arguments.nameAt($0) + ": " + (message.arguments[$0].kind == .text ? quoted(message.arguments[$0].asText) : message.arguments[$0].description) }.joined(separator: ", ") + ")"
        }
        if !message.tags.isEmpty { text += " with " + message.tags.map { "#" + $0 }.joined(separator: ", ") }
        return text
    }

    func console(_ message: GameEventScriptMessage, error: Bool) {
        var text = ""
        for index in 0..<message.arguments.count {
            let value = message.arguments[index]
            if options.color && !error && value.kind != .text { text += [.integer, .float, .percentage].contains(value.kind) ? Highlighting.paint(value.description, 34) : highlighting.render(value.description) } else { text += value.description }
        }
        io.line(options.color && error ? Highlighting.paint(text, 31) : text, toError: error)
    }

    private struct ConsoleHandler: GameEventScriptNativeMessageHandler {
        let observer: RunObserver
        let error: Bool

        func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws { observer.console(message, error: error) }
    }

    private struct ErrorCodeHandler: GameEventScriptNativeMessageHandler {
        let observer: RunObserver

        func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws {
            guard message.arguments.count == 1, ["code", "_"].contains(message.arguments.nameAt(0)) else { throw try invalid() }
            let value = message.arguments[0]
            if value.kind == .nothing {
                observer.exitCode = 0
                return
            }
            let number = value.asNumber
            guard [.integer, .float].contains(value.kind), value.unit == .none, number.isFinite, (0...255).contains(number), number == number.rounded(.towardZero) else { throw try invalid() }
            observer.exitCode = Int(number)
        }

        private func invalid() throws -> GameEventScriptExtensionFault { try .init(code: "cli.errorCodeArgument", message: "ErrorCode requires one code argument: a unitless integer from 0 to 255, or nothing.") }
    }
}
