// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import GameEventScriptSyntaxHighlighter

@GesType("ConsumerPoint")
struct ConsumerPoint {
    @GesField
    let value: Int

    @GesConstruct
    init(value: Int) { self.value = value }
}

let highlighted = try GameEventScriptSyntaxHighlighter().highlight("emit Done(42)")
precondition(highlighted.isComplete && highlighted.spans.contains { $0.kind == .number })
let pointType = try ConsumerPoint.createGesType()
let types = try GameEventScriptSwiftExternalTypeRegistry([pointType.binding])
let program = try GameEventScriptBuilder.create().addScript("on Start(value) { emit Done(result: (parse value) + 1) }").compile()
let macroProgram = try GameEventScriptBuilder.create().withExternalTypeCatalog(types).addScript("on FromMacro(value) { let point be :ConsumerPoint(value: value); emit Done(result: point.value) }").compile()
let bytes = try GameEventScriptProgramWriter.bytes(program)
try Data(bytes).write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
let host = try GameEventScriptHost.createBuilder().withRandomSeed(1).withExternalTypeRegistry(types).build()
var result: GesValue = .nothing
_ = try host.subscribeMessageName("Done") { message, _ in result = message.arguments[0] }
_ = try host.load(GameEventScriptProgramReader.read(bytes))
_ = try host.load(GameEventScriptProgramReader.read(GameEventScriptProgramWriter.bytes(macroProgram)))
let startup = try host.start()
precondition(startup.state == .ready)
let message = try GameEventScriptMessage(name: "Start", swiftArguments: [("value", "41")])
precondition(host.receive(message))
let execution = try host.runToCompletion()
precondition(execution.state == .completed)
precondition(result == .integer(42))
let macroMessage = try GameEventScriptMessage(name: "FromMacro", swiftArguments: [("value", 17)])
precondition(host.receive(macroMessage))
let macroExecution = try host.runToCompletion()
precondition(macroExecution.state == .completed)
precondition(result == .integer(17))
print("SwiftPM Compiler/Runtime/SwiftBridge consumer passed")
