#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.Flow.EventScript.Runtime;

public abstract class EventScriptFatalRuntimeException : Exception
{
    protected EventScriptFatalRuntimeException(string message)
        : base(message)
    {
    }

    protected EventScriptFatalRuntimeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
