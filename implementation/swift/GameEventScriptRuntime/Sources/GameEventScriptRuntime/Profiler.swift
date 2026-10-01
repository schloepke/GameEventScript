// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Optional synchronous instrumentation factory. Callbacks must not reenter a host. Timing, allocation and report
/// policies belong to the embedding. Instances require serialized access.
public protocol GameEventScriptProfiler: AnyObject {
    /// Creates independent instrumentation for one loaded Program instance, before execution. The host retains it
    /// until the instance is released; the factory may retain results. Duplicate module names remain distinct instances.
    func createProgramProfiler(_ program: GameEventScriptProgram) -> any GameEventScriptProgramProfiler
}

/// Instruction-boundary callbacks for one loaded instance. Callbacks must not pump a host or modify execution.
/// No callback is made while the host waits between slices.
public protocol GameEventScriptProgramProfiler: AnyObject {
    /// Called before each fetched instruction executes, including one that faults. Closes Fetch and starts Execute
    /// for this zero-based address before any jump or call. Pre-execution phases belong to this instruction.
    func instructionStarting(_ address: Int)

    /// Starts a loop phase, closing the preceding interval. Execute starts through instructionStarting.
    /// Checks before an instruction may terminate the loop without another instruction start.
    func phaseStarting(_ phase: GameEventScriptProfilePhase)

    /// Closes the last interval on completion, pause, limit or failure; may be called without a preceding instruction.
    /// Clear pending timing so time outside the VM is never charged to the next slice.
    func finishSlice()
}

/// Non-overlapping regions of an instrumented VM loop; timing policies belong to the embedding.
public enum GameEventScriptProfilePhase: Int, CaseIterable {
    /// Checks whether the VM remains processing.
    case stateCheck
    /// Checks whether the runtime budget is exhausted.
    case budgetCheck
    /// Checks the reserved per-slice instruction count.
    case sliceCheck
    /// Validates the instruction pointer, fetches code and increments the pointer.
    case fetch
    /// Dispatches and executes one opcode, including synchronous callbacks and fault handling.
    case execute
    /// Advances the completed instruction counter and returns to the loop head.
    case advance
}
