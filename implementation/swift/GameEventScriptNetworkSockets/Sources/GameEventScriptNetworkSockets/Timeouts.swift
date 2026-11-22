// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if !os(WASI)

    import NIOCore

    // The time limits of the socket transports, equal in both ports.
    enum GesNetworkSocketTimeouts {
        // A TCP connection opens within this.
        static let connect = TimeAmount.seconds(10)

        // Connecting, TLS and the WebSocket upgrade together complete within this, on a client and on a listener.
        static let upgrade = TimeAmount.seconds(10)

        // A disposed session of a closed connection writes its last packets and ends in order within this.
        static let orderlyEnd = TimeAmount.seconds(10)
    }

#endif
