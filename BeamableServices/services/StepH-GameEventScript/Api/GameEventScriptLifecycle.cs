#pragma warning disable CS1591 // Public architecture is documented in HostArchitecture.md.

using StepH.GameEventScript.Runtime.VM;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptInstance
{
    private readonly GameEventScriptHost _host;
    private readonly long _registrationId;

    internal GameEventScriptInstance(GameEventScriptHost host, long registrationId, GameEventScriptProgram program, GesLinkedProgram linkedProgram)
    {
        _host = host;
        _registrationId = registrationId;
        Program = program;
        LinkedProgram = linkedProgram;
    }

    public GameEventScriptProgram Program { get; }
    public bool IsAttached => _host.IsInstanceAttached(_registrationId);
    internal long RegistrationId => _registrationId;
    internal GesLinkedProgram LinkedProgram { get; }
    internal GameEventScriptInstance? NextRegistration { get; set; }

    public bool Detach() => _host.DetachInstance(_registrationId);
}

public sealed class GameEventScriptSubscription
{
    private readonly GameEventScriptHost _host;
    private readonly long _registrationId;

    internal GameEventScriptSubscription(GameEventScriptHost host, long registrationId)
    {
        _host = host;
        _registrationId = registrationId;
    }

    public bool IsSubscribed => _host.IsSubscriptionRegistered(_registrationId);

    public bool Unsubscribe() => _host.Unsubscribe(_registrationId);
}
