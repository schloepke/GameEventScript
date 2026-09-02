#pragma warning disable CS1591

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Conformance;

public readonly struct ConformanceSourceRange
{
    public ConformanceSourceRange(int byteOffset, int byteLength, int line, int column, int endLine, int endColumn)
    {
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public int ByteOffset { get; }
    public int ByteLength { get; }
    public int Line { get; }
    public int Column { get; }
    public int EndLine { get; }
    public int EndColumn { get; }
}

public sealed class ConformanceDiagnostic
{
    internal ConformanceDiagnostic(string code, string message, ConformanceSourceRange range)
    {
        Code = code;
        Message = message;
        Range = range;
    }

    public string Code { get; }
    public string Message { get; }
    public ConformanceSourceRange Range { get; }
}

public static class ConformanceDiagnosticCodes
{
    public const string InvalidUtf8 = "conformance.markdown.invalidUtf8";
    public const string MissingFrontmatter = "conformance.markdown.missingFrontmatter";
    public const string UnterminatedFrontmatter = "conformance.markdown.unterminatedFrontmatter";
    public const string InvalidTestHeading = "conformance.markdown.invalidTestHeading";
    public const string UnterminatedFence = "conformance.markdown.unterminatedFence";
    public const string UnknownSemanticFence = "conformance.markdown.unknownSemanticFence";
    public const string InvalidStepsTable = "conformance.markdown.invalidStepsTable";
    public const string YamlSyntax = "conformance.yaml.syntax";
    public const string YamlUnsupportedFeature = "conformance.yaml.unsupportedFeature";
    public const string YamlDuplicateKey = "conformance.yaml.duplicateKey";
    public const string YamlInvalidScalar = "conformance.yaml.invalidScalar";
    public const string YamlLimitExceeded = "conformance.yaml.limitExceeded";
    public const string SchemaUnknownField = "conformance.schema.unknownField";
    public const string SchemaMissingField = "conformance.schema.missingField";
    public const string SchemaInvalidValue = "conformance.schema.invalidValue";
    public const string SchemaDuplicateId = "conformance.schema.duplicateId";
    public const string SchemaUnknownReference = "conformance.schema.unknownReference";
    public const string SchemaInvalidCardinality = "conformance.schema.invalidCardinality";
    public const string SchemaUnknownKind = "conformance.schema.unknownKind";
    public const string SchemaUnsupportedVersion = "conformance.schema.unsupportedVersion";
}

public sealed class ConformanceParseException : Exception
{
    internal ConformanceParseException(IReadOnlyList<ConformanceDiagnostic> diagnostics)
        : base(diagnostics.Count == 0 ? "The conformance document is invalid." : diagnostics[0].Message)
    {
        var copy = new ConformanceDiagnostic[diagnostics.Count];
        for (var index = 0; index < diagnostics.Count; index++) copy[index] = diagnostics[index];
        Diagnostics = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<ConformanceDiagnostic> Diagnostics { get; }
}

public sealed class ConformanceParserLimits
{
    public static ConformanceParserLimits Default { get; } = new();

    public int MaxDocumentBytes { get; init; } = 16 * 1024 * 1024;
    public int MaxYamlDepth { get; init; } = 32;
    public int MaxTests { get; init; } = 4096;
    public int MaxSourcesPerTest { get; init; } = 256;
    public int MaxSourceBytesPerTest { get; init; } = 4 * 1024 * 1024;
    public int MaxStepsPerTest { get; init; } = 65535;
    public int MaxScalarBytes { get; init; } = 1024 * 1024;
    public int MaxYamlNodes { get; init; } = 262144;
}
