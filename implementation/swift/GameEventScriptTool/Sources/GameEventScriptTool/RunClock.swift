// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

/// Monotonic host clock of a CLI session that also shows host deadlines as local wall-clock times.
final class RunClock: GesClock {
    // Deadlines further away than about a century are shown as a remaining delay.
    private static let maxShownMicroseconds: Int64 = 3_155_760_000_000_000
    private static let timeFormatter = formatter("HH:mm:ss.SSS")
    private static let dateTimeFormatter = formatter("yyyy-MM-dd HH:mm:ss.SSS")
    private let origin = RunClock.monotonicMicroseconds()
    // Wall-clock time of the monotonic origin; deadlines are shown relative to it.
    private let wallOrigin = Date()

    /// Elapsed whole microseconds since the session clock was created.
    var elapsedMicroseconds: Int64 { RunClock.monotonicMicroseconds() - origin }

    // Adds the deadline to the session's start time and shows it in local time.
    func formatDeadline(_ deadline: Int64) -> String {
        if deadline > RunClock.maxShownMicroseconds { return "in \(deadline - elapsedMicroseconds)us" }
        let due = wallOrigin.addingTimeInterval(Double(deadline) / 1_000_000)
        return (Calendar.current.isDateInToday(due) ? RunClock.timeFormatter : RunClock.dateTimeFormatter).string(from: due)
    }

    private static func formatter(_ format: String) -> DateFormatter {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = format
        return formatter
    }

    private static func monotonicMicroseconds() -> Int64 {
        var value = timespec()
        clock_gettime(CLOCK_MONOTONIC, &value)
        return Int64(value.tv_sec) * 1_000_000 + Int64(value.tv_nsec) / 1_000
    }
}
