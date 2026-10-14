// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterSeries
{
    internal static void GesVmCreateSeries(this GesVirtualMachine vm, ushort destinationRegister, GameEventScriptBytecodeSeriesKind seriesKind)
    {
        switch (seriesKind)
        {
            case GameEventScriptBytecodeSeriesKind.Fibonacci:
                vm.SetSeries(destinationRegister, GesSeries.Fibonacci());
                return;
            case GameEventScriptBytecodeSeriesKind.Factorial:
                vm.SetSeries(destinationRegister, GesSeries.Factorial());
                return;
            default:
                vm.SetNothing(destinationRegister);
                vm.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidSeriesKind, "Unknown series kind.");
                return;
        }
    }
}
