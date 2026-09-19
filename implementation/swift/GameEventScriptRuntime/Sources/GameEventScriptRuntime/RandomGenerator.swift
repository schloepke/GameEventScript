// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host-owned xoshiro256** stream with deterministic SplitMix64 seeding and nested scopes.
/// This mutable object is serial and deliberately does not conform to Sendable.
public final class GameEventScriptRandomGenerator {
    struct State {
        var sequence: [Double] = []
        var sequenceIndex = 0
        var s0: UInt64, s1: UInt64, s2: UInt64, s3: UInt64
        init(seed: Int64, sequence: [Double] = []) {
            var mix = UInt64(bitPattern: seed)
            s0 = splitMix(&mix)
            s1 = splitMix(&mix)
            s2 = splitMix(&mix)
            s3 = splitMix(&mix)
            if s0 | s1 | s2 | s3 == 0 { s0 = 0x9e37_79b9_7f4a_7c15 }
            self.sequence = sequence
        }
    }
    enum BoundaryFault { case none, limitExceeded, boundaryUnderflow, unbalanced }
    private var state: State
    private let maxScopeDepth: Int
    private var parents: [State?] = []
    private var boundaries: [(token: Int, depth: Int)] = []
    private var boundaryDepth = 0
    private var nextToken = 0
    private var scopeDepth = 0
    private var faultState: State?
    private var faultOwner = 0
    private var overpushDepth = 0
    private var fault: BoundaryFault = .none
    var hasActiveScopeFault: Bool { faultState != nil }

    public convenience init(seed: Int64) { self.init(seed: seed, sequence: [], maxScopeDepth: 16) }
    public convenience init(sequence: [Double], fallbackSeed: Int64? = nil) {
        self.init(seed: fallbackSeed ?? Self.entropySeed(), sequence: sequence, maxScopeDepth: 16)
    }
    public convenience init(entropy: [UInt8]) throws { self.init(seed: try Self.seedFromEntropy(entropy)) }
    public convenience init() { self.init(seed: Self.entropySeed()) }
    init(seed: Int64, sequence: [Double], maxScopeDepth: Int) {
        state = State(seed: seed, sequence: sequence)
        self.maxScopeDepth = maxScopeDepth
    }
    public static func seedFromEntropy(_ entropy: [UInt8]) throws -> Int64 {
        guard !entropy.isEmpty else { throw GameEventScriptAPIError.invalidArgument("Entropy must not be empty") }
        var state: UInt64 = 0x9e37_79b9_7f4a_7c15
        for byte in entropy {
            var mix = state ^ UInt64(byte)
            state = splitMix(&mix)
        }
        return Int64(bitPattern: state)
    }
    static func entropySeed() -> Int64 {
        var source = SystemRandomNumberGenerator()
        var bytes: [UInt8] = []
        for _ in 0..<2 {
            let word = source.next()
            for shift in stride(from: 0, to: 64, by: 8) { bytes.append(UInt8(truncatingIfNeeded: word >> shift)) }
        }
        // Nonempty by construction; entropy mixing has no other failure path.
        return try! seedFromEntropy(bytes)
    }
    @discardableResult public func push(seed: Int64? = nil) -> Bool {
        if hasActiveScopeFault {
            overpushDepth += 1
            return false
        }
        if scopeDepth >= maxScopeDepth {
            enterFault(.limitExceeded, overpush: 1)
            return false
        }
        if parents.isEmpty { parents = [State?](repeating: nil, count: maxScopeDepth) }
        parents[scopeDepth] = state
        scopeDepth += 1
        if let seed { state = State(seed: seed) }
        return true
    }
    @discardableResult public func pop() -> Bool {
        if hasActiveScopeFault {
            if overpushDepth > 0 { overpushDepth -= 1 }
            if faultOwner == 0 && overpushDepth == 0 { clearFault() }
            return false
        }
        let floor = boundaryDepth == 0 ? 0 : boundaries[boundaryDepth - 1].depth
        if scopeDepth <= floor {
            if boundaryDepth > 0 { enterFault(.boundaryUnderflow, overpush: 0) }
            return false
        }
        scopeDepth -= 1
        state = parents[scopeDepth]!
        parents[scopeDepth] = nil
        return true
    }
    public func nextInclusiveInteger(_ first: Int64, _ second: Int64) -> Int64 {
        let lower = min(first, second)
        let upper = max(first, second)
        if lower == upper { return lower }
        if let value = dequeue() { return min(max(GesNumber.saturatedInteger(value), lower), upper) }
        let span = UInt64(bitPattern: upper &- lower) &+ 1
        return lower &+ Int64(bitPattern: nextBelow(span))
    }
    public func nextFloat(_ first: Double, _ second: Double) -> Double {
        if first.isNaN || second.isNaN { return .nan }
        let lower = min(first, second)
        let upper = max(first, second)
        if lower == upper { return lower }
        if let value = dequeue() {
            if value.isNaN { return .nan }
            return min(max(value, lower), upper)
        }
        let unit = Double(nextUInt64() >> 11) * (1.0 / 9007199254740992.0)
        let scaled = (upper - lower) * unit
        return lower + scaled
    }
    func markBoundary() -> Int {
        nextToken = nextToken &+ 1
        if nextToken == 0 { nextToken = nextToken &+ 1 }
        if boundaryDepth == boundaries.count {
            boundaries.append((nextToken, scopeDepth))
        } else {
            boundaries[boundaryDepth] = (nextToken, scopeDepth)
        }
        boundaryDepth += 1
        return nextToken
    }
    func releaseBoundary(_ token: Int) -> BoundaryFault {
        precondition(boundaryDepth > 0 && boundaries[boundaryDepth - 1].token == token)
        boundaryDepth -= 1
        let depth = boundaries[boundaryDepth].depth
        var result: BoundaryFault = .none
        if hasActiveScopeFault && faultOwner == token {
            result = fault
            clearFault()
        }
        if scopeDepth != depth && result == .none { result = .unbalanced }
        while scopeDepth > depth {
            scopeDepth -= 1
            state = parents[scopeDepth]!
            parents[scopeDepth] = nil
        }
        return result
    }
    private func enterFault(_ fault: BoundaryFault, overpush: Int) {
        faultState = state
        self.fault = fault
        overpushDepth = overpush
        faultOwner = boundaryDepth == 0 ? 0 : boundaries[boundaryDepth - 1].token
    }
    private func clearFault() {
        state = faultState!
        faultState = nil
        faultOwner = 0
        overpushDepth = 0
        fault = .none
    }
    private func dequeue() -> Double? {
        if state.sequenceIndex >= state.sequence.count { return nil }
        defer { state.sequenceIndex += 1 }
        return state.sequence[state.sequenceIndex]
    }
    private func nextUInt64() -> UInt64 {
        let result = Self.rotate(state.s1 &* 5, 7) &* 9
        let t = state.s1 << 17
        state.s2 ^= state.s0
        state.s3 ^= state.s1
        state.s1 ^= state.s2
        state.s0 ^= state.s3
        state.s2 ^= t
        state.s3 = Self.rotate(state.s3, 45)
        return result
    }
    private func nextBelow(_ upper: UInt64) -> UInt64 {
        if upper == 0 { return nextUInt64() }
        let threshold = (0 &- upper) % upper
        while true {
            let value = nextUInt64()
            if value >= threshold { return value % upper }
        }
    }
    private static func rotate(_ value: UInt64, _ offset: Int) -> UInt64 { value << offset | value >> (64 - offset) }
}

private func splitMix(_ state: inout UInt64) -> UInt64 {
    state = state &+ 0x9e37_79b9_7f4a_7c15
    var value = state
    value = (value ^ (value >> 30)) &* 0xbf58_476d_1ce4_e5b9
    value = (value ^ (value >> 27)) &* 0x94d0_49bb_1331_11eb
    return value ^ (value >> 31)
}
