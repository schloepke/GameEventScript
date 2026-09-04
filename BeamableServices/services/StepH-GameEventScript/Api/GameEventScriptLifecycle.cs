using StepH.GameEventScript.Runtime.VM;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents one host-local loading of a reusable immutable program.
/// </summary>
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

    /// <summary>
    /// Gets the immutable portable program from which this instance was linked.
    /// </summary>
    public GameEventScriptProgram Program { get; }
    /// <summary>
    /// Gets whether this instance still contributes handlers to future message snapshots.
    /// </summary>
    public bool IsAttached => _host.IsInstanceAttached(_registrationId);
    internal long RegistrationId => _registrationId;
    internal GesLinkedProgram LinkedProgram { get; }
    internal GameEventScriptInstance? NextRegistration { get; set; }

    /// <summary>
    /// Idempotently removes this instance's handlers from future message snapshots.
    /// </summary>
    /// <returns><see langword="true"/> only when this call performed the detach.</returns>
    /// <remarks>Handlers already captured by queued messages continue to run.</remarks>
    public bool Detach() => _host.DetachInstance(_registrationId);
}

/// <summary>
/// Represents one host-local native message subscription.
/// </summary>
public sealed class GameEventScriptSubscription
{
    private readonly GameEventScriptHost _host;
    private readonly long _registrationId;

    internal GameEventScriptSubscription(GameEventScriptHost host, long registrationId)
    {
        _host = host;
        _registrationId = registrationId;
    }

    /// <summary>
    /// Gets whether this subscription still contributes to future message snapshots.
    /// </summary>
    public bool IsSubscribed => _host.IsSubscriptionRegistered(_registrationId);

    /// <summary>
    /// Idempotently removes this handler from future message snapshots.
    /// </summary>
    /// <returns><see langword="true"/> only when this call performed the removal.</returns>
    /// <remarks>Handlers already captured by queued messages continue to run.</remarks>
    public bool Unsubscribe() => _host.Unsubscribe(_registrationId);
}
