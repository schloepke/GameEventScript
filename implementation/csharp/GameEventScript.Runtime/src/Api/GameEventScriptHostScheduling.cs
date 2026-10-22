// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;

namespace GameEventScript.Api;

public sealed partial class GameEventScriptHost
{
    private long? _idleSinceMicroseconds;

    /// <summary>Gets the elapsed whole microseconds in the current ready, work-free phase, or null before readiness or while work is active, queued or delayed.</summary>
    /// <remarks>Uses the host's monotonic clock. Reading never pumps or schedules work. Rejected messages do not reset the duration; callers must serialize access as for other host operations.</remarks>
    public long? IdleDurationMicroseconds => IsReady && !_pumping && IsIdle && _idleSinceMicroseconds is { } since
        ? Math.Max(0, _clock.ElapsedMicroseconds - since) : null;

    private void UpdateIdleDuration()
    {
        if (IsReady && IsIdle) _idleSinceMicroseconds ??= _clock.ElapsedMicroseconds;
        else _idleSinceMicroseconds = null;
    }

    private readonly IGesClock _clock;
    private List<GesDelayedMessage>? _delayed;
    private long _lastScheduledId;

    /// <summary>Gets whole microseconds until the earliest delayed message, or null when no delayed message remains. Reading never pumps the host.</summary>
    public long? NextMessageDelay => _delayed is { Count: > 0 } ? Math.Max(0, _delayed[0].Deadline - _clock.ElapsedMicroseconds) : null;

    internal bool SendFromContext(GameEventScriptMessage message, bool publish, long microseconds)
    {
        if (microseconds < 0 || _random.HasActiveScopeFault) return false;
        if (microseconds == 0)
        {
            // Absence of a recipient is still successful acceptance; a full queue is not.
            if (IsMessageQueueFull && (HasMatch(message, Get(_exact, message.SignatureId), Get(_byName, message.Name)) ||
                !GesSystemEndpoints.IsUndeliverableName(message.Name) && HasMatch(message, Get(_exact, GesSystemEndpoints.UndeliverableSignatureId), Get(_byName, GesSystemEndpoints.UndeliverableName))))
            {
                _context.RuntimeBudget.Exhaust(nameof(GesRuntimeLimits.MaxQueuedMessagesPerRun), "Message queue limit reached.", _limits.MaxQueuedMessagesPerRun);
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
            _context.RuntimeBudget.Exhaust(nameof(GesRuntimeLimits.MaxQueuedMessagesPerRun), "Delayed message queue limit reached.", _limits.MaxQueuedMessagesPerRun);
            return false;
        }
        var exact = Get(_exact, message.SignatureId);
        var names = Get(_byName, message.Name);
        if (!HasMatch(message, exact, names) && !GesSystemEndpoints.IsUndeliverableName(message.Name))
        {
            exact = Get(_exact, GesSystemEndpoints.UndeliverableSignatureId);
            names = Get(_byName, GesSystemEndpoints.UndeliverableName);
        }
        var pending = new GesPendingMessage(message, exact, names, initializationOutput: _initializationInstance);
        var delayed = new GesDelayedMessage(now + microseconds, pending, publish, _initializationInstance, ++_lastScheduledId);
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
        _idleSinceMicroseconds = null;
        _observer?.MessageScheduled(message, delayed.Id, delayed.Deadline, publish);
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
            if (delayed.Owner?.StartResult is { State: not GesStartState.Ready })
            {
                _observer?.ScheduledMessageDiscarded(delayed.Id);
                continue;
            }
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
                    _observer?.RuntimeError(CreateRuntimeDiagnostic(GesDiagnosticCodes.RuntimePublishSinkFailure, "Publish sink failed.", GesRuntimeExceptionText.Describe(exception)));
                }
            }
            _observer?.MessagePublished(delayed.Pending.Message, new(localAccepted, attempted, accepted));
        }
    }

    // Removes delayed entries of a failed initialization and emits left without recipients, in deadline order.
    private void RemoveFailedDelayed()
    {
        if (_delayed is null) return;
        var kept = 0;
        for (var index = 0; index < _delayed.Count; index++)
        {
            var item = _delayed[index];
            if (item.Owner?.StartResult is { State: not GesStartState.Ready } || !item.Publish && !item.Pending.HasRecipients())
            {
                _observer?.ScheduledMessageDiscarded(item.Id);
                continue;
            }

            _delayed[kept++] = item;
        }

        _delayed.RemoveRange(kept, _delayed.Count - kept);
    }

    // Discards every delayed entry, reporting each in deadline order.
    private void ClearDelayed()
    {
        if (_delayed is null) return;
        if (_observer is { } observer)
        {
            for (var index = 0; index < _delayed.Count; index++) observer.ScheduledMessageDiscarded(_delayed[index].Id);
        }

        _delayed.Clear();
    }
}
