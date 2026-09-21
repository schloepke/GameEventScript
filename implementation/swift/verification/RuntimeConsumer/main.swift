// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

final class Receiver: GameEventScriptNativeMessageHandler {
    var value: GesValue = .nothing

    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws { value = message.arguments[0] }
}

let receiver = Receiver()
let program = try GameEventScriptProgramReader.read(Array(Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1]))))
let host = try GameEventScriptHost.createBuilder().withRandomSeed(1).build()
_ = try host.subscribeMessageName("Done", handler: receiver)
_ = try host.load(program)
let startup = try host.start()
precondition(startup.state == .ready)
let message = try GameEventScriptMessage(name: "Start", arguments: [.init(name: "value", value: .text("41"))])
precondition(host.receive(message))
let execution = try host.runToCompletion()
precondition(execution.state == .completed)
precondition(receiver.value == .integer(42))
print("SwiftPM Runtime-only consumer passed")
