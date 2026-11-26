// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// A read-only snapshot of one pending named timer of a host (specs/HostRuntime.md#named-timers), as listed by
/// <see cref="GameEventScriptHost.PendingTimers"/>.
/// </summary>
public sealed class GesTimerInfo
{
    internal GesTimerInfo(GesValue name, long deadline, long period, string messageName, GesProgramInstance instance, bool publish)
    {
        Name = name;
        Deadline = deadline;
        Period = period;
        MessageName = messageName;
        Instance = instance;
        Publish = publish;
    }

    /// <summary>Gets the timer name: the Text or Tag key of the named send as Text, or the handle that names it.</summary>
    public GesValue Name { get; }

    /// <summary>
    /// Gets the absolute deadline of the current firing in whole microseconds on the host's monotonic clock. For a firing
    /// that is already due and waiting in or running from the message queue, it remains that firing's deadline.
    /// </summary>
    public long Deadline { get; }

    /// <summary>Gets the repeat period of an <c>every</c> timer in whole microseconds, or 0 for a one-shot <c>after</c> timer.</summary>
    public long Period { get; }

    /// <summary>Gets the name of the message the timer sends.</summary>
    public string MessageName { get; }

    /// <summary>Gets the sending Program instance whose names scope the timer.</summary>
    public GesProgramInstance Instance { get; }

    /// <summary>Gets whether the timer publishes rather than emits.</summary>
    public bool Publish { get; }
}
