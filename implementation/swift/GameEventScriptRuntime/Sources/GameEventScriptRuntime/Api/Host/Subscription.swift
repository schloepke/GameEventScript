// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An idempotently detachable native subscription identified within its owning host.
public final class GameEventScriptSubscription {
    private weak var host: GameEventScriptHost?
    /// Stable registration identifier, unique within the owning host.
    public let registrationID: Int64
    /// Whether the host still registers this native handler.
    public var isSubscribed: Bool { host?.isSubscribed(registrationID) ?? false }

    init(host: GameEventScriptHost, id: Int64) {
        self.host = host
        registrationID = id
    }

    /// Removes this handler from future subscription snapshots. Returns true only when this call removed it; already
    /// captured deliveries remain valid.
    @discardableResult public func unsubscribe() -> Bool { host?.unsubscribe(registrationID) ?? false }
}
