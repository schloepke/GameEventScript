// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;

namespace GameEventScript.Api;

public sealed partial class GameEventScriptHost
{
    // Named timers of all instances, keyed by the sending instance's registration ID and the name
    // (specs/HostRuntime.md#named-timers). A one-shot timer leaves the registry when its firing starts dispatch; a
    // repeating one stays until it is cancelled, replaced or its sender is detached.
    private Dictionary<(long Instance, string Name), GesNamedTimer>? _timers;

    /// <summary>
    /// Gets a snapshot of the pending named timers of all instances, ordered by deadline and then by send order.
    /// </summary>
    /// <remarks>
    /// Reading never pumps the host. A timer whose firing waits in or runs from the message queue keeps that firing's
    /// deadline; a repeating timer stays listed while its handlers run. Each read returns a new list.
    /// </remarks>
    public IReadOnlyList<GesTimerInfo> PendingTimers
    {
        get
        {
            if (_timers is not { Count: > 0 } timers) return [];
            var ordered = new List<GesNamedTimer>(timers.Values);
            ordered.Sort(static (left, right) => left.Deadline != right.Deadline ? left.Deadline.CompareTo(right.Deadline) : left.Id.CompareTo(right.Id));
            var result = new GesTimerInfo[ordered.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var timer = ordered[index];
                result[index] = new(timer.Name, timer.Deadline, timer.Period, timer.Message.Name, timer.Instance, timer.Publish);
            }

            return result;
        }
    }

    /// <summary>
    /// Discards all queued, delayed and active work and returns the host to the loading state, keeping its Programs,
    /// instances, registrations and random state.
    /// </summary>
    /// <remarks>
    /// The runnable queue, delayed entries and named timers, buffered initialization outputs, a paused handler, the
    /// waiting state and every start result are cleared without individual discard reports; the observer receives one
    /// <see cref="IGesRuntimeObserver.HostReset"/>. The initialization of each attached instance is queued again in load
    /// order under new send IDs, so a following <see cref="Start"/> runs it as the initial group. Send IDs keep counting
    /// and the random generator is neither reseeded nor rewound. A reset is also allowed after a failed start.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown while the host is starting or pumping, including from a native handler or callback.</exception>
    public void Reset()
    {
        if (_pumping || _starting || _executingScript)
            throw new InvalidOperationException("A host cannot be reset while it is starting or executing.");
        if (_hasActiveMessage)
        {
            if (_scriptHandlerActive) _vm?.Reset();
            if (_activeHandler is not null) _ = _random.ReleaseScopeBoundary(_activeRandomBoundary);
        }

        _hasActiveMessage = false;
        _scriptHandlerActive = false;
        _activeHandler = null;
        _activeMessage = default;
        _initializationInstance = null;
        _initializationDiagnostic = null;
        _initializationFailure = null;
        _initializationPublications?.Clear();
        _startupPublications?.Clear();
        _queue.Reset();
        _startupQueue.Reset();
        _delayed?.Clear();
        if (_timers is not null)
        {
            foreach (var timer in _timers.Values) timer.Registered = false;
            _timers.Clear();
        }

        _startResult = null;
        _idleSinceMicroseconds = null;
        var ordered = new List<GesProgramInstance>();
        for (var instance = _instances; instance is not null; instance = instance.NextRegistration) ordered.Add(instance);
        // Instances are linked newest first; initialization runs again in load order.
        for (var index = ordered.Count - 1; index >= 0; index--)
        {
            var instance = ordered[index];
            instance.StartResult = null;
            if (instance.Initialization is { Length: > 0 } entries)
                _startupQueue.Enqueue(new GesPendingMessage(GesSystemEndpoints.CreateInitializationMessage(), entries, [], ++_lastSendId, instance));
        }

        _observer?.HostReset();
    }

    internal bool SendNamedFromContext(GameEventScriptMessage message, bool publish, long microseconds, string name, bool repeat)
    {
        if (_random.HasActiveScopeFault || _activeHandler?.Instance is not { } sender) return false;
        if (repeat ? microseconds <= 0 : microseconds < 0) return false;
        var now = _clock.ElapsedMicroseconds;
        if (now < 0 || microseconds > long.MaxValue - now) return false;
        _timers ??= new();
        var key = (sender.RegistrationId, name);
        var old = _timers.TryGetValue(key, out var existing) ? existing : null;
        if (old is null && _limits.MaxNamedTimers > 0 && _timers.Count >= _limits.MaxNamedTimers)
        {
            _context.RuntimeBudget.Exhaust(nameof(GesRuntimeLimits.MaxNamedTimers), "Named timer limit reached.", _limits.MaxNamedTimers);
            return false;
        }

        // A replaced firing that still waits gives its queue slot to the new one.
        if (old is not { State: GesNamedTimerState.Delayed or GesNamedTimerState.Runnable } && IsMessageQueueFull)
        {
            _context.RuntimeBudget.Exhaust(nameof(GesRuntimeLimits.MaxQueuedMessages), "Delayed message queue limit reached.", _limits.MaxQueuedMessages);
            return false;
        }

        var exact = Array.Empty<GesSubscriptionEntry>();
        var names = Array.Empty<GesSubscriptionEntry>();
        var local = false;
        if (!repeat)
        {
            exact = Get(_exact, message.SignatureId);
            names = Get(_byName, message.Name);
            local = HasMatch(message, exact, names);
            if (!local && !GesSystemEndpoints.IsUndeliverableName(message.Name))
            {
                exact = Get(_exact, GesSystemEndpoints.UndeliverableSignatureId);
                names = Get(_byName, GesSystemEndpoints.UndeliverableName);
                local = HasMatch(message, exact, names);
            }
        }

        if (old is not null) RemoveTimer(old, report: true);
        // Like an unnamed delayed emit, a one-shot emit without recipients is never queued; the name stays free.
        if (!repeat && !local && !publish)
        {
            EmitFromContext(message);
            return !_context.RuntimeBudget.IsExhausted;
        }

        var timer = new GesNamedTimer(sender, name, repeat ? microseconds : 0, publish, message, _initializationInstance) { Registered = true };
        _timers[key] = timer;
        timer.Id = ++_lastSendId;
        timer.Deadline = now + microseconds;
        timer.State = GesNamedTimerState.Delayed;
        var pending = new GesPendingMessage(message, exact, names, timer.Id, initializationOutput: _initializationInstance, timer: timer);
        _delayed ??= new();
        _delayed.Add(new GesDelayedMessage(timer.Deadline, pending, publish, _initializationInstance, sender, timer));
        _idleSinceMicroseconds = null;
        _observer?.MessageScheduled(message, timer.Id, timer.Deadline, publish, name, timer.Period);
        if (publish)
        {
            _stepPublishedMessages++;
            if (_initializationInstance is not null) _initializationPublishes++;
        }
        else
        {
            _stepEmittedMessages++;
            if (_initializationInstance is not null) _initializationEmits++;
            if (!repeat) _observer?.MessageEmitted(message, timer.Id, true);
        }

        return true;
    }

    // Mode 0 cancels one name, mode 1 every named timer and mode 2 also the unnamed delayed sends of the active instance.
    internal bool CancelFromContext(int mode, string? name)
    {
        if (_activeHandler?.Instance is not { } sender) return false;
        var timers = new List<GesNamedTimer>();
        if (_timers is { Count: > 0 })
        {
            if (mode == 0)
            {
                if (name is not null && _timers.TryGetValue((sender.RegistrationId, name), out var timer)) timers.Add(timer);
            }
            else
            {
                foreach (var timer in _timers.Values)
                    if (ReferenceEquals(timer.Instance, sender)) timers.Add(timer);
            }
        }

        var unnamed = new List<GesDelayedMessage>();
        if (mode == 2 && _delayed is { Count: > 0 })
        {
            foreach (var entry in _delayed.InOrder())
                if (entry.Timer is null && ReferenceEquals(entry.Sender, sender)) unnamed.Add(entry);
        }

        if (timers.Count == 0 && unnamed.Count == 0) return false;
        // Discards are reported before anything changes: waiting runnable firings in send order, then delayed entries in
        // deadline order. A firing whose handlers already run only loses its name.
        if (_observer is { } observer)
        {
            var runnable = new List<long>();
            var delayed = new List<(long Deadline, long Id)>();
            foreach (var timer in timers)
            {
                if (timer.State == GesNamedTimerState.Runnable) runnable.Add(timer.Id);
                else if (timer.State == GesNamedTimerState.Delayed) delayed.Add((timer.Deadline, timer.Id));
            }

            foreach (var entry in unnamed) delayed.Add((entry.Deadline, entry.Id));
            runnable.Sort();
            delayed.Sort();
            foreach (var id in runnable) observer.MessageDiscarded(id);
            foreach (var entry in delayed) observer.MessageDiscarded(entry.Id);
        }

        foreach (var timer in timers) RemoveTimer(timer, report: false);
        foreach (var entry in unnamed) _delayed!.Remove(entry.Id);
        return true;
    }

    // Unregisters a timer and removes its waiting firing, if any.
    private void RemoveTimer(GesNamedTimer timer, bool report)
    {
        UnregisterTimer(timer);
        var removed = timer.State switch
        {
            GesNamedTimerState.Delayed => _delayed?.Remove(timer.Id) ?? false,
            GesNamedTimerState.Runnable => _queue.Remove(timer.Id),
            _ => false
        };
        if (removed && report) _observer?.MessageDiscarded(timer.Id);
    }

    private void UnregisterTimer(GesNamedTimer timer)
    {
        if (!timer.Registered) return;
        timer.Registered = false;
        _timers!.Remove((timer.Instance.RegistrationId, timer.Name));
    }

    // Promotes one due named firing. A repeating timer snapshots its recipients only now; without one it is armed again at
    // once, as if its handlers had completed.
    private void PromoteTimer(in GesDelayedMessage delayed, long now)
    {
        var timer = delayed.Timer!;
        var pending = delayed.Pending;
        if (timer.Repeats)
        {
            var message = timer.Message;
            var exact = Get(_exact, message.SignatureId);
            var names = Get(_byName, message.Name);
            if (!HasMatch(message, exact, names) && !GesSystemEndpoints.IsUndeliverableName(message.Name))
            {
                exact = Get(_exact, GesSystemEndpoints.UndeliverableSignatureId);
                names = Get(_byName, GesSystemEndpoints.UndeliverableName);
            }

            pending = new GesPendingMessage(message, exact, names, timer.Id, initializationOutput: delayed.Owner, timer: timer);
        }

        var localAccepted = pending.HasRecipients();
        if (localAccepted)
        {
            _queue.Enqueue(pending);
            timer.State = GesNamedTimerState.Runnable;
            if (timer.Repeats && !timer.Publish) _observer?.MessageEmitted(timer.Message, timer.Id, true);
        }
        else
        {
            if (!timer.Publish) _observer?.MessageDiscarded(timer.Id);
            if (!timer.Repeats) UnregisterTimer(timer);
        }

        if (timer.Publish)
        {
            var attempted = _publishSink is not null;
            var accepted = false;
            if (attempted)
            {
                try { accepted = _publishSink!.Publish(timer.Message); }
                catch (Exception exception)
                {
                    _observer?.RuntimeError(CreateRuntimeDiagnostic(GesDiagnosticCodes.RuntimePublishSinkFailure, "Publish sink failed.", GesRuntimeExceptionText.Describe(exception)));
                }
            }

            _observer?.MessagePublished(timer.Message, timer.Id, new(localAccepted, attempted, accepted));
        }

        if (!localAccepted && timer.Repeats && timer.Registered) ArmTimer(timer, now);
    }

    // Arms a repeating timer for its next firing, one period after now, under a new send ID. No firing is caught up.
    private void ArmTimer(GesNamedTimer timer, long now)
    {
        if (now < 0 || timer.Period > long.MaxValue - now)
        {
            UnregisterTimer(timer);
            return;
        }

        timer.Id = ++_lastSendId;
        timer.Deadline = now + timer.Period;
        timer.State = GesNamedTimerState.Delayed;
        _delayed ??= new();
        _delayed.Add(new GesDelayedMessage(timer.Deadline, new GesPendingMessage(timer.Message, [], [], timer.Id, timer: timer), timer.Publish, timer.Owner, timer.Instance, timer));
        _idleSinceMicroseconds = null;
        _observer?.MessageScheduled(timer.Message, timer.Id, timer.Deadline, timer.Publish, timer.Name, timer.Period);
    }

    // A one-shot firing releases its name when its dispatch starts; a repeating one marks itself as running.
    private void StartTimerDispatch(GesNamedTimer timer)
    {
        if (timer.Repeats) timer.State = GesNamedTimerState.Dispatching;
        else UnregisterTimer(timer);
    }

    // Arms a repeating timer again once all handlers of its firing completed, unless it was cancelled meanwhile.
    private void CompleteTimerDispatch(GesNamedTimer timer)
    {
        if (timer.Repeats && timer.Registered) ArmTimer(timer, _clock.ElapsedMicroseconds);
    }

    // Ends the named timers of a detached instance. A waiting repeating firing is discarded unless the instance failed its
    // initialization, whose entries the failure path discards; queued firings and one-shot entries keep their delivery.
    private void DetachTimers(GesProgramInstance instance)
    {
        if (_timers is not { Count: > 0 }) return;
        List<GesNamedTimer>? ended = null;
        foreach (var timer in _timers.Values)
            if (ReferenceEquals(timer.Instance, instance)) (ended ??= new()).Add(timer);
        if (ended is null) return;
        var failed = instance.StartResult is { State: not GesStartState.Ready };
        ended.Sort(static (left, right) => left.Deadline != right.Deadline ? left.Deadline.CompareTo(right.Deadline) : left.Id.CompareTo(right.Id));
        foreach (var timer in ended)
        {
            if (timer.Repeats && !failed && timer.State == GesNamedTimerState.Delayed) RemoveTimer(timer, report: true);
            else UnregisterTimer(timer);
        }
    }
}
