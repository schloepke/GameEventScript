#pragma warning disable CS1591 // Public architecture is documented in Documentation/Specification/HostRuntime.md.

namespace StepH.GameEventScript.Api;

/// <summary>
/// Portable synchronous message handler invoked by a <see cref="GameEventScriptHost"/>.
/// </summary>
public interface IGameEventScriptNativeMessageHandler
{
    void Handle(GameEventScriptMessage message, GameEventScriptContext context);
}
