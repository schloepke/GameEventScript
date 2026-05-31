using System.Collections.Generic;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a bindable GameEventScript module that exports message handlers for a host.
/// </summary>
public interface IGameEventScriptModule
{
    /// <summary>
    /// Gets the module name used for diagnostics and debug output.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Gets the message handlers exported by this module.
    /// </summary>
    IEnumerable<GameEventScriptMessageHandlerDescriptor> Handlers { get; }

    /// <summary>
    /// Binds external runtime dependencies required by this module.
    /// </summary>
    void Bind(IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptExternalTypeRegistry typeRegistry);
}
