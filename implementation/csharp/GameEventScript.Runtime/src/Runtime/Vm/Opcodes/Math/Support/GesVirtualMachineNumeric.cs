// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private static GesValue NumericOperand(in GesValue value)
        => value.Kind is GesBytecodeTypeKind.Boolean or Dice ? GesValue.GesInteger(value.IntegerValue) : value;
}
