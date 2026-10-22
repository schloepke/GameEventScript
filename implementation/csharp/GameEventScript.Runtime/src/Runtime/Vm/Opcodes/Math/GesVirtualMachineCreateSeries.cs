// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateSeries(ushort destinationRegister, GesBytecodeSeriesKind seriesKind)
    {
        switch (seriesKind)
        {
            case GesBytecodeSeriesKind.Fibonacci:
                SetSeries(destinationRegister, GesSeriesValue.Fibonacci());
                return;
            case GesBytecodeSeriesKind.Factorial:
                SetSeries(destinationRegister, GesSeriesValue.Factorial());
                return;
            default:
                SetNothing(destinationRegister);
                RaiseError(GesDiagnosticCodes.RuntimeInvalidSeriesKind, "Unknown series kind.");
                return;
        }
    }
}
