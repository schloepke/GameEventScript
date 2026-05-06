using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Provides repeatable, index-addressed terms for a potentially unbounded numeric or value series.
/// </summary>
public interface IGameEventScriptSeries
{
    /// <summary>
    /// Gets a stable identifier for this series definition and its arguments.
    /// </summary>
    string SignatureId { get; }

    /// <summary>
    /// Attempts to evaluate the term at the supplied zero-based index.
    /// </summary>
    bool TryGetTerm(long index, out GameEventScriptValue value);
}
