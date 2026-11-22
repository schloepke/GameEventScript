// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if !os(WASI)

    import GameEventScriptNetwork
    import GameEventScriptRuntime

    extension GesNetworkClientEnd {
        /// Waits until the client end has work: until its socket received bytes, opened or ended, or the end's
        /// `nextDeadline` passes, whichever is first. Poll after each wait. A wait may end early; it never ends after the
        /// work arrived. A cancelled task ends the wait at once.
        /// - Throws: `GesApiError.invalidArgument` when the end's connector is no `GesNetworkSocketConnector`.
        public func wait() async throws {
            guard let connector = connector as? GesNetworkSocketConnector else {
                throw GesApiError.invalidArgument("The end's connector is no socket connector.")
            }
            await connector.signal.wait(microseconds: gesNetworkSocketRemaining(nextDeadline, clock))
        }
    }

    extension GesNetworkServerEnd {
        /// Waits until the server end has work: until one of its sockets received bytes or ended, a client became ready,
        /// or the end's `nextDeadline` passes, whichever is first. Poll after each wait. A wait may end early; it never
        /// ends after the work arrived. A cancelled task ends the wait at once.
        /// - Throws: `GesApiError.invalidArgument` when the end's acceptor is no `GesNetworkSocketListener`.
        public func wait() async throws {
            guard let listener = acceptor as? GesNetworkSocketListener else {
                throw GesApiError.invalidArgument("The end's acceptor is no socket listener.")
            }
            await listener.signal.wait(microseconds: gesNetworkSocketRemaining(nextDeadline, clock))
        }
    }

    // The microseconds until the deadline; Int64.max for none.
    private func gesNetworkSocketRemaining(_ deadline: Int64, _ clock: any GesClock) -> Int64 {
        deadline == .max ? .max : deadline - clock.elapsedMicroseconds
    }

#endif
