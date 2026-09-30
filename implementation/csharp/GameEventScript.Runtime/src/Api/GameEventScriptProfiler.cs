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
    /// Finish the previous measurement, then start this address. The first call after FinishSlice starts a new interval.</summary>
    /// <param name="address">Zero-based instruction address in the immutable Program, before any jump or call.</param>
    void InstructionStarting(int address);

    /// <summary>Closes the last instruction interval on completion, pause, limit or failure. May be called without
    /// a preceding instruction. Clear pending timing so time outside the VM is never charged to the next slice.</summary>
    void FinishSlice();
}
