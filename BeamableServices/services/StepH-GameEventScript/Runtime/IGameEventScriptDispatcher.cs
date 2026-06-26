using System;

namespace StepH.GameEventScript.Runtime;

internal interface IGameEventScriptDispatcher
{
    void Enqueue(Action workItem);
}
