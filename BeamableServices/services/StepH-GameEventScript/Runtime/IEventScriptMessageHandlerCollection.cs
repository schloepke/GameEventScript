#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Runtime;

public interface IEventScriptMessageHandlerCollection
{
    IEnumerable<(EventScriptMessageSignature Signature, Action<EventScriptMessage, EventScriptContext> Handler)> Handlers { get; }
}
