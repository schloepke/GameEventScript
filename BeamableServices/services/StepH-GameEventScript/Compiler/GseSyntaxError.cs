using StepH.GameEventScript.Compiler;

namespace StepH.GameEventScript;

public sealed record GseSyntaxError(string Message, string ModuleName, GseSourceLocation SourceLocation)
{
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

