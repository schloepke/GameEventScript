# Agent Handoff

## Workspace

Root: `/Users/stephan/Projects/BattleClub/BeamableServices/services`

Main project: `StepH-GameEventScript`

Tests: `StepH-GameEventScript-Tests`

Standard verification:

```bash
dotnet test StepH-GameEventScript-Tests/StepH-GameEventScript-Tests.csproj --filter "TestCategory!=Performance"
```

## Collaboration Rules

- The user often wants analysis first when explicitly saying "nur analysieren", "nichts ändern", or similar. Otherwise implementation is usually expected.
- Do not preserve legacy compatibility unless the user explicitly asks for it. The API and DSL are still in development.
- Prefer portability toward Swift, Kotlin, C++, and similar targets.
- Keep C#-specific code in `StepH-GameEventScript/CSharpBridge`.
- The portable Core/API/Runtime/Compiler should avoid C#-specific patterns where practical.
- `CSharpBridge` may use C# idioms such as Reflection, Attributes, `System.Type`, `out`, locks, threads, and `Try...out`.
- In portable core code, avoid own GES-level `Try...out` concepts. Standard library calls such as `Dictionary.TryGetValue`, `TryAdd`, and `TryParse` are currently accepted.
- HotPath VM mutation should go through `GesVmState.Set...` methods. Read-only register borrows are currently accepted for performance.
- Be careful with direct register refs: never hold a mutable destination ref across operations that may grow or replace register storage.
- Use `rg` for code search.
- Do not revert user changes unless explicitly requested.

## Current Architecture Direction

The project is moving toward a portable Game Event Script VM with a compact value model:

- The old polymorphic value graph has been removed or largely replaced.
- `GesValue` / `GameEventScriptValue` are the current compact value concepts.
- Runtime VM code lives under `StepH-GameEventScript/Runtime/VM`.
- C# Reflection and annotation support lives under `StepH-GameEventScript/CSharpBridge`.
- The old VM/compiler path has been removed or superseded by the new binary compiler and VM.
- Standard extensions were migrated into opcodes where possible.
- Series now use direct VM concepts and `CreateSeries`.

## Recent Completed Work

### Custom `Try...` Cleanup

The user asked why `bool TryXXXX` concepts still existed outside `CSharpBridge`.

Completed cleanup:

- Compiler/Rewriter custom `Try...` methods were removed or renamed:
  - `TryEmitExpressionToRegister` -> `EmitExpressionToRegister`
  - `TryEmitCollectionPipelineInto` -> `EmitCollectionPipelineInto`
  - `TryEmitSpatialConstructor` -> `EmitSpatialConstructor`
  - Rewriter helpers like `TryGetJumpTarget`, `TryInvertBranch`, `TryFoldUnary`, `TryFoldBinary`, and `TryGetLocalConstant` were converted to nullable/default-return styles.
- Runtime/Budget/Host custom `Try...` methods were renamed:
  - `TryConsumeExecutionStep(s)` -> `ConsumeExecutionStep(s)IfAvailable`
  - `TryConsumeLoopIteration` -> `ConsumeLoopIterationIfAvailable`
  - `TryEnterCall` -> `EnterCallIfAvailable`
  - `TryCheckRangeLength` -> `CheckRangeLengthWithinLimit`
  - `TryCheckGeneratedCollectionItemCount` -> `CheckGeneratedCollectionItemCountWithinLimit`
  - `TryCheckDice` -> `CheckDiceWithinLimit`
  - `TryEnqueue...` -> `Enqueue...`

Verification after this cleanup:

```text
990/990 non-performance tests passed
```

Remaining `Try...` outside `CSharpBridge` should only be standard-library style uses such as:

- `TryGetValue`
- `TryAdd`
- `TryParse`

## Important Files

- `StepH-GameEventScript/Compiler/GesCompiler.cs`
- `StepH-GameEventScript/Compiler/GesBinaryBuilder.cs`
- `StepH-GameEventScript/Compiler/GesBinaryBuilderRewriter.cs`
- `StepH-GameEventScript/Runtime/GesRuntimeBudget.cs`
- `StepH-GameEventScript/Runtime/GameEventScriptRuntimeHost.cs`
- `StepH-GameEventScript/Runtime/VM/GameEventScriptVirtualMaschine.cs`
- `StepH-GameEventScript/Runtime/VM/GesVmState.cs`
- `StepH-GameEventScript/Api/GameEventScriptSession.cs`
- `StepH-GameEventScript/CSharpBridge`

## Known Warnings

The normal test build currently emits XML documentation warnings for `GesValueMap`. These warnings were not part of the last cleanup task.

## Open Topics

Likely next useful areas:

- Message/Emit allocation path, only if performance data justifies more work.
- More compiler allocation optimization.
- Check whether bytecode optimizer passes still produce meaningful diffs now that the compiler emits better registers directly.
- Table type and mutation/lifecycle concept.
- Further VM-near extension call model if boxing at the extension boundary becomes expensive again.
- JSON/wire message shape is intentionally not finalized yet.

