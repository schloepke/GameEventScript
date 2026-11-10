// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Generator;

/// <summary>A target language of generated schema code.</summary>
public enum GesSchemaLanguage
{
    /// <summary>C# 9, which Unity compiles.</summary>
    CSharp,

    /// <summary>Swift 6.</summary>
    Swift
}

/// <summary>The visibility of generated types.</summary>
public enum GesSchemaAccess
{
    /// <summary>Visible to every module.</summary>
    Public,

    /// <summary>Visible within the module that compiles the generated code.</summary>
    Internal
}

/// <summary>How schema code is generated.</summary>
public sealed class GesSchemaGenerateOptions
{
    /// <summary>Gets or sets the target language.</summary>
    public GesSchemaLanguage Language { get; set; } = GesSchemaLanguage.CSharp;

    /// <summary>
    /// Gets or sets the namespace of the generated types, dot-separated identifiers or empty for none. C# declares it as a
    /// namespace and Swift as nested enums, so that generated names cannot collide with types of the app.
    /// </summary>
    public string Namespace { get; set; } = "";

    /// <summary>Gets or sets the visibility of the generated types.</summary>
    public GesSchemaAccess Access { get; set; } = GesSchemaAccess.Public;

    /// <summary>
    /// Gets or sets whether records also convert to and from GES values and messages, through the schema sources the code
    /// embeds and binds once per type. The conversion allocates and is meant for values from scripts, not the hot path.
    /// </summary>
    public bool Values { get; set; }

    /// <summary>
    /// Gets or sets whether generated model types also offer themselves to scripts as external types: a value conversion per
    /// type and a generated <c>GesTypes</c> catalog and constructor registry that implement the Runtime's external-type
    /// contracts, without the Bridge, macros or Reflection. Only model generation takes it.
    /// </summary>
    public bool Bindings { get; set; }

    /// <summary>
    /// Gets or sets whether generated Swift classes keep the dynamic exclusivity checks of their stored properties. By
    /// default they mark them <c>@exclusivity(unchecked)</c>, which halves the time of reading and writing; overlapping
    /// accesses to one record, such as passing a list <c>inout</c> while the record reads into it, are then undefined
    /// instead of a trap. C# has no such checks, so this does not change C# output.
    /// </summary>
    public bool CheckedExclusivity { get; set; }
}

/// <summary>
/// A generation request that cannot be carried out, such as a name that two generated members share. Callers branch on
/// <see cref="Code"/>, a stable <c>generate.*</c> code; the message is informative text.
/// </summary>
public sealed class GesGeneratorException : Exception
{
    internal const string Usage = "generate.usage";
    internal const string NameCollision = "generate.nameCollision";
    internal const string InvalidNamespace = "generate.invalidNamespace";
    internal const string Unsupported = "generate.unsupported";

    internal GesGeneratorException(string code, string message) : base(message) => Code = code;

    /// <summary>Gets the stable <c>generate.*</c> code.</summary>
    public string Code { get; }
}
