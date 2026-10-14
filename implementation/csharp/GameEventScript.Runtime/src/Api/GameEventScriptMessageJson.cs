// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>Invalid, unsupported, or over-limit portable message/value JSON.</summary>
public sealed class GesMessageFormatException : Exception
{
    /// <summary>Creates a format failure carrying a stable language-neutral code.</summary>
    /// <param name="code">The message.* failure code.</param>
    public GesMessageFormatException(string code) : base(code) { Code = code; }
    /// <summary>The stable machine-readable failure code.</summary>
    public string Code { get; }
}

/// <remarks>Explicit, synchronous V1 product JSON transport; local Publish never invokes it. Preserves ordered message
/// arguments. External objects become immutable Record snapshots. Limits are 64 nested data levels, 65536 data values
/// and 4194304 UTF-16 code units of JSON. Deserialization never executes script or native constructors.</remarks>
public sealed partial class GameEventScriptMessage
{
    /// <summary>Serializes this message and its complete portable data graph as a canonical V1 JSON envelope.</summary>
    /// <returns>The canonical V1 JSON envelope.</returns>
    /// <exception cref="GesMessageFormatException">The graph is unsupported or exceeds limits.</exception>
    public string ToJson()
        => GesJson.Write(GesJson.Object(("version", GesValue.GesInteger(1)), ("message", new GesMessageJsonCodec().EncodeMessage(this, 0))));

    /// <summary>Reads a complete V1 message envelope without invoking constructors or host callbacks.</summary>
    /// <param name="json">The complete JSON document.</param>
    /// <returns>The reconstructed immutable message.</returns>
    /// <exception cref="GesMessageFormatException">The envelope is invalid, unsupported or over-limit.</exception>
    public static GameEventScriptMessage FromJson(string json)
        => new GesMessageJsonCodec().DecodeMessage(GesMessageJsonCodec.Envelope(GesJson.Read(json), "message"), 0);
}
