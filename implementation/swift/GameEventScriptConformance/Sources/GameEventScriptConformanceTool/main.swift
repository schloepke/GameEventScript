// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

enum ToolError: Error {
    case invalidArguments(String)
    case invalidFixture(String)
}

do {
    let args = Array(CommandLine.arguments.dropFirst())
    guard args.count == 3 else { throw ToolError.invalidArguments("Use --binary-fuzz, --message-json or --number-text with input and output paths") }
    switch args[0] {
    case "--binary-fuzz": try BinaryFuzzProbe.run(input: args[1], output: args[2])
    case "--number-text": try NumberTextProbe.run(input: args[1], output: args[2])
    case "--message-json":
        let input = try JSONDecoder().decode([String].self, from: Data(contentsOf: URL(fileURLWithPath: args[1])))
        let normalized = try input.map { try GameEventScriptMessage(json: $0).json() }
        try JSONEncoder().encode(normalized).write(to: URL(fileURLWithPath: args[2]))
    default: throw ToolError.invalidArguments("Unknown probe")
    }
} catch {
    FileHandle.standardError.write(Data((String(describing: error) + "\n").utf8))
    exit(2)
}
