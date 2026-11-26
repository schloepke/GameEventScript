// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>Where the current firing of a named timer is: waiting for its deadline, promoted to the FIFO, or being dispatched.</summary>
internal enum GesNamedTimerState
{
    Delayed,
    Runnable,
    Dispatching
}

/// <summary>
/// One named <c>emit|publish after|every</c> of a sending instance (specs/HostRuntime.md#named-timers). The host keeps it
/// registered under the instance and the name until it fires once (<c>after</c>), is cancelled or replaced, or its
/// instance detaches; an <c>every</c> timer re-arms when each logical message it sent completes.
/// </summary>
internal sealed class GesNamedTimer(GesProgramInstance instance, string name, long period, bool publish, GameEventScriptMessage message, GesProgramInstance? owner)
{
    internal GesProgramInstance Instance { get; } = instance;
    internal string Name { get; } = name;
    /// <summary>Gets the repeat period in whole microseconds, or 0 for a one-shot <c>after</c> timer.</summary>
    internal long Period { get; } = period;
    internal bool Publish { get; } = publish;
    internal GameEventScriptMessage Message { get; } = message;
    /// <summary>Gets the initializing instance that scheduled the timer, whose failure abandons it.</summary>
    internal GesProgramInstance? Owner { get; } = owner;
    /// <summary>Gets or sets the send ID of the current firing.</summary>
    internal long Id { get; set; }
    /// <summary>Gets or sets the deadline of the current firing on the host clock.</summary>
    internal long Deadline { get; set; }
    internal GesNamedTimerState State { get; set; }
    /// <summary>Gets or sets whether the host's registry still maps the instance and name to this timer.</summary>
    internal bool Registered { get; set; }
    internal bool Repeats => Period > 0;
}
