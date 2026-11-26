// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>The kind of version a handle type declares.</summary>
public enum GesHandleVersionKind : byte
{
    /// <summary>The type has no versions; every handle means the current state.</summary>
    None,

    /// <summary>An integer revision the owner counts, ascending with each change.</summary>
    Revision,

    /// <summary>A SHA-1 hash of 20 bytes the owner computes over its own representation.</summary>
    Sha1,

    /// <summary>A SHA-256 hash of 32 bytes the owner computes over its own representation.</summary>
    Sha256,

    /// <summary>The content hash of 32 bytes that GES computes over the state.</summary>
    ContentHash
}
