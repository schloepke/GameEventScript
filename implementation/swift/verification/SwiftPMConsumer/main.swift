// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import GameEventScriptSwiftBridge

let program = try GameEventScriptBuilder.create().addScript("on Start(value) { emit Done(result: (parse value) + 1) }").compile()
let bytes = try GameEventScriptProgramWriter.bytes(program)
try Data(bytes).write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
let host = try GameEventScriptHost.createBuilder().withRandomSeed(1).build()
var result: GesValue = .nothing
_ = try host.subscribeMessageName("Done") { message, _ in result = message.arguments[0] }
_ = try host.load(GameEventScriptProgramReader.read(bytes))
let startup = try host.start()
precondition(startup.state == .ready)
let message = try GameEventScriptMessage(name: "Start", swiftArguments: [("value", "41")])
precondition(host.receive(message))
let execution = try host.runToCompletion()
precondition(execution.state == .completed)
precondition(result == .integer(42))
print("SwiftPM Compiler/Runtime/SwiftBridge consumer passed")
