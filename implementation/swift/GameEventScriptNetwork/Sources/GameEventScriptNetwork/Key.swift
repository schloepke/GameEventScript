// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// A server's static X25519 key pair, which encrypts its sessions. The client receives the public key from outside the
/// protocol and pins it. Immutable; safe to share between connections.
public struct GesNetworkKey: Sendable, Equatable {
    let privateKey: [UInt8]

    /// The public key of 32 bytes, which the client pins.
    public let publicKey: [UInt8]

    private init(validated privateKey: [UInt8]) {
        self.privateKey = privateKey
        var publicKey = [UInt8](repeating: 0, count: GesNetworkX25519.keyBytes)
        privateKey.withUnsafeBytes { secret in
            publicKey.withUnsafeMutableBytes { GesNetworkX25519.publicKey(secret, into: $0) }
        }
        self.publicKey = publicKey
    }

    /// Creates a key pair from a private key; X25519 clamps it.
    /// - Parameter privateKey: The private key of 32 bytes.
    /// - Throws: `GesApiError.invalidArgument` when the private key does not hold 32 bytes.
    public init(privateKey: [UInt8]) throws {
        if privateKey.count != GesNetworkX25519.keyBytes { throw GesApiError.invalidArgument("A private key holds 32 bytes.") }
        self.init(validated: privateKey)
    }

    /// Generates a key pair from the system's cryptographic random generator.
    /// - Returns: The new key pair.
    public static func generate() -> GesNetworkKey {
        var generator = SystemRandomNumberGenerator()
        var privateKey = [UInt8](repeating: 0, count: GesNetworkX25519.keyBytes)
        for index in privateKey.indices { privateKey[index] = generator.next() }
        return GesNetworkKey(validated: privateKey)
    }

    /// Copies the private key of 32 bytes, for example to store it.
    /// - Parameter destination: The target, at least 32 bytes.
    /// - Throws: `GesApiError.invalidArgument` when the target holds fewer than 32 bytes.
    public func copyPrivateKey(into destination: UnsafeMutableRawBufferPointer) throws {
        if destination.count < GesNetworkX25519.keyBytes { throw GesApiError.invalidArgument("The target holds at least 32 bytes.") }
        destination.copyBytes(from: privateKey)
    }

    /// Whether both key pairs hold the same private key; the comparison takes the same time for every key.
    public static func == (lhs: GesNetworkKey, rhs: GesNetworkKey) -> Bool {
        var difference: UInt8 = 0
        for index in 0..<GesNetworkX25519.keyBytes { difference |= lhs.privateKey[index] ^ rhs.privateKey[index] }
        return difference == 0
    }
}
