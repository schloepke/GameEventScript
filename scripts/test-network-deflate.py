#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Checks the Deflate fixture of GES network transfers against an independent oracle and zlib.

The fixture conformance/fixtures/Network/deflate.json holds the reference encoder's exact output for generated data
(specs/Network.md#deflate) and zlib-produced or handwritten streams with the decoder's expected verdict. Both native
test suites read it. This script recomputes every encoder output with its own implementation, requires zlib (raw,
wbits -15) to decode each one to the generated data, requires zlib to accept exactly the streams the fixture accepts,
and checks that the fixture is current. --write regenerates the fixture; --self-test qualifies the checks.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FIXTURE = ROOT / "conformance" / "fixtures" / "Network" / "deflate.json"

LENGTH_BASE = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258]
LENGTH_EXTRA = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0]
DISTANCE_BASE = [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073,
                 4097, 6145, 8193, 12289, 16385, 24577]
DISTANCE_EXTRA = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13]


def generate(spec: dict) -> bytes:
    """The fixture's data generator: a 31-bit LCG, bytes reduced to the alphabet, then a repeat of the first bytes."""
    state = spec["seed"]
    out = bytearray()
    for _ in range(spec["length"]):
        state = (state * 1103515245 + 12345) % (1 << 31)
        out.append(((state >> 16) & 0xFF) % spec["alphabet"])
    out += out[:spec.get("repeat", 0)]
    return bytes(out)


class Bits:
    def __init__(self) -> None:
        self.value = 0
        self.count = 0

    def write(self, value: int, length: int) -> None:
        self.value |= value << self.count
        self.count += length

    def huffman(self, code: int, length: int) -> None:
        self.write(int(format(code, f"0{length}b")[::-1], 2) if length else 0, length)

    def bytes(self) -> bytes:
        return self.value.to_bytes((self.count + 7) // 8, "little")


def fixed_literal(bits: Bits, symbol: int) -> None:
    if symbol < 144:
        bits.huffman(0x30 + symbol, 8)
    elif symbol < 256:
        bits.huffman(0x190 + symbol - 144, 9)
    elif symbol < 280:
        bits.huffman(symbol - 256, 7)
    else:
        bits.huffman(0xC0 + symbol - 280, 8)


def reference_deflate(data: bytes) -> bytes:
    """The reference encoder: one final fixed block, greedy matches over 32 hash chain candidates."""
    head: dict[int, int] = {}
    previous: dict[int, int] = {}

    def key(position: int) -> int:
        return ((data[position] << 10) ^ (data[position + 1] << 5) ^ data[position + 2]) & 0x7FFF

    def insert(position: int) -> None:
        if position + 3 <= len(data):
            h = key(position)
            previous[position & 0x7FFF] = head.get(h, -1)
            head[h] = position

    bits = Bits()
    bits.write(1, 1)
    bits.write(1, 2)
    position = 0
    while position < len(data):
        best, distance = 0, 0
        if position + 3 <= len(data):
            limit = min(258, len(data) - position)
            candidate = head.get(key(position), -1)
            chain = 0
            while candidate >= 0 and chain < 32 and position - candidate <= 32768:
                length = 0
                while length < limit and data[candidate + length] == data[position + length]:
                    length += 1
                if length > best:
                    best, distance = length, position - candidate
                    if length == limit:
                        break
                candidate = previous[candidate & 0x7FFF]
                chain += 1
        if best >= 3:
            symbol = max(index for index, base in enumerate(LENGTH_BASE) if base <= best)
            fixed_literal(bits, 257 + symbol)
            bits.write(best - LENGTH_BASE[symbol], LENGTH_EXTRA[symbol])
            code = max(index for index, base in enumerate(DISTANCE_BASE) if base <= distance)
            bits.huffman(code, 5)
            bits.write(distance - DISTANCE_BASE[code], DISTANCE_EXTRA[code])
            for inside in range(position, position + best):
                insert(inside)
            position += best
        else:
            fixed_literal(bits, data[position])
            insert(position)
            position += 1
    fixed_literal(bits, 256)
    return bits.bytes()


def zlib_inflate(stream: bytes, size: int) -> bytes | None:
    """zlib's verdict on a raw stream: the data when it ends in a final block, has no trailing bytes and fits size."""
    decoder = zlib.decompressobj(-15)
    try:
        out = decoder.decompress(stream, size + 1)
    except zlib.error:
        return None
    if not decoder.eof or decoder.unused_data or decoder.unconsumed_tail or len(out) != size:
        return None
    return out


def zlib_deflate(data: bytes, level: int, strategy: int = zlib.Z_DEFAULT_STRATEGY, split: int | None = None) -> bytes:
    encoder = zlib.compressobj(level, zlib.DEFLATED, -15, 8, strategy)
    if split is None:
        return encoder.compress(data) + encoder.flush()
    return encoder.compress(data[:split]) + encoder.flush(zlib.Z_FULL_FLUSH) + encoder.compress(data[split:]) + encoder.flush()


# Streams up to this length are spelled out in hex; longer ones only by length and SHA-256.
SPELLED_OUT = 1024

ENCODE_CASES = [
    ("empty", {"length": 0, "seed": 1, "alphabet": 256}),
    ("one byte", {"length": 1, "seed": 7, "alphabet": 256}),
    ("two bytes", {"length": 2, "seed": 7, "alphabet": 1}),
    ("run of one byte", {"length": 1000, "seed": 3, "alphabet": 1}),
    ("four symbols", {"length": 1000, "seed": 11, "alphabet": 4}),
    ("random bytes", {"length": 300, "seed": 5, "alphabet": 256}),
    ("window minus one", {"length": 32767, "seed": 13, "alphabet": 16}),
    ("window", {"length": 32768, "seed": 17, "alphabet": 16}),
    ("window plus one", {"length": 32769, "seed": 19, "alphabet": 16}),
    ("match at distance 32768", {"length": 32768, "seed": 23, "alphabet": 256, "repeat": 300}),
    ("long data", {"length": 100000, "seed": 29, "alphabet": 8}),
]


def handwritten_invalid() -> list[tuple[str, bytes, int]]:
    cases = []
    cases.append(("reserved block type", bytes([0x07]), 0))
    cases.append(("empty input", b"", 0))
    cases.append(("stored length complement mismatch", bytes([0x01, 0x01, 0x00, 0x00, 0x00, 0x41]), 1))
    cases.append(("stored block truncated", bytes([0x01, 0x02, 0x00, 0xFD, 0xFF, 0x41]), 2))
    bits = Bits()
    bits.write(1, 1)
    bits.write(1, 2)
    fixed_literal(bits, 257)
    bits.huffman(0, 5)
    fixed_literal(bits, 256)
    cases.append(("distance beyond output", bits.bytes(), 3))
    for symbol in (286, 287):
        bits = Bits()
        bits.write(1, 1)
        bits.write(1, 2)
        fixed_literal(bits, 65)
        fixed_literal(bits, symbol)
        fixed_literal(bits, 256)
        cases.append((f"fixed literal symbol {symbol}", bits.bytes(), 1))
    for code in (30, 31):
        bits = Bits()
        bits.write(1, 1)
        bits.write(1, 2)
        fixed_literal(bits, 65)
        fixed_literal(bits, 257)
        bits.huffman(code, 5)
        fixed_literal(bits, 256)
        cases.append((f"fixed distance code {code}", bits.bytes(), 4))
    bits = Bits()
    bits.write(1, 1)
    bits.write(2, 2)
    bits.write(30, 5)
    bits.write(0, 5)
    bits.write(15, 4)
    bits.write(0, 64)
    cases.append(("too many literal lengths", bits.bytes(), 0))
    bits = Bits()
    bits.write(1, 1)
    bits.write(2, 2)
    bits.write(0, 5)
    bits.write(30, 5)
    bits.write(15, 4)
    bits.write(0, 64)
    cases.append(("too many distance codes", bits.bytes(), 0))
    # A dynamic block whose code length code gives three symbols length 1: over-subscribed.
    bits = Bits()
    bits.write(1, 1)
    bits.write(2, 2)
    bits.write(0, 5)
    bits.write(0, 5)
    bits.write(0, 4)
    for value in (1, 1, 1, 0):
        bits.write(value, 3)
    bits.write(0, 32)
    cases.append(("over-subscribed code length code", bits.bytes(), 0))
    # Only one code length symbol of length 1: incomplete, which zlib rejects for the code length code.
    bits = Bits()
    bits.write(1, 1)
    bits.write(2, 2)
    bits.write(0, 5)
    bits.write(0, 5)
    bits.write(0, 4)
    for value in (0, 0, 0, 1):
        bits.write(value, 3)
    bits.write(0, 32)
    cases.append(("incomplete code length code", bits.bytes(), 0))
    # Code 16 (repeat the previous length) as the first length.
    bits = Bits()
    bits.write(1, 1)
    bits.write(2, 2)
    bits.write(0, 5)
    bits.write(0, 5)
    bits.write(0, 4)
    for value in (1, 0, 0, 1):
        bits.write(value, 3)
    bits.huffman(0, 1)
    bits.write(0, 32)
    cases.append(("repeat without a previous length", bits.bytes(), 0))
    return cases


def dynamic_missing_end() -> bytes:
    """A dynamic block where symbol 256 has no code: literals 0-255 all get length 8 via code length 8 and repeats."""
    bits = Bits()
    bits.write(1, 1)
    bits.write(2, 2)
    bits.write(0, 5)  # 257 literal lengths
    bits.write(0, 5)  # 1 distance length
    bits.write(14, 4)  # 18 code length lengths: order 16 17 18 0 8 7 9 6 10 5 11 4 12 3 13 2 14 1
    order = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1]
    lengths = {16: 2, 8: 2, 0: 1}
    for symbol in order:
        bits.write(lengths.get(symbol, 0), 3)
    # Canonical codes: 0 -> "0", 8 -> "10", 16 -> "11".
    bits.huffman(0b10, 2)
    for _ in range(255 // 6):
        bits.huffman(0b11, 2)
        bits.write(3, 2)
    for _ in range(255 - 6 * (255 // 6)):
        bits.huffman(0b10, 2)
    bits.huffman(0b0, 1)
    bits.huffman(0b0, 1)
    bits.write(0, 16)
    return bits.bytes()


def build_fixture() -> dict:
    encode = []
    for name, spec in ENCODE_CASES:
        data = generate(spec)
        stream = reference_deflate(data)
        encode.append({
            "name": name,
            "data": spec,
            "deflate": stream.hex() if len(stream) <= SPELLED_OUT else None,
            "deflateLength": len(stream),
            "deflateSha256": hashlib.sha256(stream).hexdigest(),
        })
    inflate = []
    text = generate({"length": 4000, "seed": 31, "alphabet": 6})
    random = generate({"length": 2000, "seed": 37, "alphabet": 256})
    produced = [
        ("zlib stored", zlib_deflate(random, 0), random),
        ("zlib stored in two blocks", zlib_deflate(random, 0, split=700), random),
        ("zlib stored empty", zlib_deflate(b"", 0), b""),
        ("zlib fixed", zlib_deflate(text, 9, zlib.Z_FIXED), text),
        ("zlib dynamic", zlib_deflate(text, 9), text),
        ("zlib dynamic and stored", zlib_deflate(text + random, 9, split=4000), text + random),
        ("zlib dynamic empty", zlib_deflate(b"", 9), b""),
        ("zlib dynamic, long run", zlib_deflate(bytes(70000), 9), bytes(70000)),
    ]
    for name, stream, data in produced:
        inflate.append({"name": name, "stream": stream.hex(), "size": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    reference = reference_deflate(generate(ENCODE_CASES[4][1]))
    inflate.append({"name": "trailing byte", "stream": (reference + b"\x00").hex(), "size": 1000, "sha256": None})
    inflate.append({"name": "truncated", "stream": reference[:-1].hex(), "size": 1000, "sha256": None})
    inflate.append({"name": "size larger than the output", "stream": reference.hex(), "size": 1001, "sha256": None})
    inflate.append({"name": "size smaller than the output", "stream": reference.hex(), "size": 999, "sha256": None})
    inflate.append({"name": "missing end-of-block code", "stream": dynamic_missing_end().hex(), "size": 0, "sha256": None})
    for name, stream, size in handwritten_invalid():
        inflate.append({"name": name, "stream": stream.hex(), "size": size, "sha256": None})
    return {
        "description": "Deflate cases of GES network transfers (specs/Network.md#deflate), generated by "
                       "scripts/test-network-deflate.py --write. data: a 31-bit LCG x = (x * 1103515245 + 12345) mod 2^31 "
                       "from seed, each byte ((x >> 16) & 0xFF) mod alphabet, then the first repeat bytes again. "
                       "deflate: the reference encoder's exact output in hex up to 1024 bytes; deflateLength and "
                       "deflateSha256 always describe it. inflate: a stream, the size it must yield, and "
                       "the SHA-256 of the output, or null when the decoder must reject it.",
        "encode": encode,
        "inflate": inflate,
    }


def check(fixture: dict) -> list[str]:
    errors = []
    for case in fixture["encode"]:
        data = generate(case["data"])
        stream = reference_deflate(data)
        spelled = case["deflate"]
        if (spelled is not None and bytes.fromhex(spelled) != stream) or len(stream) != case["deflateLength"] \
                or hashlib.sha256(stream).hexdigest() != case["deflateSha256"]:
            errors.append(f"encode {case['name']}: the oracle produces other bytes")
        if zlib_inflate(stream, len(data)) != data:
            errors.append(f"encode {case['name']}: zlib does not decode the data")
    for case in fixture["inflate"]:
        out = zlib_inflate(bytes.fromhex(case["stream"]), case["size"])
        expected = case["sha256"]
        actual = None if out is None else hashlib.sha256(out).hexdigest()
        if actual != expected:
            errors.append(f"inflate {case['name']}: zlib gives {actual}, the fixture {expected}")
    return errors


def self_test() -> int:
    fixture = build_fixture()
    if check(fixture):
        print("self-test: the generated fixture fails", file=sys.stderr)
        return 1
    broken = json.loads(json.dumps(fixture))
    stream = bytearray.fromhex(broken["encode"][4]["deflate"])
    stream[3] ^= 0x10
    broken["encode"][4]["deflate"] = stream.hex()
    broken["encode"][8]["deflateSha256"] = "00" * 32
    broken["inflate"][0]["sha256"] = None
    broken["inflate"][-1]["sha256"] = "00" * 32
    errors = check(broken)
    if len(errors) < 4:
        print(f"self-test: a broken fixture passes: {errors}", file=sys.stderr)
        return 1
    print("network deflate self-test passed")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="regenerate the fixture")
    parser.add_argument("--self-test", action="store_true", help="qualify the checks with broken fixtures")
    arguments = parser.parse_args()
    if arguments.self_test:
        return self_test()
    expected = build_fixture()
    text = json.dumps(expected, indent=1) + "\n"
    if arguments.write:
        FIXTURE.parent.mkdir(parents=True, exist_ok=True)
        FIXTURE.write_text(text, encoding="utf-8")
    errors = check(expected)
    if not arguments.write and (not FIXTURE.exists() or FIXTURE.read_text(encoding="utf-8") != text):
        errors.append(f"{FIXTURE.relative_to(ROOT)} is not current; run with --write")
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    print(f"network deflate: {len(expected['encode'])} encoder and {len(expected['inflate'])} decoder cases passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
