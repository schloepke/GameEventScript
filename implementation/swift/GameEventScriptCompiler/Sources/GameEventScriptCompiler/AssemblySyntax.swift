// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

struct GesAssembly {
    let output: String?
    let declarations: [(name: String, export: Bool)]
    let lines: [GesAssemblyLine]
    let predicate: Bool
    let allowSend: Bool
    let location: GameEventScriptSourceLocation
}

struct GesAssemblyLine {
    let name: String
    let label: Bool
    let operands: [GesExpression]
    let location: GameEventScriptSourceLocation
}
