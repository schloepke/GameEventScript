using System;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Schedules host dispatch work for automatic message processing.
/// </summary>
/// <remarks>
/// The GameEventScript runtime core is sequential and does not synchronize access internally. Bridges
/// that execute dispatcher work on workers or background threads should pair the dispatcher with an
/// <see cref="IGameEventScriptRuntimeGate"/>.
/// </remarks>
public interface IGameEventScriptDispatcher
{
    /// <summary>
    /// Enqueues one dispatch work item.
    /// </summary>
    /// <param name="workItem">The work item to execute.</param>
    void Enqueue(Action workItem);
}
