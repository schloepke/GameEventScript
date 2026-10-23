// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Api;

/// <summary>A message or publication waiting for its monotonic deadline.</summary>
internal readonly record struct GesDelayedMessage(long Deadline, GesPendingMessage Pending, bool Publish, GesProgramInstance? Owner)
{
    /// <summary>Gets the send ID reported when the entry was scheduled.</summary>
    internal long Id => Pending.Id;

    /// <summary>Gets whether a failed initialization abandoned the entry: its owner failed or an emit lost every recipient.</summary>
    internal bool IsAbandoned => Owner?.StartResult is { State: not GesStartState.Ready } || !Publish && !Pending.HasRecipients();
}
