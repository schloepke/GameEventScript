// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// SHA-256 (FIPS 180-4) and HMAC-SHA256 (RFC 2104) without Foundation or CryptoKit. Both own their working memory from
// creation on, so hashing allocates nothing. Not thread-safe.
final class GesNetworkSha256 {
    static let digestBytes = 32
    static let blockBytes = 64

    private static let roundConstants: [UInt32] = [
        0x428a_2f98, 0x7137_4491, 0xb5c0_fbcf, 0xe9b5_dba5, 0x3956_c25b, 0x59f1_11f1, 0x923f_82a4, 0xab1c_5ed5,
        0xd807_aa98, 0x1283_5b01, 0x2431_85be, 0x550c_7dc3, 0x72be_5d74, 0x80de_b1fe, 0x9bdc_06a7, 0xc19b_f174,
        0xe49b_69c1, 0xefbe_4786, 0x0fc1_9dc6, 0x240c_a1cc, 0x2de9_2c6f, 0x4a74_84aa, 0x5cb0_a9dc, 0x76f9_88da,
        0x983e_5152, 0xa831_c66d, 0xb003_27c8, 0xbf59_7fc7, 0xc6e0_0bf3, 0xd5a7_9147, 0x06ca_6351, 0x1429_2967,
        0x27b7_0a85, 0x2e1b_2138, 0x4d2c_6dfc, 0x5338_0d13, 0x650a_7354, 0x766a_0abb, 0x81c2_c92e, 0x9272_2c85,
        0xa2bf_e8a1, 0xa81a_664b, 0xc24b_8b70, 0xc76c_51a3, 0xd192_e819, 0xd699_0624, 0xf40e_3585, 0x106a_a070,
        0x19a4_c116, 0x1e37_6c08, 0x2748_774c, 0x34b0_bcb5, 0x391c_0cb3, 0x4ed8_aa4a, 0x5b9c_ca4f, 0x682e_6ff3,
        0x748f_82ee, 0x78a5_636f, 0x84c8_7814, 0x8cc7_0208, 0x90be_fffa, 0xa450_6ceb, 0xbef9_a3f7, 0xc671_78f2,
    ]

    private static let initialState: [UInt32] = [
        0x6a09_e667, 0xbb67_ae85, 0x3c6e_f372, 0xa54f_f53a, 0x510e_527f, 0x9b05_688c, 0x1f83_d9ab, 0x5be0_cd19,
    ]

    // The chaining state, the message schedule, the round constants and the partial block.
    private let state = UnsafeMutablePointer<UInt32>.allocate(capacity: 8)
    private let schedule = UnsafeMutablePointer<UInt32>.allocate(capacity: 64)
    private let constants = UnsafeMutablePointer<UInt32>.allocate(capacity: 64)
    private let block = UnsafeMutablePointer<UInt8>.allocate(capacity: GesNetworkSha256.blockBytes)
    private var blockLength = 0
    private var totalBytes: UInt64 = 0

    init() {
        for index in 0..<64 { constants[index] = Self.roundConstants[index] }
        reset()
    }

    deinit {
        state.deallocate()
        schedule.deallocate()
        constants.deallocate()
        block.deallocate()
    }

    // Starts a new hash.
    func reset() {
        for index in 0..<8 { state[index] = Self.initialState[index] }
        blockLength = 0
        totalBytes = 0
    }

    // Starts a new hash from a chaining state saved after whole blocks.
    func reset(from saved: UnsafePointer<UInt32>, bytes: UInt64) {
        state.update(from: saved, count: 8)
        blockLength = 0
        totalBytes = bytes
    }

    // Saves the chaining state; valid only after whole blocks.
    func save(into target: UnsafeMutablePointer<UInt32>) {
        target.update(from: state, count: 8)
    }

    func update(_ data: UnsafeRawBufferPointer) {
        totalBytes &+= UInt64(data.count)
        var index = 0
        while index < data.count {
            let take = min(Self.blockBytes - blockLength, data.count - index)
            for offset in 0..<take { block[blockLength + offset] = data[index + offset] }
            blockLength += take
            index += take
            if blockLength == Self.blockBytes {
                compress()
                blockLength = 0
            }
        }
    }

    // Pads the message, writes the 32-byte digest and leaves the hash to be reset.
    func finish(into digest: UnsafeMutableRawBufferPointer) {
        let bits = totalBytes &* 8
        block[blockLength] = 0x80
        blockLength += 1
        if blockLength > Self.blockBytes - 8 {
            while blockLength < Self.blockBytes {
                block[blockLength] = 0
                blockLength += 1
            }
            compress()
            blockLength = 0
        }
        while blockLength < Self.blockBytes - 8 {
            block[blockLength] = 0
            blockLength += 1
        }
        for index in 0..<8 { block[Self.blockBytes - 1 - index] = UInt8(truncatingIfNeeded: bits >> UInt64(8 * index)) }
        compress()
        blockLength = 0
        for index in 0..<8 {
            let word = state[index]
            digest[4 * index] = UInt8(truncatingIfNeeded: word >> 24)
            digest[4 * index + 1] = UInt8(truncatingIfNeeded: word >> 16)
            digest[4 * index + 2] = UInt8(truncatingIfNeeded: word >> 8)
            digest[4 * index + 3] = UInt8(truncatingIfNeeded: word)
        }
    }

    private func compress() {
        for index in 0..<16 {
            schedule[index] = UInt32(block[4 * index]) << 24 | UInt32(block[4 * index + 1]) << 16 | UInt32(block[4 * index + 2]) << 8 | UInt32(block[4 * index + 3])
        }
        for index in 16..<64 {
            let low = schedule[index - 15]
            let high = schedule[index - 2]
            let sigma0 = rotate(low, 7) ^ rotate(low, 18) ^ (low >> 3)
            let sigma1 = rotate(high, 17) ^ rotate(high, 19) ^ (high >> 10)
            schedule[index] = schedule[index - 16] &+ sigma0 &+ schedule[index - 7] &+ sigma1
        }

        var a = state[0]
        var b = state[1]
        var c = state[2]
        var d = state[3]
        var e = state[4]
        var f = state[5]
        var g = state[6]
        var h = state[7]
        for index in 0..<64 {
            let sum1 = rotate(e, 6) ^ rotate(e, 11) ^ rotate(e, 25)
            let choice = (e & f) ^ (~e & g)
            let first = h &+ sum1 &+ choice &+ constants[index] &+ schedule[index]
            let sum0 = rotate(a, 2) ^ rotate(a, 13) ^ rotate(a, 22)
            let majority = (a & b) ^ (a & c) ^ (b & c)
            let second = sum0 &+ majority
            h = g
            g = f
            f = e
            e = d &+ first
            d = c
            c = b
            b = a
            a = first &+ second
        }
        state[0] &+= a
        state[1] &+= b
        state[2] &+= c
        state[3] &+= d
        state[4] &+= e
        state[5] &+= f
        state[6] &+= g
        state[7] &+= h
    }

    private func rotate(_ value: UInt32, _ count: UInt32) -> UInt32 { (value >> count) | (value << (32 - count)) }
}

// HMAC-SHA256 with a fixed key: the chaining states after the inner and the outer key block are computed once.
final class GesNetworkHmacSha256 {
    private let sha = GesNetworkSha256()
    private let inner = UnsafeMutablePointer<UInt32>.allocate(capacity: 8)
    private let outer = UnsafeMutablePointer<UInt32>.allocate(capacity: 8)
    private let digest = UnsafeMutableRawBufferPointer.allocate(byteCount: GesNetworkSha256.digestBytes, alignment: 1)

    // Copies nothing of the key: it is reduced to the two chaining states.
    init(key: UnsafeRawBufferPointer) {
        let pad = UnsafeMutableRawBufferPointer.allocate(byteCount: GesNetworkSha256.blockBytes, alignment: 1)
        defer { pad.deallocate() }
        pad.initializeMemory(as: UInt8.self, repeating: 0)
        if key.count > GesNetworkSha256.blockBytes {
            sha.update(key)
            sha.finish(into: UnsafeMutableRawBufferPointer(rebasing: pad[0..<GesNetworkSha256.digestBytes]))
            sha.reset()
        } else if !key.isEmpty {
            pad.baseAddress!.copyMemory(from: key.baseAddress!, byteCount: key.count)
        }

        for index in 0..<pad.count { pad[index] ^= 0x36 }
        sha.update(UnsafeRawBufferPointer(pad))
        sha.save(into: inner)
        sha.reset()
        for index in 0..<pad.count { pad[index] ^= 0x36 ^ 0x5c }
        sha.update(UnsafeRawBufferPointer(pad))
        sha.save(into: outer)
        sha.reset()
    }

    deinit {
        inner.deallocate()
        outer.deallocate()
        digest.deallocate()
    }

    // Writes the 32-byte HMAC of the message.
    func compute(_ message: UnsafeRawBufferPointer, into result: UnsafeMutableRawBufferPointer) {
        sha.reset(from: inner, bytes: UInt64(GesNetworkSha256.blockBytes))
        sha.update(message)
        sha.finish(into: digest)
        sha.reset(from: outer, bytes: UInt64(GesNetworkSha256.blockBytes))
        sha.update(UnsafeRawBufferPointer(digest))
        sha.finish(into: result)
    }
}
