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
    /// Called before each fetched instruction executes, including one that faults. Finish the previous measurement,
    /// then start this zero-based address before any jump or call. The first call after finishSlice starts a new interval.
    func instructionStarting(_ address: Int)

    /// Closes the last interval on completion, pause, limit or failure; may be called without a preceding instruction.
    /// Clear pending timing so time outside the VM is never charged to the next slice.
    func finishSlice()
}
