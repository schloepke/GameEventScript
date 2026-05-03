namespace StepH.GameEventScript.Compiler;

/// <summary>
/// Represents a specific location in a source file or module for the Game Event Script (GSE) compiler.
/// This class is used to pinpoint the origin of a particular piece of code or error within a source context.
/// </summary>
/// <param name="SourceName">
/// The name of the source file where the location resides.
/// </param>
/// <param name="Line">
/// The starting line number of the location. This value is optional and may be null if the line is not specified.
/// </param>
/// <param name="Column">
/// The starting column number of the location. This value is optional and may be null if the column is not specified.
/// </param>
/// <param name="EndLine">
/// The ending line number of the location. This value is optional and may be null if the range is not specified or is single-line.
/// </param>
/// <param name="EndColumn">
/// The ending column number of the location. This value is optional and may be null if the range is not specified or is single-line.
/// </param>
/// <param name="ModuleName">
/// The name of the GSE module where the location resides. Defaults to "UnknownModule" if not explicitly provided.
/// </param>
public sealed record GseSourceLocation(string SourceName, int? Line = null, int? Column = null, int? EndLine = null, int? EndColumn = null, string ModuleName = "UnknownModule")
{
    /// <summary>
    /// Returns a string representation of the GseSourceLocation object, including the module name,
    /// source name, and optionally, line and column information. If the location spans multiple
    /// lines or columns, the range is included.
    /// </summary>
    /// <returns>
    /// A string describing the source location. The format includes the module name and source name
    /// followed by line and column information when available. If line and column ranges are
    /// present, they are represented appropriately in the output.
    /// </returns>
    public override string ToString()
    {
        var locationName = string.IsNullOrWhiteSpace(ModuleName) ? SourceName : $"{ModuleName}@{SourceName}";
        if (Line is null && Column is null) return locationName;
        if (Column is null) return $"{locationName} (line {Line})";
        if (EndLine is null && EndColumn is null || EndLine == Line && EndColumn == Column) return $"{locationName} (line {Line}, col {Column})";
        return EndLine == Line ? $"{locationName} (line {Line}, col {Column}-{EndColumn})" : $"{locationName} (line {Line}, col {Column} to line {EndLine}, col {EndColumn})";
    }
}