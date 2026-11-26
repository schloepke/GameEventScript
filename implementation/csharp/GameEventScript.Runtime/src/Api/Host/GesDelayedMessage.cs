// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Api;

/// <summary>A message or publication waiting for its monotonic deadline. <c>Sender</c> is the instance whose handler sent it,
/// which <c>cancel all</c> selects by; <c>Timer</c> is the named timer the entry is the current firing of.</summary>
internal readonly record struct GesDelayedMessage(long Deadline, GesPendingMessage Pending, bool Publish, GesProgramInstance? Owner, GesProgramInstance? Sender = null, GesNamedTimer? Timer = null)
{
    /// <summary>Gets the send ID reported when the entry was scheduled.</summary>
    internal long Id => Pending.Id;

    /// <summary>Gets whether a failed initialization abandoned the entry: its owner failed, an emit lost every recipient, or
    /// a repeating timer lost its registration with its failed instance. A repeating entry takes its recipients when due.</summary>
    internal bool IsAbandoned => Owner?.StartResult is { State: not GesStartState.Ready }
        || (Timer is { Repeats: true } timer ? !timer.Registered : !Publish && !Pending.HasRecipients());
}
