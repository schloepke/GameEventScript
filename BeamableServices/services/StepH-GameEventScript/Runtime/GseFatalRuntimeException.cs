#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Runtime;

public abstract class GseFatalRuntimeException : Exception
{
    protected GseFatalRuntimeException(string message)
        : base(message)
    {
    }

    protected GseFatalRuntimeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
