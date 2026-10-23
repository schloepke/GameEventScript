// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Api;

/// <summary>One logical message with its enqueue-time recipient snapshot.</summary>
internal struct GesPendingMessage
{
    private readonly GesSubscriptionEntry[] _exact;
    private readonly GesSubscriptionEntry[] _names;
    private int _exactIndex;
    private int _nameIndex;

    internal GesPendingMessage(GameEventScriptMessage message, GesSubscriptionEntry[] exact, GesSubscriptionEntry[] names, long id, GesProgramInstance? initialization = null, GesProgramInstance? initializationOutput = null)
    {
        Message = message;
        Id = id;
        Initialization = initialization;
        InitializationOutput = initializationOutput;
        _exact = exact;
        _names = names;
        _exactIndex = 0;
        _nameIndex = 0;
    }

    internal GameEventScriptMessage Message { get; }
    /// <summary>Gets the host-local send ID that observer callbacks report for this message.</summary>
    internal long Id { get; }
    internal GesProgramInstance? Initialization { get; }
    private GesProgramInstance? InitializationOutput { get; }
    internal bool HasRecipients()
    {
        if (InitializationOutput?.StartResult is { State: not GesStartState.Ready }) return false;
        foreach (var entry in _exact) if (CanReceive(entry)) return true;
        foreach (var entry in _names) if (CanReceive(entry)) return true;
        return false;
    }
    private bool CanReceive(GesSubscriptionEntry entry)
        => entry.Instance?.StartResult is not { State: not GesStartState.Ready } && entry.Matches(Message);

    internal GesSubscriptionEntry? NextMatching()
    {
        while (_exactIndex < _exact.Length || _nameIndex < _names.Length)
        {
            GesSubscriptionEntry next;
            if (_exactIndex >= _exact.Length) next = _names[_nameIndex++];
            else if (_nameIndex >= _names.Length) next = _exact[_exactIndex++];
            else if (GesSubscriptionEntry.Compare(_exact[_exactIndex], _names[_nameIndex]) <= 0) next = _exact[_exactIndex++];
            else next = _names[_nameIndex++];
            if (CanReceive(next)) return next;
        }

        return null;
    }
}
