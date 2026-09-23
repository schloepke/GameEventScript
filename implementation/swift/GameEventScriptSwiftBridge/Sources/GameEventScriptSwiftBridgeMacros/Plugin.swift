// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import SwiftCompilerPlugin
import SwiftSyntaxMacros

@main
struct GameEventScriptSwiftBridgePlugin: CompilerPlugin {
    let providingMacros: [Macro.Type] = [GesTypeMacro.self, GesFieldMacro.self, GesConstructMacro.self]
}
