// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import NIOConcurrencyHelpers
import NIOCore
import NIOPosix

// Wakes the task that waits for an end when a socket of the end received bytes, opened, ended or a client became
// ready. A wake may be spurious; a missed one cannot happen, because a set without a waiter is kept for the next wait,
// and the waiter polls after every wait.
final class GesNetworkSocketSignal: @unchecked Sendable {
    // A longer wait ends early, which a waiter tolerates, so its time cannot overflow.
    private static let longestWait: Int64 = 86_400_000_000

    private let lock = NIOLock()
    // Guarded by the lock.
    private var pending = false
    private var waiter: CheckedContinuation<Void, Never>?
    private var timeout: Scheduled<Void>?
    // Tells a timeout or cancellation of an earlier wait from the current one.
    private var generation: UInt64 = 0

    func set() {
        let (waiter, timeout): (CheckedContinuation<Void, Never>?, Scheduled<Void>?) = lock.withLock {
            guard let waiter = self.waiter else {
                pending = true
                return (nil, nil)
            }
            return take(waiter)
        }
        timeout?.cancel()
        waiter?.resume()
    }

    // Waits until the signal is set, the time passes or the task is cancelled; Int64.max waits without a time limit.
    func wait(microseconds: Int64) async {
        if microseconds <= 0 || Task.isCancelled {
            lock.withLock { pending = false }
            return
        }

        let generation: UInt64 = lock.withLock {
            self.generation &+= 1
            return self.generation
        }
        await withTaskCancellationHandler {
            await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
                let resume: Bool = lock.withLock {
                    // A set before the wait, or a cancellation that already ran, ends it at once.
                    if pending || self.generation != generation {
                        pending = false
                        return true
                    }
                    waiter = continuation
                    if microseconds != .max {
                        timeout = MultiThreadedEventLoopGroup.singleton.next().scheduleTask(in: .microseconds(min(microseconds, Self.longestWait))) { [self] in
                            wake(generation)
                        }
                    }
                    return false
                }
                if resume { continuation.resume() }
            }
        } onCancel: {
            wake(generation)
        }
    }

    // Ends the wait of the generation if it still runs, or keeps it from starting.
    private func wake(_ generation: UInt64) {
        let (waiter, timeout): (CheckedContinuation<Void, Never>?, Scheduled<Void>?) = lock.withLock {
            if self.generation != generation { return (nil, nil) }
            self.generation &+= 1
            guard let waiter = self.waiter else { return (nil, nil) }
            return take(waiter)
        }
        timeout?.cancel()
        waiter?.resume()
    }

    // Removes the waiter and its timeout; called under the lock.
    private func take(_ waiter: CheckedContinuation<Void, Never>) -> (CheckedContinuation<Void, Never>?, Scheduled<Void>?) {
        let timeout = self.timeout
        self.waiter = nil
        self.timeout = nil
        return (waiter, timeout)
    }
}
