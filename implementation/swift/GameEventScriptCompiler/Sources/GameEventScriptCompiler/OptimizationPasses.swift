// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// One bytecode rewrite over a routine before register allocation; returns whether it changed the code.
// Each pass mirrors the C# pass of the same name in GesBinaryBuilderRewriter rule for rule.
protocol GesBinaryOptimizationPass: Sendable {
    func rewrite(_ context: GesCompiler.RewriteContext) -> Bool
}

// Nested like the C# GesBinaryBuilder types of the same names.
extension GesCompiler {
    // A routine's virtual-register code, its persistent labels and the program-wide effect summary.
    final class RewriteContext {
        let compiler: GesCompiler
        let routine: GesRoutine
        let effects: EffectAnalysis

        init(_ compiler: GesCompiler, _ routine: GesRoutine, _ effects: EffectAnalysis) {
            self.compiler = compiler
            self.routine = routine
            self.effects = effects
            // The routine entry label, every label marked while lowering and every branch target, as in the C# builder.
            routine.labels.insert(0)
            for instruction in routine.code where GesCompiler.isBranch(instruction.opcode) { routine.labels.insert(Int(instruction.word2)) }
        }

        var count: Int { routine.code.count }

        subscript(index: Int) -> GesBytecodeInstruction {
            get { routine.code[index] }
            set { routine.code[index] = newValue }
        }

        func isLabel(_ position: Int) -> Bool { routine.labels.contains(position) }

        func isTemporary(_ register: Int) -> Bool { !routine.pinned.contains(register) }

        // Register destination of slot 0, if the instruction writes a single register there.
        func destination(_ instruction: GesBytecodeInstruction) -> Int? {
            GesCompilerSupport.registerSlots(instruction).contains(0) ? Int(instruction.word0) : nil
        }

        func reads(_ instruction: GesBytecodeInstruction) -> [Int] {
            var result = GesCompilerSupport.registerSlots(instruction).filter { $0 != 0 }.map { Int(instruction.field($0)) }
            for slot in GesCompilerSupport.listSlots(instruction, text: false) where slot != 0 { result += compiler.lists[Int(instruction.field(slot))].map(Int.init) }
            return result
        }

        func writes(_ instruction: GesBytecodeInstruction) -> [Int] {
            if let register = destination(instruction) { return [register] }
            return GesCompilerSupport.listSlots(instruction, text: false).contains(0) ? compiler.lists[Int(instruction.word0)].map(Int.init) : []
        }

        // Read and write counts per register, and each register's last writer.
        func usage() -> (reads: [Int], writes: [Int], lastWrite: [Int]) {
            var reads = [Int](repeating: 0, count: routine.registers)
            var writes = [Int](repeating: 0, count: routine.registers)
            var lastWrite = [Int](repeating: -1, count: routine.registers)
            for (index, instruction) in routine.code.enumerated() {
                for register in self.writes(instruction) {
                    writes[register] += 1
                    lastWrite[register] = index
                }
                for register in self.reads(instruction) { reads[register] += 1 }
            }
            return (reads, writes, lastWrite)
        }

        func replacingDestination(_ instruction: GesBytecodeInstruction, _ register: Int) -> GesBytecodeInstruction {
            var fields = instruction.fields
            fields[0] = UInt16(register)
            return instruction.replacing(fields)
        }

        // Removes the marked instructions. A label or position that addressed a removed instruction now
        // addresses the next remaining one, as a C# label item in front of it would.
        func removeMarked(_ remove: [Bool]) {
            var addresses = [Int](repeating: 0, count: count + 1)
            var next = 0
            for index in 0..<count {
                addresses[index] = next
                if !remove[index] { next += 1 }
            }
            addresses[count] = next
            var code: [GesBytecodeInstruction] = []
            var locations: [GesSourceLocation] = []
            for index in 0..<count where !remove[index] {
                var instruction = routine.code[index]
                if GesCompiler.isBranch(instruction.opcode) {
                    var fields = instruction.fields
                    fields[2] = UInt16(addresses[Int(instruction.word2)])
                    instruction = instruction.replacing(fields)
                }
                code.append(instruction)
                locations.append(routine.locations[index])
            }
            routine.code = code
            routine.locations = locations
            routine.calls = routine.calls.map { (addresses[$0.0], $0.1) }
            routine.symbols = routine.symbols.map { ($0.0, $0.1, addresses[$0.2], $0.3) }
            routine.labels = Set(routine.labels.map { addresses[$0] })
        }
    }

    // Program-wide summary of which routines may affect anything outside their frame, as GesBinaryBuilder.EffectAnalysis.
    // Random consumption, allocation and runtime budgets do not make an operation effectful.
    struct EffectAnalysis {
        private var routineEffects: [String: Bool] = [:]
        private var recordKeys: [String: String] = [:]
        private var recordKeysByID: [Int: String] = [:]
        private let strings: [String]

        init(_ compiler: GesCompiler) {
            strings = compiler.strings
            // With assembly present, optimization runs per routine. Calls into other routines stay conservative.
            let preserved = compiler.routines.contains { $0.hasAssembly }
            for routine in compiler.routines where routine.definition.kind == "record" {
                recordKeys[routine.definition.name] = routine.key
                if let id = compiler.recordIDs[routine.definition.name] { recordKeysByID[id] = routine.key }
            }
            var callers: [String: [String]] = [:]
            var pending: [String: Int] = [:]
            for routine in compiler.routines {
                routineEffects[routine.key] = preserved
                pending[routine.key] = 0
            }
            for routine in compiler.routines {
                let calls = Dictionary(routine.calls.map { ($0.0, $0.1) }, uniquingKeysWith: { first, _ in first })
                for (index, instruction) in routine.code.enumerated() {
                    guard Self.isRoutineCall(instruction.opcode) else {
                        if Self.hasDirectEffects(instruction) { routineEffects[routine.key] = true }
                        continue
                    }
                    guard let callee = callee(instruction, calls[index]), routineEffects[callee] != nil else {
                        // Unknown targets must never be assumed pure.
                        routineEffects[routine.key] = true
                        continue
                    }
                    callers[callee, default: []].append(routine.key)
                    pending[routine.key, default: 0] += 1
                }
            }
            // Bottom-up propagation in C# routine order; cyclic or cycle-dependent calls stay effectful.
            var ready = compiler.routines.map(\.key).filter { pending[$0] == 0 }
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
        enum Constant: Equatable {
            case nothing
            case boolean(Bool)
            case integer(Int64)
        }

        func rewrite(_ context: RewriteContext) -> Bool {
            let usage = context.usage()
            var changed = false
            for index in 0..<context.count {
                let instruction = context[index]
                guard context.destination(instruction) != nil else { continue }
                if let replacement = foldUnary(context, index, instruction, usage) ?? foldBinary(context, index, instruction, usage) {
                    context[index] = replacement
                    changed = true
                }
            }
            return changed
        }

        private func foldUnary(_ context: RewriteContext, _ index: Int, _ instruction: GesBytecodeInstruction, _ usage: (reads: [Int], writes: [Int], lastWrite: [Int])) -> GesBytecodeInstruction? {
            guard [.not, .hasValue, .negate, .abs].contains(instruction.opcode), let value = constant(context, index, Int(instruction.word1), usage) else { return nil }
            switch (instruction.opcode, value) {
            case (.not, .boolean(let flag)): return boolean(instruction, !flag)
            case (.hasValue, _): return boolean(instruction, value != .nothing)
            case (.negate, .integer(let value)) where value != .min: return integer(instruction, -value)
            case (.abs, .integer(let value)) where value != .min: return integer(instruction, value < 0 ? -value : value)
            default: return nil
            }
        }

        private func foldBinary(_ context: RewriteContext, _ index: Int, _ instruction: GesBytecodeInstruction, _ usage: (reads: [Int], writes: [Int], lastWrite: [Int])) -> GesBytecodeInstruction? {
            let binary: Set<GesBytecodeOpCode> = [.equal, .notEqual, .less, .greater, .lessOrEqual, .greaterOrEqual, .add, .subtract, .multiply, .integerDivide, .modulo, .and, .or, .xor, .implies]
            guard binary.contains(instruction.opcode), let left = constant(context, index, Int(instruction.word1), usage), let right = constant(context, index, Int(instruction.word2), usage) else { return nil }
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

        private func constant(_ context: RewriteContext, _ consumer: Int, _ register: Int, _ usage: (reads: [Int], writes: [Int], lastWrite: [Int])) -> Constant? {
            guard register < usage.reads.count, context.isTemporary(register), usage.reads[register] == 1, usage.writes[register] == 1 else { return nil }
            let writer = usage.lastWrite[register]
            // A label item between writer and consumer, including one right before the consumer, ends the block.
            guard writer >= 0, writer < consumer, !((writer + 1)...consumer).contains(where: context.isLabel) else { return nil }
            let instruction = context[writer]
            guard context.destination(instruction) == register else { return nil }
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
                let reads = context.usage().reads
                let calls = Dictionary(context.routine.calls.map { ($0.0, $0.1) }, uniquingKeysWith: { first, _ in first })
                var remove = [Bool](repeating: false, count: context.count)
                var any = false
                for index in 0..<context.count {
                    let instruction = context[index]
                    guard let register = context.destination(instruction), context.isTemporary(register), reads[register] == 0, EffectAnalysis.canDiscardResult(instruction.opcode) else { continue }
                    let effects = instruction.opcode == .castCustom ? context.effects.hasEffects(instruction, call: calls[index]) : EffectAnalysis.hasDirectEffects(instruction)
                    if effects { continue }
                    remove[index] = true
                    any = true
                }
                if !any { return changed }
                context.removeMarked(remove)
                changed = true
            }
        }
    }

    // Writes a single-use temporary's value directly into the register a later move copies it to.
    struct RegisterPressureReductionPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool {
            let usage = context.usage()
            var changed = false
            var remove = [Bool](repeating: false, count: context.count)
            for moveIndex in 0..<context.count {
                let move = context[moveIndex]
                guard move.opcode == .move, move.unitAndFlags == 0 else { continue }
                let temporary = Int(move.word1)
                let destination = Int(move.word0)
                guard context.isTemporary(temporary), usage.reads[temporary] == 1, usage.writes[temporary] == 1 else { continue }
                let writerIndex = usage.lastWrite[temporary]
                guard writerIndex >= 0, writerIndex < moveIndex else { continue }
                let writer = context[writerIndex]
                guard context.destination(writer) == temporary, !context.reads(writer).contains(destination), Self.canRetargetDestination(writer.opcode),
                    canMoveWriteEarlier(context, writerIndex, moveIndex, destination)
                else { continue }
                context[writerIndex] = context.replacingDestination(writer, destination)
                remove[moveIndex] = true
                changed = true
            }
            if changed { context.removeMarked(remove) }
            return changed
        }

        private func canMoveWriteEarlier(_ context: RewriteContext, _ writer: Int, _ move: Int, _ destination: Int) -> Bool {
            if context.isLabel(move) { return false }
            for index in (writer + 1)..<move {
                if context.isLabel(index) { return false }
                let instruction = context[index]
                if Self.controlFlowBoundaries.contains(instruction.opcode) || context.reads(instruction).contains(destination) || context.destination(instruction) == destination { return false }
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
            var usage = context.usage()
            var changed = false
            var remove = [Bool](repeating: false, count: context.count)
            var index = 0
            while index + 1 < context.count {
                defer { index += 1 }
                let instruction = context[index]
                let move = context[index + 1]
                guard !remove[index], !remove[index + 1], !context.isLabel(index + 1), instruction.opcode != .iteratorNext, instruction.opcode != .iteratorCreateOrJump, move.opcode == .move, move.unitAndFlags == 0,
                    let temporary = context.destination(instruction), temporary == Int(move.word1), context.isTemporary(temporary),
                    !context.reads(instruction).contains(Int(move.word0)), usage.reads[temporary] == 1, usage.writes[temporary] == 1
                else { continue }
                context[index] = context.replacingDestination(instruction, Int(move.word0))
                remove[index + 1] = true
                usage.reads[temporary] -= 1
                usage.writes[temporary] -= 1
                changed = true
            }
            if changed { context.removeMarked(remove) }
            return changed
        }
    }

    // Folds a conditional skip over an unconditional jump into the inverse conditional jump.
    struct BranchSimplificationPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool {
            var changed = false
            var remove = [Bool](repeating: false, count: context.count)
            for index in 0..<context.count where index + 1 < context.count {
                let branch = context[index]
                let inverse: GesBytecodeOpCode? = branch.opcode == .jumpIfTrue ? .jumpIfFalse : branch.opcode == .jumpIfFalse || branch.opcode == .jumpIfNotTrue ? .jumpIfTrue : nil
                // A label between the branch and the jump keeps the jump an entry point.
                guard let inverse, !context.isLabel(index + 1), context[index + 1].opcode == .jump, Int(branch.word2) == index + 2 else { continue }
                let jump = context[index + 1]
                context[index] = .init(opcode: inverse, unitAndFlags: branch.unitAndFlags, word0: branch.word0, word1: branch.word1, word2: jump.word2, payload: branch.payload)
                remove[index + 1] = true
                changed = true
            }
            if changed { context.removeMarked(remove) }
            return changed
        }
    }

    // Removes jumps to the next instruction, threads jumps to jumps one step and drops unreachable code after a stop.
    struct JumpCleanupPass: GesBinaryOptimizationPass {
        static let jumps: Set<GesBytecodeOpCode> = [.jump, .jumpIfTrue, .jumpIfFalse, .jumpIfNotTrue, .jumpIfNothing]

        func rewrite(_ context: RewriteContext) -> Bool {
            var changed = false
            var remove = [Bool](repeating: false, count: context.count)
            for index in 0..<context.count {
                let instruction = context[index]
                if Self.jumps.contains(instruction.opcode) {
                    let target = Int(instruction.word2)
                    if target == index + 1 {
                        remove[index] = true
                        changed = true
                        continue
                    }
                    if target < context.count, context[target].opcode == .jump, Int(context[target].word2) != target {
                        var fields = instruction.fields
                        fields[2] = context[target].word2
                        context[index] = instruction.replacing(fields)
                        changed = true
                    }
                }
                if [.jump, .returnVoid, .returnValue].contains(instruction.opcode) {
                    var dead = index + 1
                    while dead < context.count && !context.isLabel(dead) {
                        remove[dead] = true
                        changed = true
                        dead += 1
                    }
                }
            }
            if changed { context.removeMarked(remove) }
            return changed
        }
    }

    struct LoopInvariantHoistingPass: GesBinaryOptimizationPass {
        func rewrite(_ context: RewriteContext) -> Bool {
            let before = context.routine.code
            context.compiler.hoistLoopInvariants(context.routine)
            return context.routine.code != before
        }
    }
}
