// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>Invalid, unsupported, or over-limit portable message/value JSON.</summary>
public sealed class GameEventScriptMessageFormatException : Exception
{
    /// <summary>Creates a format failure carrying a stable language-neutral code.</summary>
    /// <param name="code">The message.* failure code.</param>
    public GameEventScriptMessageFormatException(string code) : base(code) { Code = code; }
    /// <summary>The stable machine-readable failure code.</summary>
    public string Code { get; }
}

/// <summary>Explicit, synchronous V1 product JSON transport. Local Publish never invokes this codec.</summary>
/// <remarks>Preserves ordered message arguments. External objects become immutable Record snapshots.
/// Limits are 64 nested data levels, 65536 data values and 4194304 UTF-16 code units of JSON.
/// Deserialization never executes script or native constructors.</remarks>
public static class GameEventScriptMessageJson
{
    /// <summary>Serializes a message and its complete portable data graph.</summary>
    /// <param name="message">The message to encode.</param>
    /// <returns>The canonical V1 JSON envelope.</returns>
    /// <exception cref="GameEventScriptMessageFormatException">The graph is unsupported or exceeds limits.</exception>
    public static string Serialize(GameEventScriptMessage message)
        => GesJson.Write(GesJson.Object(("version", GesValue.GesInteger(1)), ("message", new GesMessageJsonCodec().EncodeMessage(message ?? throw new ArgumentNullException(nameof(message)), 0))));

    /// <summary>Reads a complete V1 message envelope without invoking constructors or host callbacks.</summary>
    /// <param name="json">The complete JSON document.</param>
    /// <returns>The reconstructed immutable message.</returns>
    /// <exception cref="GameEventScriptMessageFormatException">The envelope is invalid, unsupported or over-limit.</exception>
    public static GameEventScriptMessage Deserialize(string json)
    {
        var root = GesMessageJsonCodec.Envelope(GesJson.Read(json), "message");
        return new GesMessageJsonCodec().DecodeMessage(root, 0);
    }

    /// <summary>Serializes one value in a versioned value envelope, exporting external objects as Records.</summary>
    /// <param name="value">The portable value or external snapshot source.</param>
    /// <returns>The canonical V1 JSON value envelope.</returns>
    /// <exception cref="GameEventScriptMessageFormatException">The graph is unsupported or exceeds limits.</exception>
    public static string SerializeValue(GesValue value)
        => GesJson.Write(GesJson.Object(("version", GesValue.GesInteger(1)), ("value", new GesMessageJsonCodec().Encode(value, 0))));

    /// <summary>Reads a complete V1 value envelope as data only.</summary>
    /// <param name="json">The complete JSON document.</param>
    /// <returns>The reconstructed value. External snapshots become Records.</returns>
    /// <exception cref="GameEventScriptMessageFormatException">The envelope is invalid, unsupported or over-limit.</exception>
    public static GesValue DeserializeValue(string json)
        => new GesMessageJsonCodec().Decode(GesMessageJsonCodec.Envelope(GesJson.Read(json), "value"), 0);
}
