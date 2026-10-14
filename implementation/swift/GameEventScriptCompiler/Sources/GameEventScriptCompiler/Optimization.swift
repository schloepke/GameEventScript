// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    func optimize(_ routine: GesRoutine) {
        if routine.hasAssembly { return }
        let branches: Set<Op> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorNext, .iteratorCreateOrJump]
        for round in 0..<2 {
            // Same position as the C# reference pipeline: between the two cleanup rounds.
            if round == 1 { hoistLoopInvariants(routine) }
            var reads: [Int: Int] = [:]
            var writes: [Int: [Int]] = [:]
            for (index, instruction) in routine.code.enumerated() {
                let fields = instruction.fields
                for slot in GameEventScriptCompilerSupport.registerSlots(instruction) {
                    let register = Int(fields[slot])
                    if slot == 0 { writes[register, default: []].append(index) } else { reads[register, default: 0] += 1 }
                }
                for slot in GameEventScriptCompilerSupport.listSlots(instruction, text: false) {
                    for register in lists[Int(fields[slot])] {
                        if slot == 0 { writes[Int(register), default: []].append(index) } else { reads[Int(register), default: 0] += 1 }
                    }
                }
            }
            let targets = Set(routine.code.filter { branches.contains($0.opcode) }.map { Int($0.word2) })
            var removed: Set<Int> = []
            for (index, move) in routine.code.enumerated() where move.opcode == .move && move.unitAndFlags == 0 {
                let source = Int(move.word1)
                let destination = move.word0
                guard !routine.pinned.contains(source), reads[source] == 1, writes[source]?.count == 1, let writer = writes[source]?.first, writer < index, !removed.contains(writer) else { continue }
                let instruction = routine.code[writer]
                if branches.contains(instruction.opcode) { continue }
                let fields = instruction.fields
                if GameEventScriptCompilerSupport.registerSlots(instruction).contains(where: { $0 != 0 && fields[$0] == destination }) { continue }
                var blocked = targets.contains(index)
                for i in (writer + 1)..<index {
                    let candidate = routine.code[i]
                    if targets.contains(i) || branches.contains(candidate.opcode) || candidate.opcode == .returnVoid || candidate.opcode == .returnValue {
                        blocked = true
                        break
                    }
                    if GameEventScriptCompilerSupport.registerSlots(candidate).contains(where: { candidate.fields[$0] == destination }) {
                        blocked = true
                        break
                    }
                    for slot in GameEventScriptCompilerSupport.listSlots(candidate, text: false) where lists[Int(candidate.fields[slot])].contains(destination) { blocked = true }
                }
                if !blocked {
                    var fields = fields
                    fields[0] = destination
                    routine.code[writer] = instruction.replacing(fields)
                    removed.insert(index)
                }
            }
            // Fold a conditional skip over an unconditional jump into its inverse.
            // A separately targeted jump must remain an entry point.
            for index in routine.code.indices where index + 1 < routine.code.count {
                let branch = routine.code[index]
                let jump = routine.code[index + 1]
                let inverse: Op? = branch.opcode == .jumpIfTrue ? .jumpIfFalse : branch.opcode == .jumpIfFalse || branch.opcode == .jumpIfNotTrue ? .jumpIfTrue : nil
                if let inverse, Int(branch.word2) == index + 2, jump.opcode == .jump, !targets.contains(index + 1) {
                    routine.code[index] = .init(
                        opcode: inverse,
                        unitAndFlags: branch.unitAndFlags,
                        word0: branch.word0,
                        word1: branch.word1,
                        word2: jump.word2,
                        payload: branch.payload
                    )
                    removed.insert(index + 1)
                }
            }
            for index in routine.code.indices where branches.contains(routine.code[index].opcode) {
                let instruction = routine.code[index]
                var target = Int(instruction.word2)
                var seen: Set<Int> = []
                while target < routine.code.count && routine.code[target].opcode == .jump && seen.insert(target).inserted { target = Int(routine.code[target].word2) }
                routine.patch(index, target: target)
                if instruction.opcode == .jump && target == index + 1 { removed.insert(index) }
            }
            if removed.isEmpty { continue }
            var addresses = [Int](repeating: 0, count: routine.code.count + 1)
            var next = 0
            for index in routine.code.indices {
                addresses[index] = next
                if !removed.contains(index) { next += 1 }
            }
            addresses[routine.code.count] = next
            var code: [Instruction] = []
            var locations: [GameEventScriptSourceLocation] = []
            for index in routine.code.indices where !removed.contains(index) {
                let instruction = routine.code[index]
                var fields = instruction.fields
                if branches.contains(instruction.opcode) { fields[2] = UInt16(addresses[Int(instruction.word2)]) }
                code.append(instruction.replacing(fields))
                locations.append(routine.locations[index])
            }
            routine.code = code
            routine.locations = locations
            routine.calls = routine.calls.map { (addresses[$0.0], $0.1) }
        }
    }

    // Moves loop-invariant instructions in front of their loop, so they run once per loop entry
    // instead of once per iteration. Only pure, total and deterministic operations qualify: they
    // read no random state, call no host code, emit nothing, consume no runtime budget and cannot
    // fail. Results are therefore unchanged; executed opcode counts and register requirements are not.
    // Mirrors LoopInvariantHoistingPass of the C# reference compiler instruction for instruction.
    private func hoistLoopInvariants(_ routine: GesRoutine) {
        guard routine.code.indices.contains(where: { Self.backEdges.contains(routine.code[$0].opcode) && Int(routine.code[$0].word2) <= $0 }) else { return }
        // Each round moves at least one instruction outward, so this terminates. Only a loop
        // deferred because it contains a rewritten loop can still have candidates afterwards.
        while hoistRound(routine, &operandLayouts) {}
    }

    private static let branches: Set<Op> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorNext, .iteratorCreateOrJump]

    private static let backEdges: Set<Op> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing]

    // Constant loads and arithmetic are side-effect free, never fail (invalid input yields
    // Nothing or a float result), draw no random numbers and charge no runtime budget.
    private static let pureTotalOperations: Set<Op> = [
        .loadNothing, .loadTrue, .loadFalse, .loadInteger, .loadFloat, .loadPercentage, .loadText, .loadTag,
        .add, .subtract, .multiply, .divide, .integerDivide, .modulo, .remainder, .negate, .abs,
    ]

    // Moving instructions inside one loop changes no position outside it, so one analysis serves
    // every loop of a round that neither contains nor overlaps an already rewritten loop. A loop
    // containing a rewritten one waits for the next round and its fresh analysis.
    private func hoistRound(_ routine: GesRoutine, _ layouts: inout [Int: GesOperandSlots]) -> Bool {
        let analysis = GesLoopAnalysis(routine, lists, &layouts)
        var rewritten: (start: Int, end: Int)?
        var deferred = false
        for end in routine.code.indices {
            let backEdge = routine.code[end]
            let start = Int(backEdge.word2)
            guard Self.backEdges.contains(backEdge.opcode), start <= end else { continue }
            if let rewritten, start <= rewritten.end && rewritten.start <= end {
                deferred = true
                continue
            }
            // The loop must be entered only by falling into its first instruction.
            guard !(start...end).contains(where: { analysis.firstReference[$0] < start || analysis.lastReference[$0] > end }) else { continue }
            // One forward sweep finds chains such as a constant load and the product reading it.
            var marked: [Int] = []
            var hoisted: [Bool] = []
            for index in start..<end where isLoopInvariant(routine, index, start: start, end: end, analysis: analysis, hoisted: hoisted, &layouts) {
                if hoisted.isEmpty { hoisted = .init(repeating: false, count: routine.registers) }
                marked.append(index)
                hoisted[Int(routine.code[index].word0)] = true
            }
            if marked.isEmpty { continue }
            moveInstructions(routine, marked, start: start, end: end)
            // Loops are visited by ascending end, so later disjoint loops start after this one.
            rewritten = (min(rewritten?.start ?? start, start), end)
        }
        return deferred
    }

    private func isLoopInvariant(_ routine: GesRoutine, _ index: Int, start: Int, end: Int, analysis: GesLoopAnalysis, hoisted: [Bool], _ layouts: inout [Int: GesOperandSlots]) -> Bool {
        let instruction = routine.code[index]
        guard Self.pureTotalOperations.contains(instruction.opcode) else { return false }
        let slots = GesOperandSlots.of(instruction, &layouts)
        guard slots.registers.contains(0), slots.lists.isEmpty else { return false }
        // The destination has no other definition and is read only later in this loop body,
        // so no path observes it before this instruction or after a zero-iteration loop.
        let destination = Int(instruction.word0)
        guard !routine.pinned.contains(destination), analysis.writeCount[destination] == 1 else { return false }
        if analysis.lastRead[destination] >= 0 && (analysis.firstRead[destination] <= index || analysis.lastRead[destination] > end) { return false }
        // Every input is final before the loop starts, possibly by an earlier hoist of this sweep.
        for slot in slots.registers where slot != 0 {
            let source = Int(instruction.field(slot))
            if source == destination || !(analysis.lastWrite[source] < start || (!hoisted.isEmpty && hoisted[source])) { return false }
        }
        return true
    }

    // Moves the marked instructions of the loop start...end, in order, in front of `start`. A position
    // that referred to a moved instruction now addresses the next remaining one, as a C# label in
    // front of it would. Positions outside the loop are unchanged.
    private func moveInstructions(_ routine: GesRoutine, _ marked: [Int], start: Int, end: Int) {
        let span = start...end
        var isMarked = [Bool](repeating: false, count: span.count)
        for index in marked { isMarked[index - start] = true }
        let order = marked + span.filter { !isMarked[$0 - start] }
        var newIndex = [Int](repeating: 0, count: span.count)
        for (offset, old) in order.enumerated() { newIndex[old - start] = start + offset }
        // A position resolves to the first remaining instruction at or after it.
        var resolved = [Int](repeating: end + 1, count: span.count + 1)
        for offset in stride(from: span.count - 1, through: 0, by: -1) { resolved[offset] = isMarked[offset] ? resolved[offset + 1] : newIndex[offset] }
        routine.code.replaceSubrange(span, with: order.map { routine.code[$0] })
        routine.locations.replaceSubrange(span, with: order.map { routine.locations[$0] })
        for index in routine.code.indices where Self.branches.contains(routine.code[index].opcode) {
            let target = Int(routine.code[index].word2)
            if span.contains(target) { routine.patch(index, target: resolved[target - start]) }
        }
        for index in routine.calls.indices where span.contains(routine.calls[index].0) { routine.calls[index].0 = resolved[routine.calls[index].0 - start] }
        for index in routine.symbols.indices where span.contains(routine.symbols[index].2) { routine.symbols[index].2 = resolved[routine.symbols[index].2 - start] }
    }
}

// Register and register-list operand slots of one opcode shape, computed once per compilation.
struct GesOperandSlots {
    let registers: [Int]
    let lists: [Int]

    static func of(_ instruction: Instruction, _ cache: inout [Int: GesOperandSlots]) -> GesOperandSlots {
        // Slots depend on the opcode, its flags (iterator modes) and the whitespace split operand.
        let key = Int(instruction.opcode.rawValue) << 16 | Int(instruction.unitAndFlags) << 1 | (instruction.opcode == .splitText && instruction.a == 1 ? 1 : 0)
        if let slots = cache[key] { return slots }
        let slots = GesOperandSlots(registers: GameEventScriptCompilerSupport.registerSlots(instruction), lists: GameEventScriptCompilerSupport.listSlots(instruction, text: false))
        cache[key] = slots
        return slots
    }
}

// Per-register definition/use positions and per-position branch references of one routine.
struct GesLoopAnalysis {
    var writeCount: [Int]
    var lastWrite: [Int]
    var firstRead: [Int]
    var lastRead: [Int]
    var firstReference: [Int]
    var lastReference: [Int]

    init(_ routine: GesRoutine, _ lists: [[UInt16]], _ layouts: inout [Int: GesOperandSlots]) {
        // Before allocation, routine.registers counts the virtual registers.
        let registers = routine.registers
        writeCount = .init(repeating: 0, count: registers)
        lastWrite = .init(repeating: -1, count: registers)
        firstRead = .init(repeating: .max, count: registers)
        lastRead = .init(repeating: -1, count: registers)
        firstReference = .init(repeating: .max, count: routine.code.count)
        lastReference = .init(repeating: -1, count: routine.code.count)
        for (index, instruction) in routine.code.enumerated() {
            let target = Int(instruction.word2)
            if GesCompiler.isBranch(instruction.opcode), target < routine.code.count {
                firstReference[target] = min(firstReference[target], index)
                lastReference[target] = index
            }
            let slots = GesOperandSlots.of(instruction, &layouts)
            for slot in slots.registers {
                let register = Int(instruction.field(slot))
                // Closing an iterator also clears its register.
                if slot == 0 || instruction.opcode == .iteratorClose { write(register, index) }
                if slot != 0 { read(register, index) }
            }
            for slot in slots.lists {
                for register in lists[Int(instruction.field(slot))] {
                    if slot == 0 { write(Int(register), index) } else { read(Int(register), index) }
                }
            }
        }
    }

    private mutating func write(_ register: Int, _ index: Int) {
        writeCount[register] += 1
        lastWrite[register] = index
    }

    private mutating func read(_ register: Int, _ index: Int) {
        firstRead[register] = min(firstRead[register], index)
        lastRead[register] = index
    }
}

extension GesCompiler {
    static func isBranch(_ opcode: Op) -> Bool {
        switch opcode {
        case .jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorNext, .iteratorCreateOrJump: true
        default: false
        }
    }
}

extension Instruction {
    // Operand field by slot, without materializing the `fields` array.
    func field(_ slot: Int) -> UInt16 {
        switch slot {
        case 0: word0
        case 1: word1
        case 2: word2
        case 3: a
        case 4: b
        case 5: c
        default: d
        }
    }
}
