using System;

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptDispatcher
{
    void Enqueue(Action workItem);
}
