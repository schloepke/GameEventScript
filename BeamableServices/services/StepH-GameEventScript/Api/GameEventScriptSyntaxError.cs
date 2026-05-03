namespace StepH.GameEventScript.Api;

/// <summary>
/// Describes a syntax error reported while parsing GameEventScript source.
/// </summary>
/// <param name="Message">Human-readable syntax error message.</param>
/// <param name="ModuleName">Module name associated with the source that produced the error.</param>
/// <param name="SourceLocation">Precise source location for the error when available.</param>
public sealed record GameEventScriptSyntaxError(string Message, string ModuleName, GameEventScriptSourceLocation SourceLocation)
{
    /// <summary>
    /// Formats the syntax error with its module and source location.
    /// </summary>
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}
