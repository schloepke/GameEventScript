// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteContainsAny(ushort dst, in GesValue a, in GesValue b) => ContainsAnyOrAll(dst, in a, in b, requireAll: false);
}
