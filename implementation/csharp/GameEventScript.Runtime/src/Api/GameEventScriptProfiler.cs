// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>Optional synchronous instrumentation factory. Callbacks must not throw or reenter a host.
/// Timing, allocation and report policies belong to the embedding. Instances require serialized access.</summary>
public interface IGameEventScriptProfiler
{
    /// <summary>Creates independent instrumentation for one loaded Program instance, before execution.
    /// The host retains the returned object until the instance is released; the factory may retain its results.</summary>
    /// <param name="program">Immutable code and optional debug information; duplicate modules remain distinct instances.</param>
    /// <returns>Instrumentation for this instance.</returns>
    IGameEventScriptProgramProfiler CreateProgramProfiler(GameEventScriptProgram program);
}

/// <summary>Instruction-boundary callbacks for one loaded instance. Callbacks must not throw, pump a host,
/// or modify execution. No callback is made while the host waits between slices.</summary>
public interface IGameEventScriptProgramProfiler
{
    /// <summary>Called immediately before each fetched instruction executes, including an instruction that faults.
    /// Closes Fetch and starts Execute for this address. Pre-execution phases belong to this instruction.</summary>
    /// <param name="address">Zero-based instruction address in the immutable Program, before any jump or call.</param>
    void InstructionStarting(int address);

    /// <summary>Starts a loop phase, closing the preceding interval. Execute starts through InstructionStarting.
    /// Checks before an instruction may terminate the loop without another instruction start.</summary>
    /// <param name="phase">The phase about to execute.</param>
    void PhaseStarting(GameEventScriptProfilePhase phase);

    /// <summary>Closes the last phase interval on completion, pause, limit or failure. May be called without
    /// a preceding instruction. Clear pending timing so time outside the VM is never charged to the next slice.</summary>
    void FinishSlice();
}

/// <summary>Non-overlapping regions of an instrumented VM loop; timing policies belong to the embedding.</summary>
public enum GameEventScriptProfilePhase
{
    /// <summary>Checks whether the VM remains processing.</summary>
    StateCheck,
    /// <summary>Checks whether the runtime budget is exhausted.</summary>
    BudgetCheck,
    /// <summary>Checks the reserved per-slice instruction count.</summary>
    SliceCheck,
    /// <summary>Validates the instruction pointer, fetches code and increments the pointer.</summary>
    Fetch,
    /// <summary>Dispatches and executes one opcode, including synchronous callbacks and fault handling.</summary>
    Execute,
    /// <summary>Advances the completed instruction counter and returns to the loop head.</summary>
    Advance
}
