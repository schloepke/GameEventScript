// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

/// <remarks>Explicit V1 value JSON transport with the same limits as message JSON. Decoding never executes
/// constructors.</remarks>
public partial struct GesValue
{
    /// <summary>Serializes this value in a versioned value envelope, exporting external objects as Records.</summary>
    /// <returns>The canonical V1 JSON value envelope.</returns>
    /// <exception cref="GesMessageFormatException">The graph is unsupported or exceeds limits.</exception>
    public readonly string ToJson()
        => GesJson.Write(GesJson.Object(("version", GesInteger(1)), ("value", new GesMessageJsonCodec().Encode(this, 0))));

    /// <summary>Reads a complete V1 value envelope as data only.</summary>
    /// <param name="json">The complete JSON document.</param>
    /// <returns>The reconstructed value. External snapshots become Records.</returns>
    /// <exception cref="GesMessageFormatException">The envelope is invalid, unsupported or over-limit.</exception>
    public static GesValue FromJson(string json)
        => new GesMessageJsonCodec().Decode(GesMessageJsonCodec.Envelope(GesJson.Read(json), "value"), 0);
}
