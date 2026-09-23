// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;

namespace GameEventScript.Api;

public sealed partial class GameEventScriptHost
{
    private readonly IGameEventScriptClock _clock;
    private List<DelayedMessage>? _delayed;

    /// <summary>Gets whole microseconds until the earliest delayed message, or null when no delayed message remains. Reading never pumps the host.</summary>
    public long? NextMessageDelay => _delayed is { Count: > 0 } ? Math.Max(0, _delayed[0].Deadline - _clock.ElapsedMicroseconds) : null;

    internal bool SendFromContext(GameEventScriptMessage message, bool publish, long microseconds)
    {
        if (microseconds < 0 || _random.HasActiveScopeFault) return false;
        if (microseconds == 0)
        {
            // Absence of a recipient is still successful acceptance; a full queue is not.
            if (IsMessageQueueFull && (HasMatch(message, Get(_exact, message.SignatureId), Get(_byName, message.Name)) ||
                !GameEventScriptSystemEndpoints.IsUndeliverableName(message.Name) && HasMatch(message, Get(_exact, GameEventScriptSystemEndpoints.UndeliverableSignatureId), Get(_byName, GameEventScriptSystemEndpoints.UndeliverableName))))
            {
                _context.RuntimeBudget.Exhaust(nameof(GameEventScriptRuntimeLimits.MaxQueuedMessagesPerRun), "Message queue limit reached.", _limits.MaxQueuedMessagesPerRun);
                return false;
            }
            if (publish) PublishFromContext(message);
            else EmitFromContext(message);
            return !_context.RuntimeBudget.IsExhausted;
        }
        var now = _clock.ElapsedMicroseconds;
        if (now < 0 || microseconds > long.MaxValue - now) return false;
        if (IsMessageQueueFull)
        {
            _context.RuntimeBudget.Exhaust(nameof(GameEventScriptRuntimeLimits.MaxQueuedMessagesPerRun), "Delayed message queue limit reached.", _limits.MaxQueuedMessagesPerRun);
            return false;
        }
        var exact = Get(_exact, message.SignatureId);
        var names = Get(_byName, message.Name);
        if (!HasMatch(message, exact, names) && !GameEventScriptSystemEndpoints.IsUndeliverableName(message.Name))
        {
            exact = Get(_exact, GameEventScriptSystemEndpoints.UndeliverableSignatureId);
            names = Get(_byName, GameEventScriptSystemEndpoints.UndeliverableName);
        }
        var pending = new PendingMessage(message, exact, names, initializationOutput: _initializationInstance);
        var delayed = new DelayedMessage(now + microseconds, pending, publish, _initializationInstance);
        _delayed ??= new();
        var lo = 0;
        var hi = _delayed.Count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (_delayed[mid].Deadline <= delayed.Deadline) lo = mid + 1;
            else hi = mid;
        }
        _delayed.Insert(lo, delayed);
        if (publish)
        {
            _stepPublishedMessages++;
            if (_initializationInstance is not null) _initializationPublishes++;
        }
        else
        {
            _stepEmittedMessages++;
            if (_initializationInstance is not null) _initializationEmits++;
            _observer?.MessageEmitted(message, true);
        }
        return true;
    }

    private void PromoteDelayed()
    {
        if (_delayed is not { Count: > 0 }) return;
        var now = _clock.ElapsedMicroseconds;
        while (_delayed.Count > 0 && _delayed[0].Deadline <= now)
        {
            var delayed = _delayed[0];
            _delayed.RemoveAt(0);
            if (delayed.Owner?.StartResult is { State: not GameEventScriptStartState.Ready }) continue;
            var localAccepted = delayed.Pending.HasRecipients();
            if (localAccepted) _queue.Enqueue(delayed.Pending);
            if (!delayed.Publish) continue;
            var attempted = _publishSink is not null;
            var accepted = false;
            if (attempted)
            {
                try { accepted = _publishSink!.Publish(delayed.Pending.Message); }
                catch (Exception exception)
                {
                    _observer?.RuntimeError(CreateRuntimeDiagnostic(GameEventScriptDiagnosticCodes.RuntimePublishSinkFailure, "Publish sink failed.", GameEventScriptRuntimeExceptionText.Describe(exception)));
                }
            }
            _observer?.MessagePublished(delayed.Pending.Message, new(localAccepted, attempted, accepted));
        }
    }

    private void RemoveFailedDelayed()
    {
        if (_delayed is null) return;
        for (var index = _delayed.Count - 1; index >= 0; index--)
        {
            var item = _delayed[index];
            if (item.Owner?.StartResult is { State: not GameEventScriptStartState.Ready } || !item.Publish && !item.Pending.HasRecipients()) _delayed.RemoveAt(index);
        }
    }

    private readonly record struct DelayedMessage(long Deadline, PendingMessage Pending, bool Publish, GameEventScriptInstance? Owner);
}
