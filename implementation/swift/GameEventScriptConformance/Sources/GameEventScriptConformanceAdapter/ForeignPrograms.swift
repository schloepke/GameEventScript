// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

enum ForeignPrograms {
    nonisolated(unsafe) static var inputs: Input?
    nonisolated(unsafe) static var cache: [String: GameEventScriptProgram] = [:]

    static func read(_ test: Input, group: String?) throws -> GameEventScriptProgram {
        let id = try test.text("id")
        let key = group ?? test.values("sources").first?["program"]?.stringValue ?? "main"
        let cacheKey = id + "/" + key
        if let program = cache[cacheKey] { return program }
        guard let encoded = inputs?[id]?[key]?.stringValue, let data = Data(base64Encoded: encoded) else { throw ConformanceExecutionError.invalidInput("Missing foreign Program") }
        let program = try GameEventScriptProgramReader.read(Array(data))
        cache[cacheKey] = program
        return program
    }
}
