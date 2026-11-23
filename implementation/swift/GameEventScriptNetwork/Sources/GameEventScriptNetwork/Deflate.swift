// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The tables of the Deflate format (RFC 1951) shared by the encoder and the decoder.
enum GesNetworkDeflateTables {
    static let lengthBase: [Int] = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258]
    static let lengthExtra: [Int] = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0]
    static let distanceBase: [Int] = [
        1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577,
    ]
    static let distanceExtra: [Int] = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13]

    // The order in which a dynamic block lists the lengths of the code length code.
    static let codeLengthOrder: [Int] = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15]

    static func reverse(_ code: UInt32, _ length: Int) -> UInt32 {
        var code = code
        var reversed: UInt32 = 0
        for _ in 0..<length {
            reversed = (reversed << 1) | (code & 1)
            code >>= 1
        }
        return reversed
    }
}

// The reference Deflate encoder of transfers (specs/Network.md#deflate): one final block with the fixed Huffman code,
// greedy LZ77 matches over hash chains. Both ports produce the same bytes. The chain arrays are owned from creation on,
// so encoding allocates nothing. Not thread-safe.
final class GesNetworkDeflater {
    private static let window = 32768
    private static let mask = window - 1
    private static let maximumChain = 32
    private static let minimumMatch = 3
    private static let maximumMatch = 258

    // The bit-reversed fixed codes and their lengths of the literal/length symbols 0-287, and the length symbol
    // (minus 257) of each match length 3-258.
    private let literalCodes = UnsafeMutablePointer<UInt32>.allocate(capacity: 288)
    private let literalLengths = UnsafeMutablePointer<Int>.allocate(capacity: 288)
    private let lengthSymbols = UnsafeMutablePointer<Int>.allocate(capacity: maximumMatch + 1)
    private let distanceBase = UnsafeMutablePointer<Int>.allocate(capacity: 30)
    private let distanceExtra = UnsafeMutablePointer<Int>.allocate(capacity: 30)
    private let lengthBase = UnsafeMutablePointer<Int>.allocate(capacity: 29)
    private let lengthExtra = UnsafeMutablePointer<Int>.allocate(capacity: 29)
    private let head = UnsafeMutablePointer<Int32>.allocate(capacity: window)
    private let previous = UnsafeMutablePointer<Int32>.allocate(capacity: window)

    init() {
        for symbol in 0..<288 {
            let (code, length): (Int, Int)
            if symbol < 144 {
                (code, length) = (0x30 + symbol, 8)
            } else if symbol < 256 {
                (code, length) = (0x190 + symbol - 144, 9)
            } else if symbol < 280 {
                (code, length) = (symbol - 256, 7)
            } else {
                (code, length) = (0xc0 + symbol - 280, 8)
            }
            literalCodes[symbol] = GesNetworkDeflateTables.reverse(UInt32(code), length)
            literalLengths[symbol] = length
        }
        for symbol in 0..<29 {
            lengthBase[symbol] = GesNetworkDeflateTables.lengthBase[symbol]
            lengthExtra[symbol] = GesNetworkDeflateTables.lengthExtra[symbol]
            let end = symbol == 28 ? Self.maximumMatch : GesNetworkDeflateTables.lengthBase[symbol + 1] - 1
            for length in GesNetworkDeflateTables.lengthBase[symbol]...end { lengthSymbols[length] = symbol }
        }
        for code in 0..<30 {
            distanceBase[code] = GesNetworkDeflateTables.distanceBase[code]
            distanceExtra[code] = GesNetworkDeflateTables.distanceExtra[code]
        }
    }

    deinit {
        literalCodes.deallocate()
        literalLengths.deallocate()
        lengthSymbols.deallocate()
        distanceBase.deallocate()
        distanceExtra.deallocate()
        lengthBase.deallocate()
        lengthExtra.deallocate()
        head.deallocate()
        previous.deallocate()
    }

    // Compresses the data into the output. Returns the length written, or -1 when the stream does not fit the output.
    func compress(_ data: UnsafeRawBufferPointer, into output: UnsafeMutableRawBufferPointer) -> Int {
        head.update(repeating: -1, count: Self.window)
        var writer = BitWriter(output)
        writer.write(1, 1)
        writer.write(1, 2)
        let count = data.count
        var position = 0
        while position < count && !writer.overflow {
            var best = 0
            var distance = 0
            if position + Self.minimumMatch <= count {
                let limit = min(Self.maximumMatch, count - position)
                var candidate = Int(head[Self.hash(data, position)])
                var chain = 0
                while candidate >= 0 && chain < Self.maximumChain {
                    let gap = position - candidate
                    if gap > Self.window { break }
                    var length = 0
                    while length < limit && data[candidate + length] == data[position + length] { length += 1 }
                    if length > best {
                        best = length
                        distance = gap
                        if length == limit { break }
                    }
                    candidate = Int(previous[candidate & Self.mask])
                    chain += 1
                }
            }
            if best >= Self.minimumMatch {
                writeMatch(&writer, best, distance)
                let end = position + best
                while position < end {
                    insert(data, position)
                    position += 1
                }
            } else {
                let literal = Int(data[position])
                writer.write(literalCodes[literal], literalLengths[literal])
                insert(data, position)
                position += 1
            }
        }
        writer.write(literalCodes[256], literalLengths[256])
        return writer.finish()
    }

    private static func hash(_ data: UnsafeRawBufferPointer, _ position: Int) -> Int {
        ((Int(data[position]) << 10) ^ (Int(data[position + 1]) << 5) ^ Int(data[position + 2])) & mask
    }

    private func insert(_ data: UnsafeRawBufferPointer, _ position: Int) {
        if position + Self.minimumMatch > data.count { return }
        let hash = Self.hash(data, position)
        previous[position & Self.mask] = head[hash]
        head[hash] = Int32(position)
    }

    private func writeMatch(_ writer: inout BitWriter, _ length: Int, _ distance: Int) {
        let symbol = lengthSymbols[length]
        writer.write(literalCodes[257 + symbol], literalLengths[257 + symbol])
        writer.write(UInt32(length - lengthBase[symbol]), lengthExtra[symbol])
        var code = 0
        while code < 29 && distanceBase[code + 1] <= distance { code += 1 }
        writer.write(GesNetworkDeflateTables.reverse(UInt32(code), 5), 5)
        writer.write(UInt32(distance - distanceBase[code]), distanceExtra[code])
    }

    // Packs bits from the least significant end and stops writing once the output is full.
    private struct BitWriter {
        private let output: UnsafeMutableRawBufferPointer
        private var bits: UInt64 = 0
        private var count = 0
        private var position = 0
        private(set) var overflow = false

        init(_ output: UnsafeMutableRawBufferPointer) { self.output = output }

        mutating func write(_ value: UInt32, _ length: Int) {
            bits |= UInt64(value) << UInt64(count)
            count += length
            while count >= 8 {
                put(UInt8(truncatingIfNeeded: bits))
                bits >>= 8
                count -= 8
            }
        }

        mutating func finish() -> Int {
            if count > 0 { put(UInt8(truncatingIfNeeded: bits)) }
            bits = 0
            count = 0
            return overflow ? -1 : position
        }

        private mutating func put(_ value: UInt8) {
            if position == output.count {
                overflow = true
                return
            }
            output[position] = value
            position += 1
        }
    }
}

// A Deflate decoder (RFC 1951) for all block types. It accepts a stream only when it ends in a final block, fills the
// output exactly, and has no bytes after the final block's last byte; it rejects the code tables zlib rejects. The
// tables are owned from creation on, so decoding allocates nothing. Not thread-safe.
final class GesNetworkInflater {
    private static let maximumBits = 15

    private let literals = UnsafeMutablePointer<UInt16>.allocate(capacity: 1 << maximumBits)
    private let distances = UnsafeMutablePointer<UInt16>.allocate(capacity: 1 << maximumBits)
    private let codes = UnsafeMutablePointer<UInt16>.allocate(capacity: 1 << 7)
    private let lengths = UnsafeMutablePointer<UInt8>.allocate(capacity: 286 + 30)
    private let codeLengths = UnsafeMutablePointer<UInt8>.allocate(capacity: 19)
    private let counts = UnsafeMutablePointer<Int>.allocate(capacity: maximumBits + 1)
    private let next = UnsafeMutablePointer<Int>.allocate(capacity: maximumBits + 1)
    private let codeLengthOrder = UnsafeMutablePointer<Int>.allocate(capacity: 19)
    private let lengthBase = UnsafeMutablePointer<Int>.allocate(capacity: 29)
    private let lengthExtra = UnsafeMutablePointer<Int>.allocate(capacity: 29)
    private let distanceBase = UnsafeMutablePointer<Int>.allocate(capacity: 30)
    private let distanceExtra = UnsafeMutablePointer<Int>.allocate(capacity: 30)

    init() {
        for index in 0..<19 { codeLengthOrder[index] = GesNetworkDeflateTables.codeLengthOrder[index] }
        for index in 0..<29 {
            lengthBase[index] = GesNetworkDeflateTables.lengthBase[index]
            lengthExtra[index] = GesNetworkDeflateTables.lengthExtra[index]
        }
        for index in 0..<30 {
            distanceBase[index] = GesNetworkDeflateTables.distanceBase[index]
            distanceExtra[index] = GesNetworkDeflateTables.distanceExtra[index]
        }
    }

    deinit {
        literals.deallocate()
        distances.deallocate()
        codes.deallocate()
        lengths.deallocate()
        codeLengths.deallocate()
        counts.deallocate()
        next.deallocate()
        codeLengthOrder.deallocate()
        lengthBase.deallocate()
        lengthExtra.deallocate()
        distanceBase.deallocate()
        distanceExtra.deallocate()
    }

    // Decodes the stream into the output; true when it is valid and yields exactly the output's length.
    func inflate(_ input: UnsafeRawBufferPointer, into output: UnsafeMutableRawBufferPointer) -> Bool {
        var bits = BitReader(input)
        var written = 0
        while true {
            let final = bits.read(1)
            let type = bits.read(2)
            if final < 0 || type < 0 { return false }
            switch type {
            case 0:
                if !stored(&bits, output, &written) { return false }
            case 1:
                let (literalBits, distanceBits) = fixed()
                if !decodeCodes(&bits, output, &written, literalBits, distanceBits) { return false }
            case 2:
                guard let (literalBits, distanceBits) = dynamic(&bits) else { return false }
                if !decodeCodes(&bits, output, &written, literalBits, distanceBits) { return false }
            default:
                return false
            }
            if final == 1 { return written == output.count && bits.consumed == input.count }
        }
    }

    private func stored(_ bits: inout BitReader, _ output: UnsafeMutableRawBufferPointer, _ written: inout Int) -> Bool {
        bits.align()
        let length = bits.read(16)
        let complement = bits.read(16)
        if length < 0 || complement < 0 || (length ^ 0xffff) != complement { return false }
        if length > output.count - written { return false }
        if !bits.copyBytes(UnsafeMutableRawBufferPointer(rebasing: output[written..<written + length])) { return false }
        written += length
        return true
    }

    private func fixed() -> (Int, Int) {
        lengths.update(repeating: 8, count: 144)
        (lengths + 144).update(repeating: 9, count: 112)
        (lengths + 256).update(repeating: 7, count: 24)
        (lengths + 280).update(repeating: 8, count: 8)
        let literalBits = build(lengths, 288, literals, codeLengthCode: false) ?? 0
        // The fixed distance code has 32 codes; the last two are invalid when used.
        lengths.update(repeating: 5, count: 32)
        let distanceBits = build(lengths, 32, distances, codeLengthCode: false) ?? 0
        return (literalBits, distanceBits)
    }

    private func dynamic(_ bits: inout BitReader) -> (Int, Int)? {
        var literalCount = bits.read(5)
        var distanceCount = bits.read(5)
        var codeCount = bits.read(4)
        if literalCount < 0 || distanceCount < 0 || codeCount < 0 { return nil }
        literalCount += 257
        distanceCount += 1
        codeCount += 4
        if literalCount > 286 || distanceCount > 30 { return nil }
        codeLengths.update(repeating: 0, count: 19)
        for index in 0..<codeCount {
            let length = bits.read(3)
            if length < 0 { return nil }
            codeLengths[codeLengthOrder[index]] = UInt8(length)
        }
        guard let codeBits = build(codeLengths, 19, codes, codeLengthCode: true) else { return nil }
        let total = literalCount + distanceCount
        var filled = 0
        while filled < total {
            let symbol = bits.decode(codes, codeBits)
            if symbol < 0 { return nil }
            if symbol < 16 {
                lengths[filled] = UInt8(symbol)
                filled += 1
                continue
            }
            var value: UInt8 = 0
            var repeatCount: Int
            if symbol == 16 {
                if filled == 0 { return nil }
                value = lengths[filled - 1]
                repeatCount = bits.read(2)
                if repeatCount < 0 { return nil }
                repeatCount += 3
            } else if symbol == 17 {
                repeatCount = bits.read(3)
                if repeatCount < 0 { return nil }
                repeatCount += 3
            } else {
                repeatCount = bits.read(7)
                if repeatCount < 0 { return nil }
                repeatCount += 11
            }
            if repeatCount > total - filled { return nil }
            (lengths + filled).update(repeating: value, count: repeatCount)
            filled += repeatCount
        }
        if lengths[256] == 0 { return nil }
        guard let literalBits = build(lengths, literalCount, literals, codeLengthCode: false),
            let distanceBits = build(lengths + literalCount, distanceCount, distances, codeLengthCode: false)
        else { return nil }
        return (literalBits, distanceBits)
    }

    // Builds a lookup table indexed by the next bits of the stream and returns its bit width; each entry holds
    // symbol << 4 | length, 0 marks a code that does not exist. Over-subscribed lengths fail; incomplete ones only for a
    // single code of length 1 in a literal or distance table, and an empty table only fails when it is used.
    private func build(_ lengths: UnsafeMutablePointer<UInt8>, _ count: Int, _ table: UnsafeMutablePointer<UInt16>, codeLengthCode: Bool) -> Int? {
        counts.update(repeating: 0, count: Self.maximumBits + 1)
        for symbol in 0..<count { counts[Int(lengths[symbol])] += 1 }
        counts[0] = 0
        var tableBits = Self.maximumBits
        while tableBits > 0 && counts[tableBits] == 0 { tableBits -= 1 }
        if tableBits == 0 { return 0 }
        var left = 1
        for length in 1...Self.maximumBits {
            left = (left << 1) - counts[length]
            if left < 0 { return nil }
        }
        if left > 0 && (codeLengthCode || tableBits != 1) { return nil }
        var code = 0
        for length in 1...Self.maximumBits {
            code = (code + counts[length - 1]) << 1
            next[length] = code
        }
        let size = 1 << tableBits
        table.update(repeating: 0, count: size)
        for symbol in 0..<count {
            let length = Int(lengths[symbol])
            if length == 0 { continue }
            var index = Int(GesNetworkDeflateTables.reverse(UInt32(next[length]), length))
            next[length] += 1
            let entry = UInt16((symbol << 4) | length)
            while index < size {
                table[index] = entry
                index += 1 << length
            }
        }
        return tableBits
    }

    private func decodeCodes(_ bits: inout BitReader, _ output: UnsafeMutableRawBufferPointer, _ written: inout Int, _ literalBits: Int, _ distanceBits: Int) -> Bool {
        while true {
            var symbol = bits.decode(literals, literalBits)
            if symbol < 0 { return false }
            if symbol < 256 {
                if written == output.count { return false }
                output[written] = UInt8(symbol)
                written += 1
                continue
            }
            if symbol == 256 { return true }
            symbol -= 257
            if symbol >= 29 { return false }
            var extra = bits.read(lengthExtra[symbol])
            if extra < 0 { return false }
            let length = lengthBase[symbol] + extra
            let code = bits.decode(distances, distanceBits)
            if code < 0 || code >= 30 { return false }
            extra = bits.read(distanceExtra[code])
            if extra < 0 { return false }
            let distance = distanceBase[code] + extra
            if distance > written || length > output.count - written { return false }
            for _ in 0..<length {
                output[written] = output[written - distance]
                written += 1
            }
        }
    }

    // Reads bits from the least significant end of each byte.
    private struct BitReader {
        private let input: UnsafeRawBufferPointer
        private var position = 0
        private var bits: UInt64 = 0
        private var count = 0

        init(_ input: UnsafeRawBufferPointer) { self.input = input }

        // The bytes read so far, not counting whole bytes still buffered.
        var consumed: Int { position - (count >> 3) }

        // Reads up to 16 bits; -1 when the stream ends first.
        mutating func read(_ length: Int) -> Int {
            if length == 0 { return 0 }
            fill(length)
            if count < length { return -1 }
            let value = Int(bits & ((1 << UInt64(length)) - 1))
            bits >>= UInt64(length)
            count -= length
            return value
        }

        mutating func decode(_ table: UnsafeMutablePointer<UInt16>, _ tableBits: Int) -> Int {
            if tableBits == 0 { return -1 }
            fill(tableBits)
            let entry = Int(table[Int(bits & ((1 << UInt64(tableBits)) - 1))])
            let length = entry & 15
            if length == 0 || length > count { return -1 }
            bits >>= UInt64(length)
            count -= length
            return entry >> 4
        }

        // Drops the bits up to the next byte boundary and returns buffered whole bytes to the input.
        mutating func align() {
            position -= count >> 3
            bits = 0
            count = 0
        }

        mutating func copyBytes(_ target: UnsafeMutableRawBufferPointer) -> Bool {
            if target.count > input.count - position { return false }
            if target.count > 0 { target.copyMemory(from: UnsafeRawBufferPointer(rebasing: input[position..<position + target.count])) }
            position += target.count
            return true
        }

        private mutating func fill(_ length: Int) {
            while count < length && position < input.count {
                bits |= UInt64(input[position]) << UInt64(count)
                position += 1
                count += 8
            }
        }
    }
}
