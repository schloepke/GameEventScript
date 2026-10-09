// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Runtime.Values;

internal sealed class GesTextPayload(string text, int count)
{
    internal readonly string Text = text;
    internal readonly int Count = count;
}
