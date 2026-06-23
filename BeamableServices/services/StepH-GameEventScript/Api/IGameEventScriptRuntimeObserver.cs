#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptRuntimeObserver
{
    void MessageEmitted(GameEventScriptMessage message, bool accepted);

    void MessagePublished(GameEventScriptMessage message, bool accepted);

    void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId);

    void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId);

    void RuntimeLimitReached(string limitName, string detail, int limit);
}
