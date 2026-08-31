#pragma warning disable CS1591 // Public architecture is documented in HostArchitecture.md.

using System;
using StepH.GameEventScript.Runtime.VM;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptInstance
{
    private Func<bool>? _detach;

    internal GameEventScriptInstance(GameEventScriptProgram program, GesLinkedProgram linkedProgram)
    {
        Program = program;
        LinkedProgram = linkedProgram;
    }

    public GameEventScriptProgram Program { get; }
    public bool IsAttached => _detach is not null;
    internal GesLinkedProgram LinkedProgram { get; }

    internal void Attach(Func<bool> detach) => _detach = detach;

    public bool Detach()
    {
        var detach = _detach;
        if (detach is null) return false;
        _detach = null;
        return detach();
    }
}

public sealed class GameEventScriptSubscription
{
    private Func<bool>? _unsubscribe;

    internal GameEventScriptSubscription(Func<bool> unsubscribe)
        => _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));

    public bool IsSubscribed => _unsubscribe is not null;

    public bool Unsubscribe()
    {
        var unsubscribe = _unsubscribe;
        if (unsubscribe is null) return false;
        _unsubscribe = null;
        return unsubscribe();
    }
}
