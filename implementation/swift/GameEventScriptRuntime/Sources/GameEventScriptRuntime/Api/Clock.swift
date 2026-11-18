// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

/// Monotonic time source borrowed by a serial host or a network end. Values must be nonnegative and never decrease.
public protocol GesClock {
    /// Elapsed whole microseconds from a stable clock-local origin. Reading does not pump the host.
    var elapsedMicroseconds: Int64 { get }
}

// Keep the full protocol existential out of every Host. Only explicitly injected
// clocks need this box; default hosts read the system clock without allocating it.
final class GesClockSource {
    let value: any GesClock

    init(_ value: any GesClock) { self.value = value }
}

/// The system monotonic clock, measured from the moment the value was created. Reading allocates nothing and is
/// unaffected by changes of the wall-clock time. A copy keeps the origin, so hosts and network ends that use one timeline
/// share one value.
public struct GesSystemClock: GesClock, Sendable {
    private let origin: Int64

    /// Creates a clock whose time starts at zero now.
    public init() { origin = Self.read() }

    /// The whole microseconds elapsed since the clock was created.
    public var elapsedMicroseconds: Int64 { Self.read() - origin }

    static func read() -> Int64 {
        var value = timespec()
        clock_gettime(CLOCK_MONOTONIC, &value)
        return Int64(value.tv_sec) * 1_000_000 + Int64(value.tv_nsec) / 1_000
    }
}

/// A virtual clock for deterministic simulation and tests: its time changes only through `advance(by:)`. Not `Sendable`;
/// one caller at a time, like the host or end that reads it.
public final class GesVirtualClock: GesClock {
    /// The current virtual time in whole microseconds.
    public private(set) var elapsedMicroseconds: Int64

    /// Creates a virtual clock at time zero.
    public init() { elapsedMicroseconds = 0 }

    /// Creates a virtual clock at a starting time.
    /// - Parameter elapsedMicroseconds: The starting time in whole microseconds.
    /// - Throws: `GesApiError.invalidArgument` when the time is negative.
    public init(elapsedMicroseconds: Int64) throws(GesApiError) {
        if elapsedMicroseconds < 0 { throw GesApiError.invalidArgument("The time cannot be negative.") }
        self.elapsedMicroseconds = elapsedMicroseconds
    }

    /// Moves the virtual time forward. Advancing reads no clock and pumps nothing.
    /// - Parameter microseconds: The whole microseconds to add.
    /// - Throws: `GesApiError.invalidArgument` when the amount is negative or the time would exceed `Int64.max`.
    public func advance(by microseconds: Int64) throws(GesApiError) {
        if microseconds < 0 { throw GesApiError.invalidArgument("Time does not go back.") }
        if microseconds > Int64.max - elapsedMicroseconds { throw GesApiError.invalidArgument("The time exceeds the Int64 range.") }
        elapsedMicroseconds += microseconds
    }
}
