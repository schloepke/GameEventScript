// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// One bytecode rewrite over a routine before register allocation; returns whether it changed the code.
// Each pass mirrors the C# pass of the same name in GesBinaryBuilderRewriter rule for rule.
protocol GesBinaryOptimizationPass: Sendable {
    func rewrite(_ context: GesBinaryBuilder.RewriteContext) -> Bool
}

// Nested like the C# GesBinaryBuilder types of the same names.
extension GesBinaryBuilder {
    // A routine's plan items and the program-wide effect summary, as C# GesBinaryBuilder.RewriteContext. Passes move
    // and remove items without remapping: jumps address labels, which stay in place when their instructions go.
    // One context serves every routine of a compilation, so its analysis buffers are allocated once.
    final class RewriteContext {
        let builder: GesBinaryBuilder
        let effects: EffectAnalysis
        private(set) var routine: GesRoutine

        init(_ builder: GesBinaryBuilder, _ routine: GesRoutine, _ effects: EffectAnalysis) {
            self.builder = builder
            self.routine = routine
            self.effects = effects
        }

        func begin(_ routine: GesRoutine) { self.routine = routine }

        var count: Int { routine.items.count }

        var labelCount: Int { routine.labelCount }

        // The instruction at an item index, or nil for a label or an index past the end, as C# GetInstruction.
        func instruction(at index: Int) -> GesBytecodeInstruction? { index < count ? routine.items[index].instruction : nil }

        // The label at an item index, as C# GetLabel.
        func label(at index: Int) -> Int? { index < count ? routine.items[index].label : nil }

        // Replaces the instruction at an item index and keeps its source location, as C# ReplaceInstruction.
        func replaceInstruction(_ index: Int, _ instruction: GesBytecodeInstruction) {
            guard case .instruction(_, let location) = routine.items[index] else { preconditionFailure("Only an instruction item can be replaced.") }
            routine.items[index] = .instruction(instruction, location: location)
        }

        func isRoutineEntryLabel(_ label: Int) -> Bool { label == GesRoutine.entryLabel }

        func isTemporary(_ register: Int) -> Bool { !routine.pinned.contains(register) }

        // Register and register-list operand slots, cached per opcode shape for the whole compilation.
        func slots(_ instruction: GesBytecodeInstruction) -> OperandSlots { OperandSlots.of(instruction, &builder.operandLayouts) }

        // Register destination of slot 0, if the instruction writes a single register there.
        func destination(_ instruction: GesBytecodeInstruction) -> Int? {
            slots(instruction).registers.contains(0) ? Int(instruction.word0) : nil
        }

        func forEachRead(_ instruction: GesBytecodeInstruction, _ body: (Int) -> Void) {
            let slots = slots(instruction)
            for slot in slots.registers where slot != 0 { body(Int(instruction.field(slot))) }
            for slot in slots.lists where slot != 0 { for register in builder.lists[Int(instruction.field(slot))] { body(Int(register)) } }
        }

        func readsRegister(_ instruction: GesBytecodeInstruction, _ register: Int) -> Bool {
            var found = false
            forEachRead(instruction) { if $0 == register { found = true } }
            return found
        }

        func forEachWrite(_ instruction: GesBytecodeInstruction, _ body: (Int) -> Void) {
            let slots = slots(instruction)
            if slots.registers.contains(0) { return body(Int(instruction.word0)) }
            if slots.lists.contains(0) { for register in builder.lists[Int(instruction.word0)] { body(Int(register)) } }
        }

        // Buffers reused by every pass over this routine, so repeated analyses do not allocate.
        var reads: [Int] = []
        var writes: [Int] = []
        // Item index of each register's last writer.
        var lastWrite: [Int] = []
        // Item index of the first instruction after each label, by label ID, as C# BuildLabelInstructionIndexes.
        var labelInstructions: [Int] = []
        // Removal marks, sized on the first mark of a pass, so passes that remove nothing allocate nothing.
        private var remove: [Bool] = []
        private var moved: [GesBinaryBuilder.PlanItem] = []

        // Recomputes read and write counts per register and each register's last writer.
        func computeUsage() {
            reset(&reads, 0, routine.registers)
            reset(&writes, 0, routine.registers)
            reset(&lastWrite, -1, routine.registers)
            for index in routine.items.indices {
                guard let instruction = routine.items[index].instruction else { continue }
                forEachWrite(instruction) {
                    writes[$0] += 1
                    lastWrite[$0] = index
                }
                forEachRead(instruction) { reads[$0] += 1 }
            }
        }

        func computeLabelInstructions() {
            reset(&labelInstructions, -1, labelCount)
            var pending: [Int] = []
            for index in routine.items.indices {
                if let label = routine.items[index].label {
                    pending.append(label)
                } else {
                    for label in pending { labelInstructions[label] = index }
                    pending.removeAll(keepingCapacity: true)
                }
            }
        }

        // Item index of the first instruction after `index`, or -1, as C# FindNextInstructionIndex.
        func nextInstruction(after index: Int) -> Int {
            var candidate = index + 1
            while candidate < count {
                if routine.items[candidate].label == nil { return candidate }
                candidate += 1
            }
            return -1
        }

        // Clears the removal marks for the current items.
        func resetRemove() { remove.removeAll(keepingCapacity: true) }

        func isRemoved(_ index: Int) -> Bool { !remove.isEmpty && remove[index] }

        func markRemoved(_ index: Int) {
            if remove.isEmpty { reset(&remove, false, count) }
            remove[index] = true
        }

        private func reset<T>(_ buffer: inout [T], _ value: T, _ count: Int) {
            buffer.removeAll(keepingCapacity: true)
            buffer.append(contentsOf: repeatElement(value, count: count))
        }

        func replacingDestination(_ instruction: GesBytecodeInstruction, _ register: Int) -> GesBytecodeInstruction {
            .init(opcode: instruction.opcode, unitAndFlags: instruction.unitAndFlags, word0: UInt16(register), word1: instruction.word1, word2: instruction.word2, payload: instruction.payload)
        }

        func replacingTarget(_ instruction: GesBytecodeInstruction, _ label: UInt16) -> GesBytecodeInstruction {
            .init(opcode: instruction.opcode, unitAndFlags: instruction.unitAndFlags, word0: instruction.word0, word1: instruction.word1, word2: label, payload: instruction.payload)
        }

        // Removes the marked items in place, as C# RemoveMarked. Labels of removed instructions stay, so no jump changes.
        func removeMarked() {
            // A pass can change code without removing any of it, such as threading a jump.
            if remove.isEmpty { return }
            var write = 0
            for index in 0..<count where !remove[index] {
                if write != index { routine.items[write] = routine.items[index] }
                write += 1
            }
            routine.items.removeLast(count - write)
        }

        // Moves the marked items, in their order, in front of item `target`; every marked index is after `target`, as
        // C# MoveMarkedBefore.
        func moveMarked(before target: Int, _ marked: [Bool]) {
            moved.removeAll(keepingCapacity: true)
            var write = target
            for read in target..<count {
                if marked[read] {
                    moved.append(routine.items[read])
                } else {
                    routine.items[write] = routine.items[read]
                    write += 1
                }
            }
            var index = count - 1
            while index >= target + moved.count {
                routine.items[index] = routine.items[index - moved.count]
                index -= 1
            }
            for (offset, item) in moved.enumerated() { routine.items[target + offset] = item }
        }
    }

    // Program-wide summary of which routines may affect anything outside their frame, as GesBinaryBuilder.EffectAnalysis.
    // Random consumption, allocation and runtime budgets do not make an operation effectful.
    struct EffectAnalysis {
        private var routineEffects: [String: Bool] = [:]
        private(set) var recordKeys: [String: String] = [:]
        private(set) var recordKeysByID: [Int: String] = [:]
        private let strings: [String]

        init(_ builder: GesBinaryBuilder) {
            strings = builder.strings
            // With assembly present, optimization runs per routine. Calls into other routines stay conservative.
            let preserved = builder.routines.contains { $0.hasAssembly }
            for routine in builder.routines where routine.definition.kind == "record" {
                recordKeys[routine.definition.name] = routine.key
                if let id = builder.recordIDs[routine.definition.name] { recordKeysByID[id] = routine.key }
            }
            // Only record routines are resolved callees; without records every routine call stays effectful.
            if recordKeys.isEmpty { return }
            var callers: [String: [String]] = [:]
            var pending: [String: Int] = [:]
            for routine in builder.routines {
                routineEffects[routine.key] = preserved
                pending[routine.key] = 0
            }
            for routine in builder.routines {
                for item in routine.items {
                    guard let instruction = item.instruction else { continue }
                    guard Self.isRoutineCall(instruction.opcode) else {
                        if Self.hasDirectEffects(instruction) { routineEffects[routine.key] = true }
                        continue
                    }
                    guard let callee = callee(instruction, instruction.opcode == .call ? routine.calls[Int(instruction.word2)] : nil), routineEffects[callee] != nil else {
                        // Unknown targets must never be assumed pure.
                        routineEffects[routine.key] = true
                        continue
                    }
                    callers[callee, default: []].append(routine.key)
                    pending[routine.key, default: 0] += 1
                }
            }
            // Bottom-up propagation in C# routine order; cyclic or cycle-dependent calls stay effectful.
            var ready = builder.routines.map(\.key).filter { pending[$0] == 0 }
            var cursor = 0
            while cursor < ready.count {
                let callee = ready[cursor]
                cursor += 1
                for caller in callers[callee] ?? [] {
                    if routineEffects[callee] == true { routineEffects[caller] = true }
                    pending[caller]! -= 1
                    if pending[caller] == 0 { ready.append(caller) }
                }
            }
            for (key, count) in pending where count > 0 { routineEffects[key] = true }
        }

        func hasEffects(_ instruction: GesBytecodeInstruction, call: String?) -> Bool {
            guard Self.isRoutineCall(instruction.opcode) else { return Self.hasDirectEffects(instruction) }
            guard let callee = callee(instruction, call), let effects = routineEffects[callee] else { return true }
            return effects
        }

        private func callee(_ instruction: GesBytecodeInstruction, _ call: String?) -> String? {
            switch instruction.opcode {
            case .castCustom: return Int(instruction.word2) < strings.count ? recordKeys[strings[Int(instruction.word2)]] : nil
            case .call: return call
            default: return recordKeysByID[Int(instruction.word1)]
            }
        }

        static func isRoutineCall(_ opcode: GesBytecodeOpCode) -> Bool { opcode == .call || opcode == .createRecord || opcode == .castCustom }

        static func hasDirectEffects(_ instruction: GesBytecodeInstruction) -> Bool {
            // Dynamic external-field access can call host code. Without a purity contract it is conservative, like an extension call.
            switch instruction.opcode {
            case .memberAccess, .propertyAccess, .keysOfMap, .valuesOfMap, .entriesOfMap: return true
            case .cast: return GesBytecodeTypeKind(rawValue: instruction.word2) == .map
            default: break
            }
            if canDiscardResult(instruction.opcode) { return false }
            switch instruction.opcode {
            // These operations affect only the current frame, its control flow, argument staging or private builders.
            case .nop, .registerLocals, .jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .returnVoid, .returnValue,
                .stageRegister, .stageNothing, .stageTrue, .stageFalse, .stageInteger, .stageFloat, .stageText, .stageTag, .stagePercentage,
                .createVector, .createPoint, .createList, .createMap, .createRecordValue, .randomPush, .randomPushConstant, .randomPop,
                .iteratorCreateOrJump, .iteratorNext, .iteratorClose, .listBuilderCreate, .listBuilderAdd, .listBuilderFinish,
                .mapBuilderCreate, .mapBuilderAdd, .mapBuilderFinish, .distinctBuilderCreate, .distinctBuilderAdd, .distinctBuilderFinish,
                .groupBuilderCreate, .groupBuilderAdd, .groupBuilderFinish, .orderBuilderCreate, .orderBuilderAdd, .orderBuilderFinishAscending, .orderBuilderFinishDescending:
                return false
            // Emit/Publish, extension calls, external constructors and future unclassified operations may have outside effects.
            default: return true
            }
        }

        static func canDiscardResult(_ opcode: GesBytecodeOpCode) -> Bool {
            switch opcode {
            case .castCustom, .randomTake, .randomTakeFloat, .chance, .createDice, .oneRandom, .takeRandom, .oneWeighted, .takeWeighted, .shuffle,
                .cast, .castUnit, .castNumeric, .checkType, .checkCustomType, .checkUnit, .checkNumeric, .checkInteger, .checkFractional, .move,
                .memberAccess, .indexAccess, .propertyAccess, .bindHandler, .loadNothing, .loadTrue, .loadFalse, .loadInteger, .loadFloat,
                .loadPercentage, .loadText, .loadTag, .loadHandler, .loadMessage, .createSeries, .createRange, .createRangeWithStep,
                .createRangeIterator, .createRangeIteratorWithStep, .createRangeIteratorShort, .hasValue, .isEmpty, .default, .or, .and, .xor,
                .implies, .not, .equal, .notEqual, .less, .greater, .lessOrEqual, .greaterOrEqual, .add, .subtract, .multiply, .divide, .power,
                .integerDivide, .modulo, .remainder, .min, .max, .negate, .abs, .logN, .clamp, .term, .exp, .floor, .ceil, .truncate,
                .roundHalfEven, .roundHalfUp, .roundHalfDown, .degreeToRadians, .degreeFromRadians, .wrapDegree, .sin, .cos, .tan, .asin, .acos,
                .atan, .atan2, .hypot2D, .hypot3D, .distance, .distance2D, .distance3D, .distanceSquared, .distanceSquared2D, .distanceSquared3D,
                .lengthSquared, .lengthSquared2D, .lengthSquared3D, .normalize, .normalize2D, .normalize3D, .dot, .dot2D, .dot3D, .cross, .cross2D,
                .cross3D, .angleBetween, .angleBetween2D, .angleBetween3D, .takeFirst, .dropFirst, .takeLast, .dropLast, .takeHighest, .takeLowest,
                .dropHighest, .dropLowest, .count, .startsWith, .endsWith, .contains, .containsAny, .containsAll, .hasAny, .hasAll, .containsValue,
                .union, .intersect, .zip, .keysOfMap, .valuesOfMap, .entriesOfMap, .first, .last, .single, .iteratorCreate, .distinct,
                .sortAscending, .sortDescending, .reverse, .hasPattern, .takePattern:
                return true
            default: return false
            }
        }
    }

    // Folds a unary or binary operation whose inputs are single-use temporary constants loaded earlier in the same block.
    struct LocalConstantFoldingPass: GesBinaryOptimizationPass {
        static let unary: Set<GesBytecodeOpCode> = [.not, .hasValue, .negate, .abs]
        static let binary: Set<GesBytecodeOpCode> = [.equal, .notEqual, .less, .greater, .lessOrEqual, .greaterOrEqual, .add, .subtract, .multiply, .integerDivide, .modulo, .and, .or, .xor, .implies]

        enum Constant: Equatable {
            case nothing
            case boolean(Bool)
            case integer(Int64)
        }

        func rewrite(_ context: RewriteContext) -> Bool {
            context.computeUsage()
            var changed = false
            for index in 0..<context.count {
                guard let instruction = context.instruction(at: index), context.destination(instruction) != nil else { continue }
                if let replacement = foldUnary(context, index, instruction) ?? foldBinary(context, index, instruction) {
                    context.replaceInstruction(index, replacement)
                    changed = true
                }
            }
            return changed
        }

        private func foldUnary(_ context: RewriteContext, _ index: Int, _ instruction: GesBytecodeInstruction) -> GesBytecodeInstruction? {
            guard Self.unary.contains(instruction.opcode), let value = constant(context, index, Int(instruction.word1)) else { return nil }
            switch (instruction.opcode, value) {
            case (.not, .boolean(let flag)): return boolean(instruction, !flag)
            case (.hasValue, _): return boolean(instruction, value != .nothing)
            case (.negate, .integer(let value)) where value != .min: return integer(instruction, -value)
            case (.abs, .integer(let value)) where value != .min: return integer(instruction, value < 0 ? -value : value)
            default: return nil
            }
        }

        private func foldBinary(_ context: RewriteContext, _ index: Int, _ instruction: GesBytecodeInstruction) -> GesBytecodeInstruction? {
            guard Self.binary.contains(instruction.opcode), let left = constant(context, index, Int(instruction.word1)), let right = constant(context, index, Int(instruction.word2)) else { return nil }
            if case .integer(let a) = left, case .integer(let b) = right {
                switch instruction.opcode {
                case .equal: return boolean(instruction, a == b)
                case .notEqual: return boolean(instruction, a != b)
                case .less: return boolean(instruction, a < b)
                case .greater: return boolean(instruction, a > b)
                case .lessOrEqual: return boolean(instruction, a <= b)
                case .greaterOrEqual: return boolean(instruction, a >= b)
                case .add:
                    let (sum, overflow) = a.addingReportingOverflow(b)
                    return overflow ? nil : integer(instruction, sum)
                case .subtract:
                    let (difference, overflow) = a.subtractingReportingOverflow(b)
                    return overflow ? nil : integer(instruction, difference)
                case .multiply:
                    let (product, overflow) = a.multipliedReportingOverflow(by: b)
                    return overflow ? nil : integer(instruction, product)
                // Truncating division, exactly as the C# pass computes it.
                case .integerDivide where b != 0 && !(a == .min && b == -1): return integer(instruction, a / b)
                case .modulo where b != 0 && !(a == .min && b == -1):
                    var modulo = a % b
                    if modulo != 0 && (modulo < 0 && b > 0 || modulo > 0 && b < 0) { modulo += b }
                    return integer(instruction, modulo)
                default: break
                }
            }
            if case .boolean(let a) = left, case .boolean(let b) = right {
                switch instruction.opcode {
                case .equal: return boolean(instruction, a == b)
                case .notEqual: return boolean(instruction, a != b)
                case .and: return boolean(instruction, a && b)
                case .or: return boolean(instruction, a || b)
                case .xor: return boolean(instruction, a != b)
                case .implies: return boolean(instruction, !a || b)
                default: break
                }
            }
            if left == .nothing || right == .nothing {
                switch instruction.opcode {
                case .equal: return boolean(instruction, left == .nothing && right == .nothing)
                case .notEqual: return boolean(instruction, left != .nothing || right != .nothing)
                default: break
                }
            }
            return nil
        }

        private func constant(_ context: RewriteContext, _ consumer: Int, _ register: Int) -> Constant? {
            guard register < context.reads.count, context.isTemporary(register), context.reads[register] == 1, context.writes[register] == 1 else { return nil }
            let writer = context.lastWrite[register]
            // A label between writer and consumer ends the basic block.
            guard writer >= 0, writer < consumer, !((writer + 1)..<consumer).contains(where: { context.label(at: $0) != nil }) else { return nil }
            guard let instruction = context.instruction(at: writer), context.destination(instruction) == register else { return nil }
            switch instruction.opcode {
            case .loadInteger where instruction.unitAndFlags == 0: return .integer(Int64(bitPattern: instruction.payload))
            case .loadTrue: return .boolean(true)
            case .loadFalse: return .boolean(false)
            case .loadNothing: return .nothing
            default: return nil
            }
        }

        private func integer(_ source: GesBytecodeInstruction, _ value: Int64) -> GesBytecodeInstruction {
            .init(opcode: .loadInteger, unitAndFlags: 0, word0: source.word0, word1: 0, word2: 0, payload: UInt64(bitPattern: value))
        }

        private func boolean(_ source: GesBytecodeInstruction, _ value: Bool) -> GesBytecodeInstruction {
            .init(opcode: value ? .loadTrue : .loadFalse, unitAndFlags: 0, word0: source.word0, word1: 0, word2: 0, payload: 0)
        }
    }

    // Removes writes to temporaries that are never read, when the operation has no effect beyond its result.
    struct DeadTempWriteEliminationPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool {
            var changed = false
            while true {
                context.computeUsage()
                context.resetRemove()
                var any = false
                for index in 0..<context.count {
                    guard let instruction = context.instruction(at: index), let register = context.destination(instruction), context.isTemporary(register), context.reads[register] == 0, EffectAnalysis.canDiscardResult(instruction.opcode) else {
                        continue
                    }
                    // CastCustom resolves its record routine by name; its call entry is irrelevant.
                    let effects = instruction.opcode == .castCustom ? context.effects.hasEffects(instruction, call: nil) : EffectAnalysis.hasDirectEffects(instruction)
                    if effects { continue }
                    context.markRemoved(index)
                    any = true
                }
                if !any { return changed }
                context.removeMarked()
                changed = true
            }
        }
    }

    // Writes a single-use temporary's value directly into the register a later move copies it to.
    struct RegisterPressureReductionPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool {
            context.computeUsage()
            context.resetRemove()
            var changed = false
            for moveIndex in 0..<context.count {
                guard let move = context.instruction(at: moveIndex), move.opcode == .move, move.unitAndFlags == 0 else { continue }
                let temporary = Int(move.word1)
                let destination = Int(move.word0)
                guard context.isTemporary(temporary), context.reads[temporary] == 1, context.writes[temporary] == 1 else { continue }
                let writerIndex = context.lastWrite[temporary]
                guard writerIndex >= 0, writerIndex < moveIndex else { continue }
                guard let writer = context.instruction(at: writerIndex), context.destination(writer) == temporary, !context.readsRegister(writer, destination), Self.canRetargetDestination(writer.opcode),
                    canMoveWriteEarlier(context, writerIndex, moveIndex, destination)
                else { continue }
                context.replaceInstruction(writerIndex, context.replacingDestination(writer, destination))
                context.markRemoved(moveIndex)
                changed = true
            }
            if changed { context.removeMarked() }
            return changed
        }

        private func canMoveWriteEarlier(_ context: RewriteContext, _ writer: Int, _ move: Int, _ destination: Int) -> Bool {
            for index in (writer + 1)..<move {
                guard let instruction = context.instruction(at: index) else { return false }
                if Self.controlFlowBoundaries.contains(instruction.opcode) || context.readsRegister(instruction, destination) || context.destination(instruction) == destination { return false }
            }
            return true
        }

        static let controlFlowBoundaries: Set<GesBytecodeOpCode> = [
            .jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .returnVoid, .returnValue, .randomPush, .randomPushConstant, .randomPop, .iteratorCreateOrJump, .iteratorNext,
        ]

        static func canRetargetDestination(_ opcode: GesBytecodeOpCode) -> Bool {
            switch opcode {
            case .registerLocals, .jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing, .call, .callExternal, .returnVoid, .returnValue,
                .emitMessage, .emitMessageWithTags, .emitMessageValue, .emitMessageValueWithTags, .publishMessage, .publishMessageWithTags,
                .publishMessageValue, .publishMessageValueWithTags, .stageRegister, .stageNothing, .stageTrue, .stageFalse, .stageInteger, .stageFloat,
                .stageText, .stageTag, .stagePercentage, .createRecord, .randomPush, .randomPushConstant, .randomPop, .iteratorCreateOrJump, .iteratorNext,
                .iteratorClose, .listBuilderAdd, .mapBuilderAdd, .distinctBuilderAdd, .groupBuilderAdd, .orderBuilderAdd:
                return false
            default: return true
            }
        }
    }

    // Writes an instruction's result directly into the register an immediately following move copies it to.
    struct PeepholeMoveEliminationPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool {
            context.computeUsage()
            context.resetRemove()
            var changed = false
            var index = 0
            while index + 1 < context.count {
                defer { index += 1 }
                guard !context.isRemoved(index), !context.isRemoved(index + 1), let instruction = context.instruction(at: index), let move = context.instruction(at: index + 1), instruction.opcode != .iteratorNext,
                    instruction.opcode != .iteratorCreateOrJump, move.opcode == .move, move.unitAndFlags == 0,
                    let temporary = context.destination(instruction), temporary == Int(move.word1), context.isTemporary(temporary),
                    !context.readsRegister(instruction, Int(move.word0)), context.reads[temporary] == 1, context.writes[temporary] == 1
                else { continue }
                context.replaceInstruction(index, context.replacingDestination(instruction, Int(move.word0)))
                context.markRemoved(index + 1)
                context.reads[temporary] -= 1
                context.writes[temporary] -= 1
                changed = true
            }
            if changed { context.removeMarked() }
            return changed
        }
    }

    // Folds a conditional skip over an unconditional jump into the inverse conditional jump.
    struct BranchSimplificationPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool {
            var changed = false
            context.computeLabelInstructions()
            context.resetRemove()
            for index in 0..<context.count {
                guard let branch = context.instruction(at: index) else { continue }
                let inverse: GesBytecodeOpCode? = branch.opcode == .jumpIfTrue ? .jumpIfFalse : branch.opcode == .jumpIfFalse || branch.opcode == .jumpIfNotTrue ? .jumpIfTrue : nil
                // The jump must follow directly; a label between them keeps the jump an entry point.
                guard let inverse, let jump = context.instruction(at: index + 1), jump.opcode == .jump,
                    context.labelInstructions[Int(branch.word2)] == context.nextInstruction(after: index + 1)
                else { continue }
                context.replaceInstruction(index, .init(opcode: inverse, unitAndFlags: branch.unitAndFlags, word0: branch.word0, word1: branch.word1, word2: jump.word2, payload: branch.payload))
                context.markRemoved(index + 1)
                changed = true
            }
            if changed { context.removeMarked() }
            return changed
        }
    }

    // Removes jumps to the next instruction, threads jumps to jumps one step and drops unreachable code after a stop.
    struct JumpCleanupPass: GesBinaryOptimizationPass {
        static let jumps: Set<GesBytecodeOpCode> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing]
        static let stops: Set<GesBytecodeOpCode> = [.jump, .returnVoid, .returnValue]

        func rewrite(_ context: RewriteContext) -> Bool {
            var changed = false
            context.computeLabelInstructions()
            context.resetRemove()
            for index in 0..<context.count {
                guard let instruction = context.instruction(at: index) else { continue }
                if Self.jumps.contains(instruction.opcode) {
                    let target = context.labelInstructions[Int(instruction.word2)]
                    if target == context.nextInstruction(after: index) {
                        context.markRemoved(index)
                        changed = true
                        continue
                    }
                    if target >= 0, let targetJump = context.instruction(at: target), targetJump.opcode == .jump, targetJump.word2 != instruction.word2 {
                        context.replaceInstruction(index, context.replacingTarget(instruction, targetJump.word2))
                        changed = true
                    }
                }
                if Self.stops.contains(instruction.opcode) {
                    var dead = index + 1
                    while dead < context.count && context.label(at: dead) == nil {
                        context.markRemoved(dead)
                        changed = true
                        dead += 1
                    }
                }
            }
            if changed { context.removeMarked() }
            return changed
        }
    }

    struct LoopInvariantHoistingPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool { context.builder.hoistLoopInvariants(context) }
    }
}
