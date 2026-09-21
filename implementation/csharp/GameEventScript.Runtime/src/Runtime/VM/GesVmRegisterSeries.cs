// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterSeries
{
    internal static void GesVmCreateSeries(this GesVmState vmState, ushort destinationRegister, GameEventScriptBytecodeSeriesKind seriesKind)
    {
        switch (seriesKind)
        {
            case GameEventScriptBytecodeSeriesKind.Fibonacci:
                vmState.SetSeries(destinationRegister, GesSeries.Fibonacci());
                return;
            case GameEventScriptBytecodeSeriesKind.Factorial:
                vmState.SetSeries(destinationRegister, GesSeries.Factorial());
                return;
            default:
                vmState.SetNothing(destinationRegister);
                vmState.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidSeriesKind, "Unknown series kind.");
                return;
        }
    }
}
