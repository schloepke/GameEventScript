// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Independent local delivery and outbound publication outcomes.
public struct GesPublishResult: Sendable {
    /// Whether the message was accepted for local delivery.
    public let localAccepted: Bool
    /// Whether the outbound sink was called.
    public let outboundAttempted: Bool
    /// Whether the outbound sink accepted the message.
    public let outboundAccepted: Bool
    /// Whether outbound publication is staged until initialization succeeds.
    public let outboundDeferred: Bool

    /// Creates publication outcome flags; deferred outbound delivery defaults to false.
    public init(localAccepted: Bool, outboundAttempted: Bool, outboundAccepted: Bool, outboundDeferred: Bool = false) {
        self.localAccepted = localAccepted
        self.outboundAttempted = outboundAttempted
        self.outboundAccepted = outboundAccepted
        self.outboundDeferred = outboundDeferred
    }

    /// Whether local, outbound or deferred outbound delivery was accepted.
    public var anyAccepted: Bool { localAccepted || outboundAccepted || outboundDeferred }
}
