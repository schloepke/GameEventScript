// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

/// Monotonic time source borrowed by a serial host. Values must be nonnegative and never decrease.
public protocol GameEventScriptClock {
    /// Elapsed whole microseconds from a stable clock-local origin. Reading does not pump the host.
    var elapsedMicroseconds: Int64 { get }
}

// Keep the full protocol existential out of every Host. Only explicitly injected
// clocks need this box; default hosts read the system clock without allocating it.
final class GesClockSource {
    let value: any GameEventScriptClock

    init(_ value: any GameEventScriptClock) { self.value = value }
}

enum GesSystemClock {
    static func read() -> Int64 {
        var value = timespec()
        clock_gettime(CLOCK_MONOTONIC, &value)
        return Int64(value.tv_sec) * 1_000_000 + Int64(value.tv_nsec) / 1_000
    }
}
