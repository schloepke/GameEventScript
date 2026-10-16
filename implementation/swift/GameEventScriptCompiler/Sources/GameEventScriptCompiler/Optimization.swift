// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    // The C# reference pipeline: two cleanup rounds with loop-invariant hoisting between them.
    static let defaultOptimizationPasses: [any GesBinaryOptimizationPass] = [
        LocalConstantFoldingPass(), DeadTempWriteEliminationPass(), RegisterPressureReductionPass(), PeepholeMoveEliminationPass(),
        BranchSimplificationPass(), JumpCleanupPass(), LoopInvariantHoistingPass(),
        LocalConstantFoldingPass(), DeadTempWriteEliminationPass(), RegisterPressureReductionPass(), PeepholeMoveEliminationPass(),
        BranchSimplificationPass(), JumpCleanupPass(),
    ]

    func optimize(_ routine: GesRoutine, effects: EffectAnalysis) {
        if routine.hasAssembly { return }
        let context = RewriteContext(self, routine, effects)
        for pass in Self.defaultOptimizationPasses { _ = pass.rewrite(context) }
    }

    // Moves loop-invariant instructions in front of their loop, so they run once per loop entry
    // instead of once per iteration. Only pure, total and deterministic operations qualify: they
    // read no random state, call no host code, emit nothing, consume no runtime budget and cannot
    // fail. Results are therefore unchanged; executed opcode counts and register requirements are not.
    // Mirrors LoopInvariantHoistingPass of the C# reference compiler instruction for instruction.
    func hoistLoopInvariants(_ routine: GesRoutine) {
        guard routine.code.indices.contains(where: { Self.backEdges.contains(routine.code[$0].opcode) && Int(routine.code[$0].word2) <= $0 }) else { return }
        // Each round moves at least one instruction outward, so this terminates. Only a loop
        // deferred because it contains a rewritten loop can still have candidates afterwards.
        while hoistRound(routine, &operandLayouts) {}
    }

    private static let branches: Set<GesBytecodeOpCode> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorNext, .iteratorCreateOrJump]

    private static let backEdges: Set<GesBytecodeOpCode> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing]

    // Constant loads and arithmetic are side-effect free, never fail (invalid input yields
    // Nothing or a float result), draw no random numbers and charge no runtime budget.
    private static let pureTotalOperations: Set<GesBytecodeOpCode> = [
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
            // The loop must be entered only by falling into its first instruction. A jump from before the loop to its
            // first instruction targets a label marked before the C# loop header label (such as for_source_done), so it
            // does not enter the loop and later lands in front of the hoisted instructions.
            guard !(start...end).contains(where: { ($0 != start && analysis.firstReference[$0] < start) || analysis.lastReference[$0] > end }) else { continue }
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
        let labels = routine.labels
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
        // Jumps from before the loop keep addressing its start, which now holds the hoisted instructions.
        let outerLabel = routine.code.indices.contains { $0 < start && Self.branches.contains(routine.code[$0].opcode) && Int(routine.code[$0].word2) == start }
        for index in routine.code.indices where Self.branches.contains(routine.code[index].opcode) {
            let target = Int(routine.code[index].word2)
            if span.contains(target) && !(index < start && target == start) { routine.patch(index, target: resolved[target - start]) }
        }
        for index in routine.calls.indices where span.contains(routine.calls[index].0) { routine.calls[index].0 = resolved[routine.calls[index].0 - start] }
        for index in routine.symbols.indices where span.contains(routine.symbols[index].2) { routine.symbols[index].2 = resolved[routine.symbols[index].2 - start] }
        routine.labels = Set(labels.map { span.contains($0) ? resolved[$0 - start] : $0 })
        if outerLabel { routine.labels.insert(start) }
    }
}

// Register and register-list operand slots of one opcode shape, computed once per compilation.
struct GesOperandSlots {
    let registers: [Int]
    let lists: [Int]

    static func of(_ instruction: GesBytecodeInstruction, _ cache: inout [Int: GesOperandSlots]) -> GesOperandSlots {
        // Slots depend on the opcode, its flags (iterator modes) and the whitespace split operand.
        let key = Int(instruction.opcode.rawValue) << 16 | Int(instruction.unitAndFlags) << 1 | (instruction.opcode == .splitText && instruction.a == 1 ? 1 : 0)
        if let slots = cache[key] { return slots }
        let slots = GesOperandSlots(registers: GesCompilerSupport.registerSlots(instruction), lists: GesCompilerSupport.listSlots(instruction, text: false))
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
    static func isBranch(_ opcode: GesBytecodeOpCode) -> Bool {
        switch opcode {
        case .jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .iteratorNext, .iteratorCreateOrJump: true
        default: false
        }
    }
}

extension GesBytecodeInstruction {
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
