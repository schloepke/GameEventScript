#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Runtime;

public sealed class GseDynamicLinkException : Exception
{
    public GseDynamicLinkException(string message)
        : base(message)
    {
    }
}
