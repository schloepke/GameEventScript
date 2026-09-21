// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import TerminalSupport

@main struct GesMain {
    static func main() {
        ges_terminal_initialize()
        let io = ToolIO()
        let result = Tool(io: io).run(Array(CommandLine.arguments.dropFirst()))
        exit(Int32(io.outputError == nil ? result : 1))
    }
}
