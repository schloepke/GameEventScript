// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using GameEventScript.Api;

internal sealed class HostScenario(GameEventScriptHost host, IReadOnlyDictionary<string, GameEventScriptProgram> programs, JsonElement request, VirtualClock clock)
{
    private readonly Dictionary<string, GesProgramInstance> _instances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GesSubscription> _subscriptions = new(StringComparer.Ordinal);
    internal List<object> Actions { get; } = new();

    internal void Configure()
    {
        var deferred = request.GetProperty("deferredPrograms").EnumerateArray().Select(p => p.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var program in programs) if (!deferred.Contains(program.Key)) Load(program.Key);
        foreach (var definition in request.GetProperty("nativeHandlers").EnumerateArray())
            if (definition.GetProperty("initiallySubscribed").GetBoolean()) Subscribe(definition.GetProperty("id").GetString()!);
    }

    internal void Apply(string origin, JsonElement actions)
    {
        if (actions.GetArrayLength() == 0) return;
        var results = new List<object>();
        Actions.Add(new { origin, results });
        foreach (var action in actions.EnumerateArray())
        {
            var target = action.GetProperty("target").GetString()!;
            try
            {
                var result = action.GetProperty("operation").GetString() switch
                {
                    "advanceMicroseconds" => clock.Advance(target),
                    "loadProgram" => Load(target),
                    "detachProgram" => _instances.TryGetValue(target, out var instance) && instance.Detach(),
                    "subscribeHandler" => Subscribe(target),
                    "unsubscribeHandler" => _subscriptions.TryGetValue(target, out var subscription) && subscription.Unsubscribe(),
                    _ => throw new ArgumentException("Unknown host action.")
                };
                results.Add(new { result = (bool?)result, error = (object?)null });
            }
            catch (GesDynamicLinkException exception)
            {
                results.Add(new { result = (bool?)null, error = CompilerObservations.Diagnostic(exception.Diagnostic) });
            }
        }
    }

    internal void CaptureState(Dictionary<string, object> observation)
    {
        observation["hostReady"] = host.IsReady;
        observation["idleDurationMicroseconds"] = host.IdleDurationMicroseconds?.ToString(CultureInfo.InvariantCulture) ?? "none";
        observation["programStarts"] = _instances.ToDictionary(pair => pair.Key, pair => pair.Value.StartResult?.State switch
        {
            GesStartState.Ready => "ready",
            GesStartState.RuntimeError => "runtimeError",
            GesStartState.RuntimeLimitReached => "runtimeLimitReached",
            _ => "pending"
        }, StringComparer.Ordinal);
    }

    private bool Load(string id)
    {
        if (_instances.TryGetValue(id, out var instance) && instance.IsAttached) return true;
        _instances[id] = host.Load(programs[id]);
        return true;
    }

    private bool Subscribe(string id)
    {
        if (_subscriptions.TryGetValue(id, out var existing) && existing.IsSubscribed) return true;
        var definition = request.GetProperty("nativeHandlers").EnumerateArray().Single(d => d.GetProperty("id").GetString() == id);
        var handler = NativeHandlers.Create(definition, this);
        var name = definition.GetProperty("message").GetString()!;
        var priority = definition.GetProperty("priority").GetInt32();
        _subscriptions[id] = definition.GetProperty("messageName").GetBoolean()
            ? host.SubscribeMessageName(name, handler, priority: priority)
            : host.Subscribe(name, definition.GetProperty("parameters").EnumerateArray().Select(p => p.GetString()!).ToArray(), handler, priority);
        return true;
    }

    internal static void Validate(JsonElement request, IReadOnlySet<string> programs)
    {
        var deferred = request.GetProperty("deferredPrograms").EnumerateArray().Select(p => p.GetString()!).ToArray();
        if (deferred.Any(p => !programs.Contains(p)) || deferred.Distinct(StringComparer.Ordinal).Count() != deferred.Length) throw new ArgumentException("Invalid deferred Programs.");
        var handlers = request.GetProperty("nativeHandlers").EnumerateArray().Select(h => h.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        void ValidateActions(JsonElement actions)
        {
            foreach (var action in actions.EnumerateArray())
            {
                RuntimeValues.Fields(action, "operation", "target");
                var target = action.GetProperty("target").GetString();
                var valid = action.GetProperty("operation").GetString() switch
                {
                    "advanceMicroseconds" => VirtualClock.ValidDuration(target),
                    "loadProgram" => deferred.Contains(target, StringComparer.Ordinal),
                    "detachProgram" => target is not null && programs.Contains(target),
                    "subscribeHandler" or "unsubscribeHandler" => target is not null && handlers.Contains(target),
                    _ => false
                };
                if (!valid) throw new ArgumentException("Invalid host action target.");
            }
        }
        foreach (var handler in request.GetProperty("nativeHandlers").EnumerateArray()) ValidateActions(handler.GetProperty("actions"));
        var steps = request.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var step in request.GetProperty("stepActions").EnumerateObject())
        {
            if (!steps.Contains(step.Name)) throw new ArgumentException("Unknown action step.");
            ValidateActions(step.Value);
        }
    }
}
