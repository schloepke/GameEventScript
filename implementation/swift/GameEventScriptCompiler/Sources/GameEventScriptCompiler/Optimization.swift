// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesBinaryBuilder {
    // The C# reference pipeline: two cleanup rounds with loop-invariant hoisting between them.
    static let defaultOptimizationPasses: [any GesBinaryOptimizationPass] = [
        LocalConstantFoldingPass(), DeadTempWriteEliminationPass(), RegisterPressureReductionPass(), PeepholeMoveEliminationPass(),
        BranchSimplificationPass(), JumpCleanupPass(), LoopInvariantHoistingPass(),
        LocalConstantFoldingPass(), DeadTempWriteEliminationPass(), RegisterPressureReductionPass(), PeepholeMoveEliminationPass(),
        BranchSimplificationPass(), JumpCleanupPass(),
    ]

    func optimize(_ routine: GesRoutine, _ context: RewriteContext) {
        if routine.hasAssembly { return }
        context.begin(routine)
        for pass in Self.defaultOptimizationPasses { _ = pass.rewrite(context) }
    }

    // Moves loop-invariant instructions in front of their loop, so they run once per loop entry
    // instead of once per iteration. Only pure, total and deterministic operations qualify: they
    // read no random state, call no host code, emit nothing, consume no runtime budget and cannot
    // fail. Results are therefore unchanged; executed opcode counts and register requirements are not.
    // Mirrors LoopInvariantHoistingPass of the C# reference compiler item for item.
    func hoistLoopInvariants(_ context: RewriteContext) -> Bool {
        guard hasBackEdge(context) else { return false }
        // Each round moves at least one instruction outward, so this terminates. Only a loop
        // deferred because it contains a rewritten loop can still have candidates afterwards.
        var changed = false
        while true {
            let (moved, deferred) = hoistRound(context)
            changed = changed || moved
            if !deferred { return changed }
        }
    }

    private static let backEdges: Set<GesBytecodeOpCode> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing]

    // Constant loads and arithmetic are side-effect free, never fail (invalid input yields
    // Nothing or a float result), draw no random numbers and charge no runtime budget.
    private static let pureTotalOperations: Set<GesBytecodeOpCode> = [
        .loadNothing, .loadTrue, .loadFalse, .loadInteger, .loadFloat, .loadPercentage, .loadText, .loadTag,
        .add, .subtract, .multiply, .divide, .integerDivide, .modulo, .remainder, .negate, .abs,
    ]

    private func hasBackEdge(_ context: RewriteContext) -> Bool {
        var placed = [Bool](repeating: false, count: context.labelCount)
        for index in 0..<context.count {
            if let label = context.label(at: index) {
                placed[label] = true
            } else if let instruction = context.instruction(at: index), Self.backEdges.contains(instruction.opcode), placed[Int(instruction.word2)] {
                return true
            }
        }
        return false
    }

    // Moving instructions inside one loop changes no position outside it, so one analysis serves
    // every loop of a round that neither contains nor overlaps an already rewritten loop. A loop
    // containing a rewritten one waits for the next round and its fresh analysis.
    private func hoistRound(_ context: RewriteContext) -> (moved: Bool, deferred: Bool) {
        let analysis = LoopAnalysis(context)
        var rewritten: (start: Int, end: Int)?
        var deferred = false
        var moved = false
        for end in 0..<context.count {
            guard let backEdge = context.instruction(at: end), Self.backEdges.contains(backEdge.opcode) else { continue }
            let header = Int(backEdge.word2)
            let start = analysis.labelIndex[header]
            guard start >= 0, start < end else { continue }
            if let rewritten, start <= rewritten.end && rewritten.start <= end {
                deferred = true
                continue
            }
            guard !context.isRoutineEntryLabel(header), analysis.hasSingleEntry(context, start, end) else { continue }
            // One forward sweep finds chains such as a constant load and the product reading it.
            var marked: [Bool] = []
            var hoisted: [Bool] = []
            for index in (start + 1)..<end {
                guard let candidate = context.instruction(at: index), isHoistable(context, candidate, index, start: start, end: end, analysis: analysis, hoisted: hoisted) else { continue }
                if marked.isEmpty {
                    marked = .init(repeating: false, count: context.count)
                    hoisted = .init(repeating: false, count: context.routine.registers)
                }
                marked[index] = true
                hoisted[Int(candidate.word0)] = true
            }
            if marked.isEmpty { continue }
            context.moveMarked(before: start, marked)
            // Loops are visited by ascending end, so later disjoint loops start after this one.
            rewritten = (min(rewritten?.start ?? start, start), end)
            moved = true
        }
        return (moved, deferred)
    }

    private func isHoistable(_ context: RewriteContext, _ instruction: GesBytecodeInstruction, _ index: Int, start: Int, end: Int, analysis: LoopAnalysis, hoisted: [Bool]) -> Bool {
        guard Self.pureTotalOperations.contains(instruction.opcode) else { return false }
        let slots = context.slots(instruction)
        guard slots.registers.contains(0), slots.lists.isEmpty else { return false }
        // The destination has no other definition and is read only later in this loop body,
        // so no path observes it before this instruction or after a zero-iteration loop.
        let destination = Int(instruction.word0)
        guard context.isTemporary(destination), analysis.writeCount[destination] == 1 else { return false }
        if analysis.lastRead[destination] >= 0 && (analysis.firstRead[destination] <= index || analysis.lastRead[destination] > end) { return false }
        // Every input is final before the loop starts, possibly by an earlier hoist of this sweep.
        for slot in slots.registers where slot != 0 {
            let source = Int(instruction.field(slot))
            if source == destination || !(analysis.lastWrite[source] < start || (!hoisted.isEmpty && hoisted[source])) { return false }
        }
        return true
    }
}

// Nested like the C# builder's analysis types.
extension GesBinaryBuilder {
    // Register and register-list operand slots of one opcode shape, computed once per compilation.
    struct OperandSlots {
        let registers: [Int]
        let lists: [Int]

        static func of(_ instruction: GesBytecodeInstruction, _ cache: inout [Int: OperandSlots]) -> OperandSlots {
            let opcode = instruction.opcode
            if let slots = fixed[Int(opcode.rawValue)] { return slots }
            // The remaining shapes also depend on the flags (iterator and send modes) or on payload word a (split and pattern modes).
            let key = Int(opcode.rawValue) << 32 | Int(instruction.unitAndFlags) << 16 | Int(instruction.a)
            if let slots = cache[key] { return slots }
            let slots = OperandSlots(instruction)
            cache[key] = slots
            return slots
        }

        private init(_ instruction: GesBytecodeInstruction) {
            registers = GesCompilerSupport.registerSlots(instruction)
            lists = GesCompilerSupport.listSlots(instruction, text: false)
        }

        // Slots of every opcode whose operands depend on the opcode alone, built once per process.
        private static let fixed: [OperandSlots?] = {
            var table = [OperandSlots?](repeating: nil, count: 256)
            for opcode in GesBytecodeOpCode.allCases where !hasVariableShape(opcode) {
                table[Int(opcode.rawValue)] = OperandSlots(.init(opcode: opcode, unitAndFlags: 0, word0: 0, word1: 0, word2: 0, payload: 0))
            }
            return table
        }()

        private static func hasVariableShape(_ opcode: GesBytecodeOpCode) -> Bool {
            switch opcode {
            case .iteratorCreate, .iteratorCreateOrJump, .iteratorNext, .splitText, .hasPattern, .takePattern, .emitInstant, .emitAfter, .publishInstant, .publishAfter: true
            default: false
            }
        }
    }

    // Per-register definition and use item indexes, label item indexes and the first and last item referring to each
    // label, as the Analysis of the C# LoopInvariantHoistingPass.
    struct LoopAnalysis {
        var writeCount: [Int]
        var lastWrite: [Int]
        var firstRead: [Int]
        var lastRead: [Int]
        var labelIndex: [Int]
        var firstReference: [Int]
        var lastReference: [Int]

        init(_ context: RewriteContext) {
            // Before allocation, routine.registers counts the virtual registers.
            let registers = context.routine.registers
            writeCount = .init(repeating: 0, count: registers)
            lastWrite = .init(repeating: -1, count: registers)
            firstRead = .init(repeating: .max, count: registers)
            lastRead = .init(repeating: -1, count: registers)
            labelIndex = .init(repeating: -1, count: context.labelCount)
            firstReference = .init(repeating: .max, count: context.labelCount)
            lastReference = .init(repeating: -1, count: context.labelCount)
            for index in 0..<context.count {
                if let label = context.label(at: index) {
                    labelIndex[label] = index
                    continue
                }
                guard let instruction = context.instruction(at: index) else { continue }
                if GesBinaryBuilder.isBranch(instruction.opcode) {
                    let label = Int(instruction.word2)
                    firstReference[label] = min(firstReference[label], index)
                    lastReference[label] = index
                }
                let slots = context.slots(instruction)
                for slot in slots.registers {
                    let register = Int(instruction.field(slot))
                    // Closing an iterator also clears its register.
                    if slot == 0 || instruction.opcode == .iteratorClose { write(register, index) }
                    if slot != 0 { read(register, index) }
                }
                for slot in slots.lists {
                    for register in context.builder.lists[Int(instruction.field(slot))] {
                        if slot == 0 { write(Int(register), index) } else { read(Int(register), index) }
                    }
                }
            }
        }

        // The loop body starts at its header label and ends with the back edge. It must be entered
        // only by falling through the header, so code placed before the header runs on every entry.
        func hasSingleEntry(_ context: RewriteContext, _ start: Int, _ end: Int) -> Bool {
            for index in start...end {
                if let label = context.label(at: index), firstReference[label] < start || lastReference[label] > end { return false }
            }
            return true
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
}

extension GesBinaryBuilder {
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
