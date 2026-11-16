// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptGenerator
import GameEventScriptNetwork
import GameEventScriptNetworkSockets
import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import GameEventScriptSyntax

@GesType("ConsumerPoint")
struct ConsumerPoint {
    @GesField
    let value: Int

    @GesConstruct
    init(value: Int) { self.value = value }
}

let highlighted = try GesSyntaxHighlighter().highlight("emit Done(42)")
precondition(highlighted.isComplete && highlighted.spans.contains { $0.kind == .number })
let pointType = try ConsumerPoint.createGesType()
let types = try GesBridgeExternalTypeRegistry([pointType.binding])
let program = try GesProgramBuilder().addScript("on Start(value) { emit Done(result: (parse value) + 1) }").compile()
let macroProgram = try GesProgramBuilder().withExternalTypeCatalog(types).addScript("on FromMacro(value) { let point be :ConsumerPoint(value: value); emit Done(result: point.value) }").compile()
let bytes = try program.encode()
try Data(bytes).write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
let host = try GameEventScriptHost.makeBuilder().withRandomSeed(1).withExternalTypeRegistry(types).build()
var result: GesValue = .nothing
_ = try host.subscribeMessageName("Done") { message, _ in result = message.arguments[0] }
_ = try host.load(GameEventScriptProgram.read(bytes))
_ = try host.load(GameEventScriptProgram.read(macroProgram.encode()))
let startup = try host.start()
precondition(startup.state == .ready)
let message = try GameEventScriptMessage(name: "Start", nativeArguments: [("value", "41")])
precondition(host.receive(message))
let execution = try host.runToCompletion()
precondition(execution.state == .completed)
precondition(result == .integer(42))
let macroMessage = try GameEventScriptMessage(name: "FromMacro", nativeArguments: [("value", 17)])
precondition(host.receive(macroMessage))
let macroExecution = try host.runToCompletion()
precondition(macroExecution.state == .completed)
precondition(result == .integer(17))
let generated = try GesSchemaGenerator.generate(GesSchemaSet(parsing: "schema :Ping as {\n  ready: :Boolean\n}\n"), roots: ["Ping"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, namespace: "Consumer"))
precondition(generated.contains("public struct Ping"))
let network = try GesSchemaSet(parsing: "wire schema :Ping as { ready: :Boolean }\nnetwork :Game as {\n  server sends :Ping reliable\n}\n").network("Game").bind()
let client = try GesNetworkConnection.makeClient(network: network, token: Array("t".utf8), now: 0)
let server = try GesNetworkConnection.makeServer(network: network, now: 0)
let loopback = try GesNetworkLoopback(client: client, server: server)
try loopback.advance(0)
try server.accept()
try loopback.advance(0)
precondition(client.state == .connected)
precondition(String(describing: GesNetworkSocketListener.self) == "GesNetworkSocketListener")
print("SwiftPM Compiler/Runtime/SwiftBridge/Generator/Network consumer passed")
