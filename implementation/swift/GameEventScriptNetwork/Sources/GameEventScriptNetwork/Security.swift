// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// How a client protects its session: with the server's pinned public key, encrypted with a key the server sends during
/// the handshake, or not at all.
public struct GesNetworkClientSecurity: Sendable, Equatable {
    /// The lowest level the client accepts: `GesNetworkSecurity.verified` when pinned, `GesNetworkSecurity.encrypted`
    /// for development, else `GesNetworkSecurity.none`.
    public let level: GesNetworkSecurity
    /// The pinned public key of 32 bytes; empty unless pinned.
    public let serverPublicKey: [UInt8]

    private init(level: GesNetworkSecurity, serverPublicKey: [UInt8]) {
        self.level = level
        self.serverPublicKey = serverPublicKey
    }

    /// The security of a client without encryption of its own, for an in-process transport or one that protects
    /// itself, such as TLS. A server with a key still encrypts the session. C# calls it `None`; Swift avoids `.none`,
    /// which an optional would read as `nil`.
    public static let unencrypted = GesNetworkClientSecurity(level: .none, serverPublicKey: [])

    /// The security of a development client, which takes the key from the server's `retry` and reaches
    /// `GesNetworkSecurity.encrypted`. It protects only against attackers who read but do not change packets.
    public static let development = GesNetworkClientSecurity(level: .encrypted, serverPublicKey: [])

    /// Creates the security of a client that pins the server's public key and reaches `GesNetworkSecurity.verified`.
    /// - Parameter serverPublicKey: The server's public key of 32 bytes.
    /// - Returns: The security.
    /// - Throws: `GesApiError.invalidArgument` when the key does not hold 32 bytes.
    public static func pinned(_ serverPublicKey: [UInt8]) throws -> GesNetworkClientSecurity {
        if serverPublicKey.count != GesNetworkX25519.keyBytes { throw GesApiError.invalidArgument("A server public key holds 32 bytes.") }
        return GesNetworkClientSecurity(level: .verified, serverPublicKey: serverPublicKey)
    }
}

/// How a server protects its sessions: with its static key, or not at all.
public struct GesNetworkServerSecurity: Sendable, Equatable {
    /// The server's key; `nil` for `unencrypted`.
    public let serverKey: GesNetworkKey?

    private init(serverKey: GesNetworkKey?) {
        self.serverKey = serverKey
    }

    /// The security of a server without a key, for an in-process transport or one that protects itself, such as TLS.
    /// It closes a client that asks for encryption with `GesNetworkCodes.insecure`. C# calls it `None`; Swift avoids
    /// `.none`, which an optional would read as `nil`.
    public static let unencrypted = GesNetworkServerSecurity(serverKey: nil)

    /// Creates the security of a server that encrypts every session with its key and never accepts a plain `hello`.
    /// - Parameter key: The server's key.
    /// - Returns: The security.
    public static func key(_ key: GesNetworkKey) -> GesNetworkServerSecurity {
        GesNetworkServerSecurity(serverKey: key)
    }
}
