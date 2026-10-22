// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Iterative, recursion-free call-graph analysis; rejects cycles before resource analysis runs on its routine order.
enum GesProgramCallGraphValidator {
    static func validate(_ p: GameEventScriptProgram) throws {
        if p.code.isEmpty { return }
        let executable = p.bindings.filter(\.isExecutable)
        let entries = Set(executable.map { Int($0.entryAddress) } + p.code.filter { $0.opcode == .call }.map { Int($0.word2) }).sorted()
        let indexes = Dictionary(uniqueKeysWithValues: entries.enumerated().map { ($0.element, $0.offset) })
        var recordIDs: [UInt16: Int] = [:]
        var recordNames: [String: Int] = [:]
        for binding in executable where binding.kind == .record {
            recordIDs[binding.id] = Int(binding.entryAddress)
            recordNames[p.stringConstants[Int(binding.name)]] = Int(binding.entryAddress)
        }

        func callee(_ i: GesBytecodeInstruction) -> Int? {
            let address: Int?
            switch i.opcode {
            case .call: address = Int(i.word2)
            case .createRecord: address = recordIDs[i.word1]
            case .castCustom: address = recordNames[p.stringConstants[Int(i.word2)]]
            default: address = nil
            }
            return address.flatMap { indexes[$0] }
        }

        var calls = [[Int]](repeating: [], count: entries.count)
        for routine in entries.indices {
            let end = routine + 1 < entries.count ? entries[routine + 1] : p.code.count
            var seen: Set<Int> = []
            for index in entries[routine]..<end { if let target = callee(p.code[index]), seen.insert(target).inserted { calls[routine].append(target) } }
        }
        // No recursion: malformed or deep programs cannot exhaust the native stack.
        var states = [UInt8](repeating: 0, count: entries.count)
        var next = [Int](repeating: 0, count: entries.count)
        var completed: [Int] = []
        for root in entries.indices where states[root] == 0 {
            var path = [root]
            states[root] = 1
            while let current = path.last {
                if next[current] == calls[current].count {
                    states[current] = 2
                    completed.append(current)
                    path.removeLast()
                    continue
                }
                let target = calls[current][next[current]]
                next[current] += 1
                if states[target] == 2 { continue }
                if states[target] == 1 { throw GesProgramValidator.failure(.cyclicCallGraph) }
                states[target] = 1
                path.append(target)
            }
        }
        try GesProgramResourceValidator.validate(p, executable: executable, entries: entries, indexes: indexes, completed: completed, callee: callee)
    }
}
