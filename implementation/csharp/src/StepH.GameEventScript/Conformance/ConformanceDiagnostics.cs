// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Conformance;

/// <summary>
/// Represents a conformance source range.
/// </summary>
public readonly struct ConformanceSourceRange
{
    /// <summary>
    /// Initializes a new instance of Conformance Source Range.
    /// </summary>
    /// <param name="byteOffset">The byte offset value.</param>
    /// <param name="byteLength">The byte length value.</param>
    /// <param name="line">The line value.</param>
    /// <param name="column">The column value.</param>
    /// <param name="endLine">The end line value.</param>
    /// <param name="endColumn">The end column value.</param>
    public ConformanceSourceRange(int byteOffset, int byteLength, int line, int column, int endLine, int endColumn)
    {
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    /// <summary>
    /// Gets the byte offset.
    /// </summary>
    public int ByteOffset { get; }
    /// <summary>
    /// Gets the byte length.
    /// </summary>
    public int ByteLength { get; }
    /// <summary>
    /// Gets the line.
    /// </summary>
    public int Line { get; }
    /// <summary>
    /// Gets the column.
    /// </summary>
    public int Column { get; }
    /// <summary>
    /// Gets the end line.
    /// </summary>
    public int EndLine { get; }
    /// <summary>
    /// Gets the end column.
    /// </summary>
    public int EndColumn { get; }
}

/// <summary>
/// Represents a conformance diagnostic.
/// </summary>
public sealed class ConformanceDiagnostic
{
    internal ConformanceDiagnostic(string code, string message, ConformanceSourceRange range)
    {
        Code = code;
        Message = message;
        Range = range;
    }

    /// <summary>
    /// Gets the code.
    /// </summary>
    public string Code { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public string Message { get; }
    /// <summary>
    /// Gets the range.
    /// </summary>
    public ConformanceSourceRange Range { get; }
}

/// <summary>
/// Represents a conformance diagnostic codes.
/// </summary>
public static class ConformanceDiagnosticCodes
{
    /// <summary>
    /// Defines the invalid utf8 value.
    /// </summary>
    public const string InvalidUtf8 = "conformance.markdown.invalidUtf8";
    /// <summary>
    /// Defines the missing frontmatter value.
    /// </summary>
    public const string MissingFrontmatter = "conformance.markdown.missingFrontmatter";
    /// <summary>
    /// Defines the unterminated frontmatter value.
    /// </summary>
    public const string UnterminatedFrontmatter = "conformance.markdown.unterminatedFrontmatter";
    /// <summary>
    /// Defines the invalid test heading value.
    /// </summary>
    public const string InvalidTestHeading = "conformance.markdown.invalidTestHeading";
    /// <summary>
    /// Defines the unterminated fence value.
    /// </summary>
    public const string UnterminatedFence = "conformance.markdown.unterminatedFence";
    /// <summary>
    /// Defines the unknown semantic fence value.
    /// </summary>
    public const string UnknownSemanticFence = "conformance.markdown.unknownSemanticFence";
    /// <summary>
    /// Defines the invalid steps table value.
    /// </summary>
    public const string InvalidStepsTable = "conformance.markdown.invalidStepsTable";
    /// <summary>
    /// Defines the yaml syntax value.
    /// </summary>
    public const string YamlSyntax = "conformance.yaml.syntax";
    /// <summary>
    /// Defines the yaml unsupported feature value.
    /// </summary>
    public const string YamlUnsupportedFeature = "conformance.yaml.unsupportedFeature";
    /// <summary>
    /// Defines the yaml duplicate key value.
    /// </summary>
    public const string YamlDuplicateKey = "conformance.yaml.duplicateKey";
    /// <summary>
    /// Defines the yaml invalid scalar value.
    /// </summary>
    public const string YamlInvalidScalar = "conformance.yaml.invalidScalar";
    /// <summary>
    /// Defines the yaml limit exceeded value.
    /// </summary>
    public const string YamlLimitExceeded = "conformance.yaml.limitExceeded";
    /// <summary>
    /// Defines the schema unknown field value.
    /// </summary>
    public const string SchemaUnknownField = "conformance.schema.unknownField";
    /// <summary>
    /// Defines the schema missing field value.
    /// </summary>
    public const string SchemaMissingField = "conformance.schema.missingField";
    /// <summary>
    /// Defines the schema invalid value value.
    /// </summary>
    public const string SchemaInvalidValue = "conformance.schema.invalidValue";
    /// <summary>
    /// Defines the schema duplicate id value.
    /// </summary>
    public const string SchemaDuplicateId = "conformance.schema.duplicateId";
    /// <summary>
    /// Defines the schema unknown reference value.
    /// </summary>
    public const string SchemaUnknownReference = "conformance.schema.unknownReference";
    /// <summary>
    /// Defines the schema invalid cardinality value.
    /// </summary>
    public const string SchemaInvalidCardinality = "conformance.schema.invalidCardinality";
    /// <summary>
    /// Defines the schema unknown kind value.
    /// </summary>
    public const string SchemaUnknownKind = "conformance.schema.unknownKind";
    /// <summary>
    /// Defines the schema unsupported version value.
    /// </summary>
    public const string SchemaUnsupportedVersion = "conformance.schema.unsupportedVersion";
}

/// <summary>
/// Represents a conformance parse exception.
/// </summary>
public sealed class ConformanceParseException : Exception
{
    internal ConformanceParseException(IReadOnlyList<ConformanceDiagnostic> diagnostics)
        : base(diagnostics.Count == 0 ? "The conformance document is invalid." : diagnostics[0].Message)
    {
        var copy = new ConformanceDiagnostic[diagnostics.Count];
        for (var index = 0; index < diagnostics.Count; index++) copy[index] = diagnostics[index];
        Diagnostics = Array.AsReadOnly(copy);
    }

    /// <summary>
    /// Gets the diagnostics.
    /// </summary>
    public IReadOnlyList<ConformanceDiagnostic> Diagnostics { get; }
}

/// <summary>
/// Represents a conformance parser limits.
/// </summary>
public sealed class ConformanceParserLimits
{
    /// <summary>
    /// Gets the default.
    /// </summary>
    public static ConformanceParserLimits Default { get; } = new();

    /// <summary>
    /// Gets the max document bytes.
    /// </summary>
    public int MaxDocumentBytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>
    /// Gets the max yaml depth.
    /// </summary>
    public int MaxYamlDepth { get; init; } = 32;
    /// <summary>
    /// Gets the max tests.
    /// </summary>
    public int MaxTests { get; init; } = 4096;
    /// <summary>
    /// Gets the max sources per test.
    /// </summary>
    public int MaxSourcesPerTest { get; init; } = 256;
    /// <summary>
    /// Gets the max source bytes per test.
    /// </summary>
    public int MaxSourceBytesPerTest { get; init; } = 4 * 1024 * 1024;
    /// <summary>
    /// Gets the max steps per test.
    /// </summary>
    public int MaxStepsPerTest { get; init; } = 65535;
    /// <summary>
    /// Gets the max hosts per test.
    /// </summary>
    public int MaxHostsPerTest { get; init; } = 256;
    /// <summary>
    /// Gets the max scalar bytes.
    /// </summary>
    public int MaxScalarBytes { get; init; } = 1024 * 1024;
    /// <summary>
    /// Gets the max yaml nodes.
    /// </summary>
    public int MaxYamlNodes { get; init; } = 262144;
}
