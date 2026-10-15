// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal static class GesVmRegisterSeries
{
    internal static void GesVmCreateSeries(this GesVirtualMachine vm, ushort destinationRegister, GesBytecodeSeriesKind seriesKind)
    {
        switch (seriesKind)
        {
            case GesBytecodeSeriesKind.Fibonacci:
                vm.SetSeries(destinationRegister, GesSeries.Fibonacci());
                return;
            case GesBytecodeSeriesKind.Factorial:
                vm.SetSeries(destinationRegister, GesSeries.Factorial());
                return;
            default:
                vm.SetNothing(destinationRegister);
                vm.RaiseError(GesDiagnosticCodes.RuntimeInvalidSeriesKind, "Unknown series kind.");
                return;
        }
    }
}
