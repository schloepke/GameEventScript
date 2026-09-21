// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Iterative call-graph and control-flow analysis, including unreachable retained code.
enum GesProgramResourceValidator {
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

        func callee(_ i: GameEventScriptBytecodeInstruction) -> Int? {
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
                if states[target] == 1 { throw GameEventScriptProgramValidator.failure(.cyclicCallGraph) }
                states[target] = 1
                path.append(target)
            }
        }
        var arguments = [Int](repeating: -1, count: entries.count)
        var registers = [Int](repeating: 0, count: entries.count)
        var depths = registers
        var maximumFrames = registers
        var frames = [Int](repeating: -1, count: p.code.count)
        var stages = frames

        func invalid(_ index: Int) -> GameEventScriptProgramFormatError { GameEventScriptProgramValidator.failure(.invalidResourceMetadata, 16, index) }

        for binding in executable {
            let routine = indexes[Int(binding.entryAddress)]!
            let count = binding.kind == .messageNameHandler ? 1 : binding.argumentNames.count
            if arguments[routine] >= 0 && arguments[routine] != count { throw invalid(Int(binding.entryAddress)) }
            arguments[routine] = count
        }
        var staged = 0
        for (index, i) in p.code.enumerated() {
            if i.opcode.isStage {
                staged += 1
                continue
            }
            if i.opcode == .call {
                let target = indexes[Int(i.word2)]!
                if arguments[target] >= 0 && arguments[target] != staged { throw invalid(index) }
                arguments[target] = staged
            }
            staged = 0
        }
        for routine in completed {
            let start = entries[routine]
            let end = routine + 1 < entries.count ? entries[routine + 1] : p.code.count
            registers[routine] = max(0, arguments[routine])
            maximumFrames[routine] = registers[routine]
            var pending: [Int] = []

            func enqueue(_ address: Int, _ frame: Int, _ stage: Int) throws {
                if address < start || address >= end { throw invalid(address) }
                if frames[address] >= 0 {
                    if frames[address] != frame || stages[address] != stage { throw invalid(address) }
                    return
                }
                frames[address] = frame
                stages[address] = stage
                pending.append(address)
            }

            try enqueue(start, registers[routine], 0)
            var cursor = 0
            while cursor < pending.count {
                let index = pending[cursor]
                cursor += 1
                let i = p.code[index]
                let opcode = i.opcode
                var frame = frames[index]
                var stage = stages[index]
                if stage > 0 && !opcode.isStage && !opcode.consumesStage { throw invalid(index) }
                if opcode == .registerLocals {
                    frame += Int(i.signedWord1)
                    if !(0...65535).contains(frame) { throw invalid(index) }
                }
                maximumFrames[routine] = max(maximumFrames[routine], frame)
                try GameEventScriptProgramValidator.validateFrame(p, i, index, frame)
                if opcode.isStage { stage += 1 }
                registers[routine] = max(registers[routine], frame + stage)
                if let target = callee(i) {
                    if (opcode == .call || opcode == .createRecord) && stage != arguments[target] { throw invalid(index) }
                    registers[routine] = max(registers[routine], frame + registers[target])
                    depths[routine] = max(depths[routine], 1 + depths[target])
                }
                if registers[routine] > 65535 || depths[routine] > 65535 { throw invalid(index) }
                if opcode.consumesStage { stage = 0 }
                if opcode == .returnVoid || opcode == .returnValue { continue }
                if opcode.branches { try enqueue(Int(i.word2), frame, stage) }
                if opcode != .jump { try enqueue(index + 1, frame, stage) }
            }
        }
        var routine = -1
        for index in p.code.indices {
            if routine + 1 < entries.count && index == entries[routine + 1] { routine += 1 }
            if frames[index] < 0 { try GameEventScriptProgramValidator.validateFrame(p, p.code[index], index, routine < 0 ? 0 : maximumFrames[routine]) }
        }
        for (index, symbol) in (p.debugSymbols ?? []).enumerated() {
            let position = entries.partitionPoint { $0 > Int(symbol.codeStart) } - 1
            let end = position + 1 < entries.count ? entries[position + 1] : p.code.count
            if position < 0 || UInt64(symbol.codeStart) + UInt64(symbol.codeLength) > UInt64(end) || Int(symbol.registerID) >= maximumFrames[position] { throw GameEventScriptProgramValidator.failure(.invalidDebugSymbol, 32, index) }
        }
        for (index, binding) in p.bindings.enumerated() where binding.isHandler {
            let routine = indexes[Int(binding.entryAddress)]!
            if Int(binding.requiredRegisterCount) < registers[routine] || Int(binding.requiredCallStackDepth) < depths[routine] { throw GameEventScriptProgramValidator.failure(.invalidResourceMetadata, 4, index) }
        }
    }
}

extension GameEventScriptBytecodeOpCode {
    var isStage: Bool { [.stageRegister, .stageNothing, .stageTrue, .stageFalse, .stageInteger, .stageFloat, .stageText, .stageTag, .stagePercentage].contains(self) }
    var consumesStage: Bool { [.call, .createVector, .createPoint, .createList, .createMap, .createRecord, .createExternalType].contains(self) }
    var branches: Bool { [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorCreateOrJump, .iteratorNext].contains(self) }
}

extension Array where Element == Int {
    func partitionPoint(where predicate: (Int) -> Bool) -> Int {
        var low = 0
        var high = count
        while low < high {
            let middle = low + (high - low) / 2
            if predicate(self[middle]) { high = middle } else { low = middle + 1 }
        }
        return low
    }
}
