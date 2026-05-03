#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Runtime;

public abstract class GameEventScriptFatalRuntimeException : Exception
{
    protected GameEventScriptFatalRuntimeException(string message)
        : base(message)
    {
    }

    protected GameEventScriptFatalRuntimeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
