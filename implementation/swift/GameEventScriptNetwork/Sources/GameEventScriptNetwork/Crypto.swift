// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The cipher suite of GesNetwork (specs/Network.md#cipher-suite): X25519 (RFC 7748), ChaCha20-Poly1305 (RFC 8439) and
// HKDF-SHA256 (RFC 5869), without Foundation or CryptoKit. Secret data never selects a branch or a memory address: the
// ladder swaps with masks, the field arithmetic has no data-dependent carries and the tag comparison accumulates every
// byte.

// X25519 over field elements of ten signed limbs of alternately 26 and 25 bits (radix 2^25.5).
enum GesNetworkX25519 {
    static let keyBytes = 32
    private static let limbs = 10
    private static let a24: Int64 = 121_665

    // Writes the public key of a private key: its product with the base point 9.
    static func publicKey(_ privateKey: UnsafeRawBufferPointer, into publicKey: UnsafeMutableRawBufferPointer) {
        withUnsafeTemporaryAllocation(of: UInt8.self, capacity: keyBytes) { basePoint in
            basePoint.initialize(repeating: 0)
            basePoint[0] = 9
            scalarMultiply(privateKey, UnsafeRawBufferPointer(basePoint), into: publicKey)
        }
    }

    // Writes the shared secret of a private key and a peer's public key; false when it is all zero (a low-order key).
    static func sharedSecret(_ privateKey: UnsafeRawBufferPointer, _ publicKey: UnsafeRawBufferPointer, into secret: UnsafeMutableRawBufferPointer) -> Bool {
        scalarMultiply(privateKey, publicKey, into: secret)
        var bits: UInt8 = 0
        for index in 0..<keyBytes { bits |= secret[index] }
        return bits != 0
    }

    // The function X25519 of RFC 7748 section 5: the clamped scalar times the u-coordinate, high bit masked.
    static func scalarMultiply(_ scalar: UnsafeRawBufferPointer, _ u: UnsafeRawBufferPointer, into result: UnsafeMutableRawBufferPointer) {
        withUnsafeTemporaryAllocation(of: UInt8.self, capacity: keyBytes) { k in
            for index in 0..<keyBytes { k[index] = scalar[index] }
            k[0] &= 248
            k[31] &= 127
            k[31] |= 64
            withUnsafeTemporaryAllocation(of: Int64.self, capacity: limbs * 19) { memory in
                let base = memory.baseAddress!
                base.initialize(repeating: 0, count: limbs * 19)
                let x1 = base
                let x2 = base + limbs
                let z2 = base + 2 * limbs
                let x3 = base + 3 * limbs
                let z3 = base + 4 * limbs
                let a = base + 5 * limbs
                let aa = base + 6 * limbs
                let b = base + 7 * limbs
                let bb = base + 8 * limbs
                let e = base + 9 * limbs
                let c = base + 10 * limbs
                let d = base + 11 * limbs
                let da = base + 12 * limbs
                let cb = base + 13 * limbs
                // Scratch for the product and the inversion.
                let scratch = base + 14 * limbs

                fromBytes(x1, u)
                x2[0] = 1
                x3.update(from: x1, count: limbs)
                z3[0] = 1
                var swap: Int64 = 0
                var t = 254
                while t >= 0 {
                    let bit = Int64((k[t >> 3] >> UInt8(t & 7)) & 1)
                    swap ^= bit
                    conditionalSwap(x2, x3, swap)
                    conditionalSwap(z2, z3, swap)
                    swap = bit

                    add(a, x2, z2)
                    multiply(aa, a, a, scratch)
                    subtract(b, x2, z2)
                    multiply(bb, b, b, scratch)
                    subtract(e, aa, bb)
                    add(c, x3, z3)
                    subtract(d, x3, z3)
                    multiply(da, d, a, scratch)
                    multiply(cb, c, b, scratch)
                    add(x3, da, cb)
                    multiply(x3, x3, x3, scratch)
                    subtract(z3, da, cb)
                    multiply(z3, z3, z3, scratch)
                    multiply(z3, z3, x1, scratch)
                    multiply(x2, aa, bb, scratch)
                    multiplySmall(z2, e, a24)
                    add(z2, z2, aa)
                    multiply(z2, z2, e, scratch)
                    t -= 1
                }

                conditionalSwap(x2, x3, swap)
                conditionalSwap(z2, z3, swap)
                invert(a, z2, scratch)
                multiply(x2, x2, a, scratch)
                toBytes(result, x2)
                base.update(repeating: 0, count: limbs * 19)
            }
            k.update(repeating: 0)
        }
    }

    // Bit offset of a limb; limb i holds bits offset(i) up to offset(i + 1).
    private static func offset(_ limb: Int) -> Int { (51 * limb + 1) >> 1 }

    private static func fromBytes(_ h: UnsafeMutablePointer<Int64>, _ bytes: UnsafeRawBufferPointer) {
        for limb in 0..<limbs {
            let start = offset(limb)
            let width = offset(limb + 1) - start
            let index = start >> 3
            let word = UInt32(bytes[index]) | UInt32(bytes[index + 1]) << 8 | UInt32(bytes[index + 2]) << 16 | UInt32(bytes[index + 3]) << 24
            // The last limb ends at bit 255 and so drops the high bit.
            h[limb] = Int64((word >> UInt32(start & 7)) & ((1 << UInt32(width)) - 1))
        }
    }

    // Writes the canonical 32 bytes: the element is reduced below 2^255 - 19 without a branch.
    private static func toBytes(_ bytes: UnsafeMutableRawBufferPointer, _ h: UnsafeMutablePointer<Int64>) {
        var q = (19 &* h[9] &+ (1 << 24)) >> 25
        for limb in 0..<limbs { q = (h[limb] &+ q) >> (limb & 1 == 0 ? 26 : 25) }
        h[0] &+= 19 &* q
        for limb in 0..<limbs - 1 {
            let shift: Int64 = limb & 1 == 0 ? 26 : 25
            let carry = h[limb] >> shift
            h[limb + 1] &+= carry
            h[limb] &-= carry << shift
        }
        h[9] &= (1 << 25) - 1
        var accumulator: UInt64 = 0
        var bits = 0
        var position = 0
        for limb in 0..<limbs {
            accumulator |= UInt64(bitPattern: h[limb]) << UInt64(bits)
            bits += limb & 1 == 0 ? 26 : 25
            while bits >= 8 {
                bytes[position] = UInt8(truncatingIfNeeded: accumulator)
                position += 1
                accumulator >>= 8
                bits -= 8
            }
        }
        bytes[position] = UInt8(truncatingIfNeeded: accumulator)
    }

    private static func add(_ h: UnsafeMutablePointer<Int64>, _ f: UnsafeMutablePointer<Int64>, _ g: UnsafeMutablePointer<Int64>) {
        for limb in 0..<limbs { h[limb] = f[limb] &+ g[limb] }
    }

    private static func subtract(_ h: UnsafeMutablePointer<Int64>, _ f: UnsafeMutablePointer<Int64>, _ g: UnsafeMutablePointer<Int64>) {
        for limb in 0..<limbs { h[limb] = f[limb] &- g[limb] }
    }

    // Swaps f and g when swap is 1 and leaves them when it is 0, with the same memory accesses either way.
    private static func conditionalSwap(_ f: UnsafeMutablePointer<Int64>, _ g: UnsafeMutablePointer<Int64>, _ swap: Int64) {
        let mask = 0 &- swap
        for limb in 0..<limbs {
            let x = (f[limb] ^ g[limb]) & mask
            f[limb] ^= x
            g[limb] ^= x
        }
    }

    // h = f * g through the scratch limbs; h may alias f or g. Products of two odd limbs count twice; those past limb 9
    // wrap with 19.
    private static func multiply(_ h: UnsafeMutablePointer<Int64>, _ f: UnsafeMutablePointer<Int64>, _ g: UnsafeMutablePointer<Int64>, _ scratch: UnsafeMutablePointer<Int64>) {
        let t = scratch
        t.update(repeating: 0, count: limbs)
        for i in 0..<limbs {
            for j in 0..<limbs {
                var product = f[i] &* g[j]
                if i & j & 1 != 0 { product &*= 2 }
                var k = i + j
                if k >= limbs {
                    product &*= 19
                    k -= limbs
                }
                t[k] &+= product
            }
        }
        carry(t)
        h.update(from: t, count: limbs)
    }

    private static func multiplySmall(_ h: UnsafeMutablePointer<Int64>, _ f: UnsafeMutablePointer<Int64>, _ factor: Int64) {
        for limb in 0..<limbs { h[limb] = f[limb] &* factor }
        carry(h)
    }

    // Brings every limb back into its signed width; the order bounds the intermediate values as ref10 does.
    private static func carry(_ h: UnsafeMutablePointer<Int64>) {
        carryLimb(h, 0)
        carryLimb(h, 4)
        carryLimb(h, 1)
        carryLimb(h, 5)
        carryLimb(h, 2)
        carryLimb(h, 6)
        carryLimb(h, 3)
        carryLimb(h, 7)
        carryLimb(h, 4)
        carryLimb(h, 8)
        carryLimb(h, 9)
        carryLimb(h, 0)
    }

    private static func carryLimb(_ h: UnsafeMutablePointer<Int64>, _ limb: Int) {
        let shift: Int64 = limb & 1 == 0 ? 26 : 25
        let carry = (h[limb] &+ (1 << (shift - 1))) >> shift
        h[limb] &-= carry << shift
        if limb == limbs - 1 {
            h[0] &+= 19 &* carry
        } else {
            h[limb + 1] &+= carry
        }
    }

    private static func square(_ h: UnsafeMutablePointer<Int64>, _ f: UnsafeMutablePointer<Int64>, _ times: Int, _ scratch: UnsafeMutablePointer<Int64>) {
        multiply(h, f, f, scratch)
        for _ in 1..<max(times, 1) { multiply(h, h, h, scratch) }
    }

    // result = z^(2^255 - 21), the inverse of z, by the fixed addition chain of ref10. Uses five scratch elements.
    private static func invert(_ result: UnsafeMutablePointer<Int64>, _ z: UnsafeMutablePointer<Int64>, _ scratch: UnsafeMutablePointer<Int64>) {
        let product = scratch
        let t0 = scratch + limbs
        let t1 = scratch + 2 * limbs
        let t2 = scratch + 3 * limbs
        let t3 = scratch + 4 * limbs
        square(t0, z, 1, product)
        square(t1, t0, 2, product)
        multiply(t1, z, t1, product)
        multiply(t0, t0, t1, product)
        square(t2, t0, 1, product)
        multiply(t1, t1, t2, product)
        square(t2, t1, 5, product)
        multiply(t1, t2, t1, product)
        square(t2, t1, 10, product)
        multiply(t2, t2, t1, product)
        square(t3, t2, 20, product)
        multiply(t2, t3, t2, product)
        square(t2, t2, 10, product)
        multiply(t1, t2, t1, product)
        square(t2, t1, 50, product)
        multiply(t2, t2, t1, product)
        square(t3, t2, 100, product)
        multiply(t2, t3, t2, product)
        square(t2, t2, 50, product)
        multiply(t1, t2, t1, product)
        square(t1, t1, 5, product)
        multiply(result, t1, t0, product)
    }
}

// Poly1305 (RFC 8439 section 2.5) over five 26-bit limbs. Owns its state; reusable after start.
final class GesNetworkPoly1305 {
    static let tagBytes = 16
    private static let mask: UInt32 = 0x3ff_ffff

    private let block = UnsafeMutableRawBufferPointer.allocate(byteCount: 16, alignment: 4)
    private var r0: UInt32 = 0, r1: UInt32 = 0, r2: UInt32 = 0, r3: UInt32 = 0, r4: UInt32 = 0
    private var h0: UInt32 = 0, h1: UInt32 = 0, h2: UInt32 = 0, h3: UInt32 = 0, h4: UInt32 = 0
    private var pad0: UInt32 = 0, pad1: UInt32 = 0, pad2: UInt32 = 0, pad3: UInt32 = 0
    private var buffered = 0

    init() { block.initializeMemory(as: UInt8.self, repeating: 0) }

    deinit { block.deallocate() }

    // Starts a tag with a one-time 32-byte key.
    func start(key: UnsafeRawBufferPointer) {
        r0 = load(key, 0) & 0x3ff_ffff
        r1 = (load(key, 3) >> 2) & 0x3ff_ff03
        r2 = (load(key, 6) >> 4) & 0x3ff_c0ff
        r3 = (load(key, 9) >> 6) & 0x3f0_3fff
        r4 = (load(key, 12) >> 8) & 0x00f_ffff
        pad0 = load(key, 16)
        pad1 = load(key, 20)
        pad2 = load(key, 24)
        pad3 = load(key, 28)
        h0 = 0
        h1 = 0
        h2 = 0
        h3 = 0
        h4 = 0
        buffered = 0
    }

    func update(_ data: UnsafeRawBufferPointer) {
        var index = 0
        if buffered > 0 {
            let take = min(16 - buffered, data.count)
            for offset in 0..<take { block[buffered + offset] = data[offset] }
            buffered += take
            index = take
            if buffered < 16 { return }
            process(UnsafeRawBufferPointer(block), highBit: 1 << 24)
            buffered = 0
        }
        while data.count - index >= 16 {
            process(UnsafeRawBufferPointer(rebasing: data[index..<index + 16]), highBit: 1 << 24)
            index += 16
        }
        for offset in 0..<data.count - index { block[offset] = data[index + offset] }
        buffered = data.count - index
    }

    // Completes a partial block with zero bytes as a full block: the padding of the AEAD construction.
    func padToBlock() {
        if buffered == 0 { return }
        for index in buffered..<16 { block[index] = 0 }
        process(UnsafeRawBufferPointer(block), highBit: 1 << 24)
        buffered = 0
    }

    func finish(into tag: UnsafeMutableRawBufferPointer) {
        if buffered > 0 {
            block[buffered] = 1
            for index in buffered + 1..<16 { block[index] = 0 }
            process(UnsafeRawBufferPointer(block), highBit: 0)
            buffered = 0
        }

        let mask = Self.mask
        var h0 = self.h0
        var h1 = self.h1
        var h2 = self.h2
        var h3 = self.h3
        var h4 = self.h4
        var c = h1 >> 26
        h1 &= mask
        h2 &+= c
        c = h2 >> 26
        h2 &= mask
        h3 &+= c
        c = h3 >> 26
        h3 &= mask
        h4 &+= c
        c = h4 >> 26
        h4 &= mask
        h0 &+= c &* 5
        c = h0 >> 26
        h0 &= mask
        h1 &+= c

        // g = h + 5 - 2^130; h is replaced by g when g is not negative, selected with a mask.
        var g0 = h0 &+ 5
        c = g0 >> 26
        g0 &= mask
        var g1 = h1 &+ c
        c = g1 >> 26
        g1 &= mask
        var g2 = h2 &+ c
        c = g2 >> 26
        g2 &= mask
        var g3 = h3 &+ c
        c = g3 >> 26
        g3 &= mask
        let g4 = h4 &+ c &- (1 << 26)
        let select = (g4 >> 31) &- 1
        let keep = ~select
        h0 = (h0 & keep) | (g0 & select)
        h1 = (h1 & keep) | (g1 & select)
        h2 = (h2 & keep) | (g2 & select)
        h3 = (h3 & keep) | (g3 & select)
        h4 = (h4 & keep) | (g4 & select)

        h0 |= h1 << 26
        h1 = (h1 >> 6) | (h2 << 20)
        h2 = (h2 >> 12) | (h3 << 14)
        h3 = (h3 >> 18) | (h4 << 8)
        var f = UInt64(h0) &+ UInt64(pad0)
        store(tag, 0, UInt32(truncatingIfNeeded: f))
        f = UInt64(h1) &+ UInt64(pad1) &+ (f >> 32)
        store(tag, 4, UInt32(truncatingIfNeeded: f))
        f = UInt64(h2) &+ UInt64(pad2) &+ (f >> 32)
        store(tag, 8, UInt32(truncatingIfNeeded: f))
        f = UInt64(h3) &+ UInt64(pad3) &+ (f >> 32)
        store(tag, 12, UInt32(truncatingIfNeeded: f))
        r0 = 0
        r1 = 0
        r2 = 0
        r3 = 0
        r4 = 0
        pad0 = 0
        pad1 = 0
        pad2 = 0
        pad3 = 0
    }

    private func process(_ m: UnsafeRawBufferPointer, highBit: UInt32) {
        let mask = Self.mask
        let h0 = UInt64(self.h0 &+ (load(m, 0) & mask))
        let h1 = UInt64(self.h1 &+ ((load(m, 3) >> 2) & mask))
        let h2 = UInt64(self.h2 &+ ((load(m, 6) >> 4) & mask))
        let h3 = UInt64(self.h3 &+ ((load(m, 9) >> 6) & mask))
        let h4 = UInt64(self.h4 &+ ((load(m, 12) >> 8) | highBit))
        let r0 = UInt64(self.r0)
        let r1 = UInt64(self.r1)
        let r2 = UInt64(self.r2)
        let r3 = UInt64(self.r3)
        let r4 = UInt64(self.r4)
        let s1 = r1 &* 5
        let s2 = r2 &* 5
        let s3 = r3 &* 5
        let s4 = r4 &* 5
        let d0 = h0 &* r0 &+ h1 &* s4 &+ h2 &* s3 &+ h3 &* s2 &+ h4 &* s1
        var d1 = h0 &* r1 &+ h1 &* r0 &+ h2 &* s4 &+ h3 &* s3 &+ h4 &* s2
        var d2 = h0 &* r2 &+ h1 &* r1 &+ h2 &* r0 &+ h3 &* s4 &+ h4 &* s3
        var d3 = h0 &* r3 &+ h1 &* r2 &+ h2 &* r1 &+ h3 &* r0 &+ h4 &* s4
        var d4 = h0 &* r4 &+ h1 &* r3 &+ h2 &* r2 &+ h3 &* r1 &+ h4 &* r0
        let wide = UInt64(mask)
        var c = d0 >> 26
        var n0 = UInt32(d0 & wide)
        d1 &+= c
        c = d1 >> 26
        var n1 = UInt32(d1 & wide)
        d2 &+= c
        c = d2 >> 26
        let n2 = UInt32(d2 & wide)
        d3 &+= c
        c = d3 >> 26
        let n3 = UInt32(d3 & wide)
        d4 &+= c
        c = d4 >> 26
        let n4 = UInt32(d4 & wide)
        n0 &+= UInt32(truncatingIfNeeded: c) &* 5
        let carry = n0 >> 26
        n0 &= mask
        n1 &+= carry
        self.h0 = n0
        self.h1 = n1
        self.h2 = n2
        self.h3 = n3
        self.h4 = n4
    }

    private func load(_ data: UnsafeRawBufferPointer, _ offset: Int) -> UInt32 {
        UInt32(data[offset]) | UInt32(data[offset + 1]) << 8 | UInt32(data[offset + 2]) << 16 | UInt32(data[offset + 3]) << 24
    }

    private func store(_ data: UnsafeMutableRawBufferPointer, _ offset: Int, _ value: UInt32) {
        data[offset] = UInt8(truncatingIfNeeded: value)
        data[offset + 1] = UInt8(truncatingIfNeeded: value >> 8)
        data[offset + 2] = UInt8(truncatingIfNeeded: value >> 16)
        data[offset + 3] = UInt8(truncatingIfNeeded: value >> 24)
    }
}

// ChaCha20-Poly1305 (RFC 8439 section 2.8) with a replaceable key. Seals and opens in place; allocates nothing after
// creation. Not thread-safe.
final class GesNetworkAead {
    static let keyBytes = 32
    static let nonceBytes = 12
    static let tagBytes = GesNetworkPoly1305.tagBytes

    private let key = UnsafeMutablePointer<UInt32>.allocate(capacity: 8)
    private let state = UnsafeMutablePointer<UInt32>.allocate(capacity: 16)
    private let working = UnsafeMutablePointer<UInt32>.allocate(capacity: 16)
    private let stream = UnsafeMutableRawBufferPointer.allocate(byteCount: 64, alignment: 4)
    private let lengths = UnsafeMutableRawBufferPointer.allocate(byteCount: 16, alignment: 8)
    private let tag = UnsafeMutableRawBufferPointer.allocate(byteCount: GesNetworkAead.tagBytes, alignment: 1)
    private let poly = GesNetworkPoly1305()

    init() {
        key.initialize(repeating: 0, count: 8)
        state.initialize(repeating: 0, count: 16)
        working.initialize(repeating: 0, count: 16)
        stream.initializeMemory(as: UInt8.self, repeating: 0)
        lengths.initializeMemory(as: UInt8.self, repeating: 0)
        tag.initializeMemory(as: UInt8.self, repeating: 0)
    }

    deinit {
        clear()
        key.deallocate()
        state.deallocate()
        working.deallocate()
        stream.deallocate()
        lengths.deallocate()
        tag.deallocate()
    }

    func setKey(_ bytes: UnsafeRawBufferPointer) {
        for index in 0..<8 { key[index] = Self.load(bytes, 4 * index) }
    }

    func clear() {
        key.update(repeating: 0, count: 8)
        state.update(repeating: 0, count: 16)
        working.update(repeating: 0, count: 16)
        for index in 0..<64 { stream[index] = 0 }
    }

    // Encrypts the first length bytes of the buffer in place and writes the tag behind them.
    func seal(nonce: UnsafeRawBufferPointer, aad: UnsafeRawBufferPointer, buffer: UnsafeMutableRawBufferPointer, length: Int) {
        let data = UnsafeMutableRawBufferPointer(rebasing: buffer[0..<length])
        crypt(nonce, data)
        authenticate(nonce, aad, UnsafeRawBufferPointer(data), UnsafeMutableRawBufferPointer(rebasing: buffer[length..<length + Self.tagBytes]))
    }

    // Checks the tag behind the ciphertext and decrypts it in place; false, with the buffer unchanged, when the tag does
    // not match. The plaintext is the sealed length minus the tag.
    func open(nonce: UnsafeRawBufferPointer, aad: UnsafeRawBufferPointer, sealed: UnsafeMutableRawBufferPointer) -> Bool {
        if sealed.count < Self.tagBytes { return false }
        let length = sealed.count - Self.tagBytes
        let data = UnsafeMutableRawBufferPointer(rebasing: sealed[0..<length])
        authenticate(nonce, aad, UnsafeRawBufferPointer(data), tag)
        var difference: UInt8 = 0
        for index in 0..<Self.tagBytes { difference |= tag[index] ^ sealed[length + index] }
        if difference != 0 { return false }
        crypt(nonce, data)
        return true
    }

    private func authenticate(_ nonce: UnsafeRawBufferPointer, _ aad: UnsafeRawBufferPointer, _ ciphertext: UnsafeRawBufferPointer, _ tag: UnsafeMutableRawBufferPointer) {
        block(nonce, 0)
        poly.start(key: UnsafeRawBufferPointer(stream))
        poly.update(aad)
        poly.padToBlock()
        poly.update(ciphertext)
        poly.padToBlock()
        Self.store64(lengths, 0, UInt64(aad.count))
        Self.store64(lengths, 8, UInt64(ciphertext.count))
        poly.update(UnsafeRawBufferPointer(lengths))
        poly.finish(into: tag)
    }

    // XORs the key stream from block counter 1 on.
    private func crypt(_ nonce: UnsafeRawBufferPointer, _ data: UnsafeMutableRawBufferPointer) {
        var counter: UInt32 = 1
        var index = 0
        while index < data.count {
            block(nonce, counter)
            counter &+= 1
            let take = min(64, data.count - index)
            for offset in 0..<take { data[index + offset] ^= stream[offset] }
            index += take
        }
    }

    // The ChaCha20 block function (RFC 8439 section 2.3) into the key stream buffer.
    private func block(_ nonce: UnsafeRawBufferPointer, _ counter: UInt32) {
        let s = state
        s[0] = 0x6170_7865
        s[1] = 0x3320_646e
        s[2] = 0x7962_2d32
        s[3] = 0x6b20_6574
        for index in 0..<8 { s[4 + index] = key[index] }
        s[12] = counter
        s[13] = Self.load(nonce, 0)
        s[14] = Self.load(nonce, 4)
        s[15] = Self.load(nonce, 8)
        let w = working
        w.update(from: s, count: 16)
        for _ in 0..<10 {
            quarterRound(w, 0, 4, 8, 12)
            quarterRound(w, 1, 5, 9, 13)
            quarterRound(w, 2, 6, 10, 14)
            quarterRound(w, 3, 7, 11, 15)
            quarterRound(w, 0, 5, 10, 15)
            quarterRound(w, 1, 6, 11, 12)
            quarterRound(w, 2, 7, 8, 13)
            quarterRound(w, 3, 4, 9, 14)
        }
        for index in 0..<16 {
            let word = w[index] &+ s[index]
            stream[4 * index] = UInt8(truncatingIfNeeded: word)
            stream[4 * index + 1] = UInt8(truncatingIfNeeded: word >> 8)
            stream[4 * index + 2] = UInt8(truncatingIfNeeded: word >> 16)
            stream[4 * index + 3] = UInt8(truncatingIfNeeded: word >> 24)
        }
    }

    private func quarterRound(_ x: UnsafeMutablePointer<UInt32>, _ a: Int, _ b: Int, _ c: Int, _ d: Int) {
        x[a] &+= x[b]
        x[d] = rotate(x[d] ^ x[a], 16)
        x[c] &+= x[d]
        x[b] = rotate(x[b] ^ x[c], 12)
        x[a] &+= x[b]
        x[d] = rotate(x[d] ^ x[a], 8)
        x[c] &+= x[d]
        x[b] = rotate(x[b] ^ x[c], 7)
    }

    private func rotate(_ value: UInt32, _ count: UInt32) -> UInt32 { (value << count) | (value >> (32 - count)) }

    private static func load(_ data: UnsafeRawBufferPointer, _ offset: Int) -> UInt32 {
        UInt32(data[offset]) | UInt32(data[offset + 1]) << 8 | UInt32(data[offset + 2]) << 16 | UInt32(data[offset + 3]) << 24
    }

    private static func store64(_ data: UnsafeMutableRawBufferPointer, _ offset: Int, _ value: UInt64) {
        for index in 0..<8 { data[offset + index] = UInt8(truncatingIfNeeded: value >> UInt64(8 * index)) }
    }
}

// HKDF-SHA256 (RFC 5869). Used at the handshake and at a key change, so it may allocate.
enum GesNetworkHkdf {
    static let hashBytes = 32

    static func extract(salt: UnsafeRawBufferPointer, ikm: UnsafeRawBufferPointer, into prk: UnsafeMutableRawBufferPointer) {
        GesNetworkHmacSha256(key: salt).compute(ikm, into: prk)
    }

    // Expands with info made of a label and an optional suffix, which saves the callers a concatenation.
    static func expand(prk: UnsafeRawBufferPointer, label: UnsafeRawBufferPointer, suffix: UnsafeRawBufferPointer = UnsafeRawBufferPointer(start: nil, count: 0), into output: UnsafeMutableRawBufferPointer) {
        let hmac = GesNetworkHmacSha256(key: prk)
        var input = [UInt8](repeating: 0, count: hashBytes + label.count + suffix.count + 1)
        var block = [UInt8](repeating: 0, count: hashBytes)
        var previous = 0
        var counter: UInt8 = 1
        var written = 0
        while written < output.count {
            for index in 0..<previous { input[index] = block[index] }
            for index in 0..<label.count { input[previous + index] = label[index] }
            for index in 0..<suffix.count { input[previous + label.count + index] = suffix[index] }
            input[previous + label.count + suffix.count] = counter
            counter &+= 1
            let length = previous + label.count + suffix.count + 1
            input.withUnsafeBytes { source in
                block.withUnsafeMutableBytes { hmac.compute(UnsafeRawBufferPointer(rebasing: source[0..<length]), into: $0) }
            }
            let take = min(hashBytes, output.count - written)
            for index in 0..<take { output[written + index] = block[index] }
            written += take
            previous = hashBytes
        }
        for index in 0..<input.count { input[index] = 0 }
        for index in 0..<block.count { block[index] = 0 }
    }
}
