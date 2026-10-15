// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteMoveNumeric(ushort destination, ushort source)
    {
        ref readonly var value = ref Register(source);
        if (!value.IsNumeric) SetNothing(destination);
        else if (value.Kind is GesBytecodeTypeKind.Boolean or Dice) SetInteger(destination, value.IntegerValue);
        else SetValue(destination, in value);
    }
}
