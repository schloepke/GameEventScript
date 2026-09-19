// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Dependency-free SHA-256 for source archive validation.
/// Implements the 512-bit block schedule, 64 compression rounds, and big-endian
/// length padding defined by FIPS 180-4. All word arithmetic wraps modulo 2^32.
enum GesSha256 {
    /// Returns the 32-byte SHA-256 digest.
    /// Hashing is synchronous and performs no I/O or platform cryptography calls.
    static func digest(_ bytes: [UInt8]) -> [UInt8] {
        var state: [UInt32] = [
            0x6a09_e667, 0xbb67_ae85, 0x3c6e_f372, 0xa54f_f53a,
            0x510e_527f, 0x9b05_688c, 0x1f83_d9ab, 0x5be0_cd19,
        ]
        var schedule = [UInt32](repeating: 0, count: 64)
        let completeLength = bytes.count - bytes.count % 64
        for offset in stride(from: 0, to: completeLength, by: 64) {
            compress(bytes, offset: offset, schedule: &schedule, state: &state)
        }

        // Only the final one or two blocks are copied. The input itself stays immutable.
        let remainder = bytes.count - completeLength
        var tail = [UInt8](repeating: 0, count: remainder < 56 ? 64 : 128)
        for index in 0..<remainder { tail[index] = bytes[completeLength + index] }
        tail[remainder] = 0x80
        let bitCount = UInt64(bytes.count) &* 8
        for index in 0..<8 {
            tail[tail.count - 1 - index] = UInt8(truncatingIfNeeded: bitCount >> (index * 8))
        }
        for offset in stride(from: 0, to: tail.count, by: 64) {
            compress(tail, offset: offset, schedule: &schedule, state: &state)
        }

        return state.flatMap { word in
            [
                UInt8(truncatingIfNeeded: word >> 24), UInt8(truncatingIfNeeded: word >> 16),
                UInt8(truncatingIfNeeded: word >> 8), UInt8(truncatingIfNeeded: word),
            ]
        }
    }

    private static func compress(_ bytes: [UInt8], offset: Int, schedule: inout [UInt32], state: inout [UInt32]) {
        for index in 0..<16 {
            let start = offset + index * 4
            schedule[index] =
                UInt32(bytes[start]) << 24 | UInt32(bytes[start + 1]) << 16
                | UInt32(bytes[start + 2]) << 8 | UInt32(bytes[start + 3])
        }
        for index in 16..<64 {
            let a = schedule[index - 15]
            let b = schedule[index - 2]
            let smallSigma0 = rotateRight(a, 7) ^ rotateRight(a, 18) ^ (a >> 3)
            let smallSigma1 = rotateRight(b, 17) ^ rotateRight(b, 19) ^ (b >> 10)
            schedule[index] = schedule[index - 16] &+ smallSigma0 &+ schedule[index - 7] &+ smallSigma1
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
            let bigSigma1 = rotateRight(e, 6) ^ rotateRight(e, 11) ^ rotateRight(e, 25)
            let choice = (e & f) ^ (~e & g)
            let first = h &+ bigSigma1 &+ choice &+ roundConstants[index] &+ schedule[index]
            let bigSigma0 = rotateRight(a, 2) ^ rotateRight(a, 13) ^ rotateRight(a, 22)
            let majority = (a & b) ^ (a & c) ^ (b & c)
            let second = bigSigma0 &+ majority
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

    private static func rotateRight(_ word: UInt32, _ count: UInt32) -> UInt32 {
        (word >> count) | (word << (32 - count))
    }

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
}
