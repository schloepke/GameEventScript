// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;

internal sealed class RuntimeObservations(string publishSink) : IGesRuntimeObserver, IGesPublishSink
{
    private readonly List<GameEventScriptMessage> _local = new();
    private readonly List<GameEventScriptMessage> _outbound = new();
    private readonly List<object> _limits = new();
    private readonly List<object> _diagnostics = new();
    private readonly List<Dictionary<string, object>> _trace = new();

    internal static object Run(GameEventScriptProgram program, JsonElement request) => Run(new Dictionary<string, GameEventScriptProgram> { ["main"] = program }, request);

    internal static object Run(IReadOnlyDictionary<string, GameEventScriptProgram> programs, JsonElement request)
    {
        var hosts = new object[request.GetProperty("hostCount").GetInt32()];
        for (var index = 0; index < hosts.Length; index++) hosts[index] = RunHost(programs, request);
        return new { hosts };
    }

    private static object RunHost(IReadOnlyDictionary<string, GameEventScriptProgram> programs, JsonElement request)
    {
        var observer = new RuntimeObservations(request.GetProperty("publishSink").GetString()!);
        var clock = new VirtualClock();
        var builder = GameEventScriptHost.CreateBuilder().WithClock(clock).WithRuntimeObserver(observer)
            .WithExternalTypeRegistry(GameEventScript.Tests.Conformance.GameEventScriptConformanceExternalTypes.Registry)
            .WithRegistry(new RuntimeFixtures()).WithRuntimeLimits(CreateLimits(request.GetProperty("limits")));
        if (request.GetProperty("publishSink").GetString() != "absent") builder.WithPublishSink(observer);
        RuntimeRandom.Configure(builder, request.GetProperty("random"));
        var host = builder.Build();
        var scenario = new HostScenario(host, programs, request, clock);
        scenario.Configure();
        host.Start();
        if (host.IsReady && request.GetProperty("initializationPump").GetString() == "completion") host.RunToCompletion();
        var initialization = observer.Capture();
        initialization["actions"] = scenario.Actions.ToArray();
        scenario.CaptureState(initialization);
        var steps = new Dictionary<string, object>();
        foreach (var step in request.GetProperty("steps").EnumerateArray())
        {
            observer.Clear();
            scenario.Actions.Clear();
            var stepId = step.GetProperty("id").GetString()!;
            if (request.GetProperty("stepActions").TryGetProperty(stepId, out var actions)) scenario.Apply("step/" + stepId, actions);
            var accepted = host.Receive(RuntimeValues.DecodeMessage(step.GetProperty("input")));
            var paused = false;
            var waiting = false;
            if (host.IsReady)
            {
                var pump = step.GetProperty("pump").GetString();
                if (pump == "completion")
                {
                    var execution = host.RunToCompletion();
                    paused = execution.State == GesExecutionState.Paused;
                    waiting = execution.State == GesExecutionState.Waiting;
                }
                else if (pump is "frame" or "frames")
                {
                    var frames = 0;
                    GesExecutionResult execution;
                    do
                    {
                        if (++frames > 1_000_000) throw new InvalidOperationException("MaxFramesPerStep exceeded.");
                        execution = host.ExecuteFrame(step.GetProperty("budget").GetInt32());
                        paused |= execution.State == GesExecutionState.Paused;
                        waiting = execution.State == GesExecutionState.Waiting;
                    }
                    while (pump == "frames" && execution.State == GesExecutionState.Paused);
                }
            }
            var result = observer.Capture();
            scenario.CaptureState(result);
            result["actions"] = scenario.Actions.ToArray();
            result["accepted"] = accepted;
            result["paused"] = paused;
            result["waiting"] = waiting;
            steps.Add(step.GetProperty("id").GetString()!, result);
        }
        return new { initialization, steps };
    }

    internal static GesRuntimeLimits CreateLimits(JsonElement limits)
    {
        var defaults = GesRuntimeLimits.Default;
        int Read(string name, int fallback) => limits.TryGetProperty(name, out var item) ? item.GetInt32() : fallback;
        return new GesRuntimeLimits
        {
            MaxProcessedEventsPerRun = Read("maxProcessedEventsPerRun", defaults.MaxProcessedEventsPerRun),
            MaxQueuedMessagesPerRun = Read("maxQueuedMessagesPerRun", defaults.MaxQueuedMessagesPerRun),
            MaxExecutionSteps = Read("maxExecutionSteps", defaults.MaxExecutionSteps),
            MaxRegisterValues = Read("maxRegisterValues", defaults.MaxRegisterValues),
            MaxLoopIterations = Read("maxLoopIterations", defaults.MaxLoopIterations),
            MaxCallDepth = Read("maxCallDepth", defaults.MaxCallDepth),
            MaxRandomScopeDepth = Read("maxRandomScopeDepth", defaults.MaxRandomScopeDepth),
            MaxRangeItems = Read("maxRangeItems", defaults.MaxRangeItems),
            MaxGeneratedCollectionItems = Read("maxGeneratedCollectionItems", defaults.MaxGeneratedCollectionItems),
            MaxDiceCount = Read("maxDiceCount", defaults.MaxDiceCount),
            MaxDiceSides = Read("maxDiceSides", defaults.MaxDiceSides)
        };
    }

    private Dictionary<string, object> Capture() => new()
    {
        ["local"] = _local.Select(RuntimeValues.EncodeMessage).ToArray(),
        ["outbound"] = _outbound.Select(RuntimeValues.EncodeMessage).ToArray(),
        ["runtimeLimits"] = _limits.ToArray(),
        ["diagnostics"] = _diagnostics.ToArray(),
        ["trace"] = _trace.Select(item => item.ToDictionary(pair => pair.Key, pair => pair.Value is GameEventScriptMessage message ? RuntimeValues.EncodeMessage(message) : pair.Value)).ToArray()
    };

    private void Clear()
    {
        _local.Clear();
        _outbound.Clear();
        _limits.Clear();
        _diagnostics.Clear();
        _trace.Clear();
    }

    /// <summary>Records the attempted emission and its acceptance at the callback position.</summary>
    public void MessageEmitted(GameEventScriptMessage message, bool accepted)
    {
        _local.Add(message);
        _trace.Add(new() { ["event"] = "emit", ["message"] = message, ["accepted"] = accepted });
    }

    /// <summary>Records publication with all final-delivery acceptance flags.</summary>
    public void MessagePublished(GameEventScriptMessage message, GesPublishResult result)
    {
        _local.Add(message);
        _trace.Add(new()
        {
            ["event"] = "publish",
            ["message"] = message,
            ["result"] = new { localAccepted = result.LocalAccepted, outboundAttempted = result.OutboundAttempted, outboundAccepted = result.OutboundAccepted, anyAccepted = result.AnyAccepted }
        });
    }

    /// <summary>Records every outbound attempt before accepting, rejecting or throwing as configured.</summary>
    public bool Publish(GameEventScriptMessage message)
    {
        _outbound.Add(message);
        return publishSink switch
        {
            "accept" => true,
            "reject" => false,
            _ => throw new InvalidOperationException("Configured publish sink failure.")
        };
    }

    /// <summary>Records dispatch entry without serializing message values inside the callback.</summary>
    public void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId)
        => _trace.Add(new() { ["event"] = "dispatchStarted", ["message"] = message, ["signatureId"] = dispatchSignatureId });

    /// <summary>Records dispatch completion in callback order.</summary>
    public void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId)
        => _trace.Add(new() { ["event"] = "dispatchCompleted", ["message"] = message, ["signatureId"] = dispatchSignatureId });

    /// <summary>Records stable limit fields in both limit observations and the callback trace.</summary>
    public void RuntimeLimitReached(string limitName, string detail, int limit)
    {
        var value = new { name = limitName, limit };
        _limits.Add(value);
        _trace.Add(new() { ["event"] = "runtimeLimit", ["runtimeLimit"] = value });
    }

    /// <summary>Records structured diagnostics in both channels, excluding English diagnostic text.</summary>
    public void RuntimeError(GesDiagnostic diagnostic)
    {
        var value = CompilerObservations.Diagnostic(diagnostic);
        _diagnostics.Add(value);
        _trace.Add(new() { ["event"] = "diagnostic", ["diagnostic"] = value });
    }
}
