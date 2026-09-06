// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesCustomObject(string typeName, GesValueMap map)
{
    internal string TypeName { get; } = typeName;
    internal GesValueMap Map { get; } = map;
    internal int Length => Map.Length;
}
