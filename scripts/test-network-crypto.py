#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Checks the cryptography fixture of GES network encryption against an independent oracle.

The fixture conformance/fixtures/Network/crypto.json holds X25519 (RFC 7748), ChaCha20-Poly1305 (RFC 8439) and
HKDF-SHA256 (RFC 5869) results for published and generated inputs (specs/Network.md#cipher-suite). Both native test
suites read it and the platform cross-checks compare against it. This script recomputes every result with plain
big-integer arithmetic, requires the published RFC vectors, and checks that the fixture is current. --write
regenerates the fixture; --self-test qualifies the checks with broken fixtures.
"""

from __future__ import annotations

import argparse
import hashlib
import hmac
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FIXTURE = ROOT / "conformance" / "fixtures" / "Network" / "crypto.json"

P25519 = 2**255 - 19
P1305 = 2**130 - 5


def x25519(scalar: bytes, u_bytes: bytes) -> bytes:
    """RFC 7748 section 5: clamped scalar, masked high bit, the Montgomery ladder over the field."""
    k = bytearray(scalar)
    k[0] &= 248
    k[31] &= 127
    k[31] |= 64
    k = int.from_bytes(k, "little")
    u = int.from_bytes(u_bytes, "little") & ((1 << 255) - 1)
    x1, x2, z2, x3, z3, swap = u % P25519, 1, 0, u % P25519, 1, 0
    for t in reversed(range(255)):
        bit = (k >> t) & 1
        swap ^= bit
        if swap:
            x2, x3, z2, z3 = x3, x2, z3, z2
        swap = bit
        a, b = (x2 + z2) % P25519, (x2 - z2) % P25519
        aa, bb = a * a % P25519, b * b % P25519
        e = (aa - bb) % P25519
        c, d = (x3 + z3) % P25519, (x3 - z3) % P25519
        da, cb = d * a % P25519, c * b % P25519
        x3 = (da + cb) ** 2 % P25519
        z3 = x1 * (da - cb) ** 2 % P25519
        x2 = aa * bb % P25519
        z2 = e * (aa + 121665 * e) % P25519
    if swap:
        x2, z2 = x3, z3
    return (x2 * pow(z2, P25519 - 2, P25519) % P25519).to_bytes(32, "little")


def rotl(value: int, count: int) -> int:
    return ((value << count) | (value >> (32 - count))) & 0xFFFFFFFF


def chacha20_block(key: bytes, counter: int, nonce: bytes) -> bytes:
    """RFC 8439 section 2.3: 20 rounds over the constant, key, 32-bit counter and 96-bit nonce."""
    words = [0x61707865, 0x3320646E, 0x79622D32, 0x6B206574]
    words += [int.from_bytes(key[i:i + 4], "little") for i in range(0, 32, 4)]
    words += [counter & 0xFFFFFFFF] + [int.from_bytes(nonce[i:i + 4], "little") for i in range(0, 12, 4)]
    state = list(words)

    def quarter(a: int, b: int, c: int, d: int) -> None:
        state[a] = (state[a] + state[b]) & 0xFFFFFFFF; state[d] = rotl(state[d] ^ state[a], 16)
        state[c] = (state[c] + state[d]) & 0xFFFFFFFF; state[b] = rotl(state[b] ^ state[c], 12)
        state[a] = (state[a] + state[b]) & 0xFFFFFFFF; state[d] = rotl(state[d] ^ state[a], 8)
        state[c] = (state[c] + state[d]) & 0xFFFFFFFF; state[b] = rotl(state[b] ^ state[c], 7)

    for _ in range(10):
        quarter(0, 4, 8, 12); quarter(1, 5, 9, 13); quarter(2, 6, 10, 14); quarter(3, 7, 11, 15)
        quarter(0, 5, 10, 15); quarter(1, 6, 11, 12); quarter(2, 7, 8, 13); quarter(3, 4, 9, 14)
    return b"".join(((state[i] + words[i]) & 0xFFFFFFFF).to_bytes(4, "little") for i in range(16))


def chacha20(key: bytes, counter: int, nonce: bytes, data: bytes) -> bytes:
    out = bytearray()
    for index in range(0, len(data), 64):
        block = chacha20_block(key, counter + index // 64, nonce)
        out += bytes(x ^ y for x, y in zip(data[index:index + 64], block))
    return bytes(out)


def poly1305(key: bytes, message: bytes) -> bytes:
    """RFC 8439 section 2.5: clamped r, 16-byte blocks with a high 1 bit, plus s modulo 2^128."""
    r = int.from_bytes(key[:16], "little") & 0x0FFFFFFC0FFFFFFC0FFFFFFC0FFFFFFF
    s = int.from_bytes(key[16:], "little")
    accumulator = 0
    for index in range(0, len(message), 16):
        block = message[index:index + 16] + b"\x01"
        accumulator = (accumulator + int.from_bytes(block, "little")) * r % P1305
    return ((accumulator + s) % (1 << 128)).to_bytes(16, "little")


def pad16(data: bytes) -> bytes:
    return b"\x00" * (-len(data) % 16)


def aead_seal(key: bytes, nonce: bytes, aad: bytes, plaintext: bytes) -> bytes:
    """RFC 8439 section 2.8: ciphertext followed by the 16-byte tag."""
    one_time_key = chacha20_block(key, 0, nonce)[:32]
    ciphertext = chacha20(key, 1, nonce, plaintext)
    mac = aad + pad16(aad) + ciphertext + pad16(ciphertext) + len(aad).to_bytes(8, "little") + len(ciphertext).to_bytes(8, "little")
    return ciphertext + poly1305(one_time_key, mac)


def aead_open(key: bytes, nonce: bytes, aad: bytes, sealed: bytes) -> bytes | None:
    if len(sealed) < 16:
        return None
    plaintext = chacha20(key, 1, nonce, sealed[:-16])
    return plaintext if hmac.compare_digest(aead_seal(key, nonce, aad, plaintext), sealed) else None


def hkdf_extract(salt: bytes, ikm: bytes) -> bytes:
    return hmac.new(salt or b"\x00" * 32, ikm, hashlib.sha256).digest()


def hkdf_expand(prk: bytes, info: bytes, length: int) -> bytes:
    out, block, counter = b"", b"", 1
    while len(out) < length:
        block = hmac.new(prk, block + info + bytes([counter]), hashlib.sha256).digest()
        out += block
        counter += 1
    return out[:length]


def derive(label: str, length: int) -> bytes:
    """Deterministic generated input: SHA-256 blocks over the label and a counter."""
    out, counter = b"", 0
    while len(out) < length:
        out += hashlib.sha256(f"{label}/{counter}".encode()).digest()
        counter += 1
    return out[:length]


# Published vectors the oracle must reproduce before it is trusted.
RFC_X25519 = [
    ("a546e36bf0527c9d3b16154b82465edd62144c0ac1fc5a18506a2244ba449ac4",
     "e6db6867583030db3594c1a424b15f7c726624ec26b3353b10a903a6d0ab1c4c",
     "c3da55379de9c6908e94ea4df28d084f32eccf03491c71f754b4075577a28552"),
]
RFC_ALICE = ("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a",
             "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a")
RFC_BOB = ("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb",
           "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f")
RFC_SHARED = "4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742"
RFC_ITERATED = {1: "422c8e7a6227d7bca1350b3e2bb7279f7897b87bb6854b783c60e80311ae3079",
                1000: "684cf59ba83309552800ef566f2f4d3c1c3887c49360e3875f2eb94d99532c51"}
RFC_POLY1305 = ("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b",
                b"Cryptographic Forum Research Group".hex(), "a8061dc1305136c6c22b8baf0c0127a9")
RFC_AEAD_TAG = "1ae10b594f09e26a7e902ecbd0600691"
RFC_HKDF = ("0b" * 22, "000102030405060708090a0b0c", "f0f1f2f3f4f5f6f7f8f9", 42,
            "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865")


def check_published() -> list[str]:
    errors = []
    for scalar, u, out in RFC_X25519:
        if x25519(bytes.fromhex(scalar), bytes.fromhex(u)).hex() != out:
            errors.append(f"oracle misses RFC 7748 X25519 vector {scalar[:8]}")
    base = (9).to_bytes(32, "little")
    for private, public in (RFC_ALICE, RFC_BOB):
        if x25519(bytes.fromhex(private), base).hex() != public:
            errors.append(f"oracle misses RFC 7748 public key {public[:8]}")
    if x25519(bytes.fromhex(RFC_ALICE[0]), bytes.fromhex(RFC_BOB[1])).hex() != RFC_SHARED:
        errors.append("oracle misses the RFC 7748 shared secret")
    k = u = base
    for iteration in range(1, 1001):
        k, u = x25519(k, u), k
        if iteration in RFC_ITERATED and k.hex() != RFC_ITERATED[iteration]:
            errors.append(f"oracle misses the RFC 7748 iterated vector after {iteration}")
    key, message, tag = RFC_POLY1305
    if poly1305(bytes.fromhex(key), bytes.fromhex(message)).hex() != tag:
        errors.append("oracle misses the RFC 8439 Poly1305 vector")
    sealed = aead_seal(bytes(range(0x80, 0xA0)), bytes.fromhex("070000004041424344454647"),
                       bytes.fromhex("50515253c0c1c2c3c4c5c6c7"), RFC_AEAD_PLAINTEXT)
    if sealed[-16:].hex() != RFC_AEAD_TAG:
        errors.append("oracle misses the RFC 8439 AEAD tag")
    ikm, salt, info, length, okm = RFC_HKDF
    if hkdf_expand(hkdf_extract(bytes.fromhex(salt), bytes.fromhex(ikm)), bytes.fromhex(info), length).hex() != okm:
        errors.append("oracle misses the RFC 5869 HKDF vector")
    return errors


RFC_AEAD_PLAINTEXT = (b"Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, "
                      b"sunscreen would be it.")


def build_fixture() -> dict:
    x25519_cases = []
    base = (9).to_bytes(32, "little")
    for private, public in (RFC_ALICE, RFC_BOB):
        x25519_cases.append({"name": f"rfc7748 public {public[:8]}", "scalar": private, "u": base.hex(), "result": public})
    x25519_cases.append({"name": "rfc7748 shared", "scalar": RFC_ALICE[0], "u": RFC_BOB[1], "result": RFC_SHARED})
    for scalar, u, out in RFC_X25519:
        x25519_cases.append({"name": f"rfc7748 vector {scalar[:8]}", "scalar": scalar, "u": u, "result": out})
    special_u = {
        "u zero": 0,
        "u one": 1,
        "u p-1": P25519 - 1,
        "u p (non-canonical zero)": P25519,
        "u p+1 (non-canonical one)": P25519 + 1,
        "u 2^255-1 (high bit masked)": 2**255 - 1,
        "u 2^256-1 (high bit masked)": 2**256 - 1,
        "u order-8 point": int.from_bytes(bytes.fromhex("5f9c95bca3508c24b1d0b1559c83ef5b04445cc4581c8e86d8224eddd09f1157"), "little"),
        "u order-8 point e0eb": int.from_bytes(bytes.fromhex("e0eb7a7c3b41b8ae1656e3faf19fc46ada098deb9c32b1fd866205165f49b800"), "little"),
    }
    for index, (name, value) in enumerate(special_u.items()):
        scalar = derive(f"x25519 special {index}", 32)
        u = value.to_bytes(32, "little")
        x25519_cases.append({"name": name, "scalar": scalar.hex(), "u": u.hex(), "result": x25519(scalar, u).hex()})
    for index in range(24):
        scalar = derive(f"x25519 scalar {index}", 32)
        u = derive(f"x25519 u {index}", 32)
        x25519_cases.append({"name": f"generated {index}", "scalar": scalar.hex(), "u": u.hex(), "result": x25519(scalar, u).hex()})

    poly_cases = [{"name": "rfc8439 2.5.2", "key": RFC_POLY1305[0], "message": RFC_POLY1305[1], "tag": RFC_POLY1305[2]}]
    poly_keys = {
        "maximal r and s": bytes([0xFF] * 32),
        "zero r": bytes(16) + derive("poly s", 16),
        "generated key": derive("poly key", 32),
    }
    for key_name, key in poly_keys.items():
        for length in (0, 1, 15, 16, 17, 31, 32, 33, 64, 129):
            for fill_name, message in (("ones", bytes([0xFF] * length)), ("generated", derive(f"poly {key_name} {length}", length))):
                poly_cases.append({"name": f"{key_name}, {length} bytes {fill_name}", "key": key.hex(), "message": message.hex(),
                                   "tag": poly1305(key, message).hex()})

    aead_cases = [{
        "name": "rfc8439 2.8.2", "key": bytes(range(0x80, 0xA0)).hex(), "nonce": "070000004041424344454647",
        "aad": "50515253c0c1c2c3c4c5c6c7", "plaintext": RFC_AEAD_PLAINTEXT.hex(),
        "sealed": aead_seal(bytes(range(0x80, 0xA0)), bytes.fromhex("070000004041424344454647"),
                            bytes.fromhex("50515253c0c1c2c3c4c5c6c7"), RFC_AEAD_PLAINTEXT).hex(),
    }]
    for index, (aad_length, length) in enumerate([(0, 0), (0, 1), (7, 15), (12, 16), (16, 17), (1, 63), (20, 64), (3, 65),
                                                  (40, 128), (0, 200), (300, 1184)]):
        key, nonce = derive(f"aead key {index}", 32), derive(f"aead nonce {index}", 12)
        aad, plaintext = derive(f"aead aad {index}", aad_length), derive(f"aead plaintext {index}", length)
        aead_cases.append({"name": f"generated {aad_length} aad, {length} bytes", "key": key.hex(), "nonce": nonce.hex(),
                           "aad": aad.hex(), "plaintext": plaintext.hex(), "sealed": aead_seal(key, nonce, aad, plaintext).hex()})
    rejected = []
    base_case = aead_cases[3]
    sealed = bytes.fromhex(base_case["sealed"])
    for name, changed_sealed, changed_aad in (
            ("flipped ciphertext bit", bytes([sealed[0] ^ 1]) + sealed[1:], None),
            ("flipped tag bit", sealed[:-1] + bytes([sealed[-1] ^ 0x80]), None),
            ("truncated tag", sealed[:-1], None),
            ("shorter than a tag", sealed[:15], None),
            ("changed associated data", sealed, bytes.fromhex(base_case["aad"])[:-1] + b"\x00")):
        aad = changed_aad if changed_aad is not None else bytes.fromhex(base_case["aad"])
        if aead_open(bytes.fromhex(base_case["key"]), bytes.fromhex(base_case["nonce"]), aad, changed_sealed) is not None:
            raise AssertionError(f"oracle accepts {name}")
        rejected.append({"name": name, "key": base_case["key"], "nonce": base_case["nonce"], "aad": aad.hex(),
                         "sealed": changed_sealed.hex()})

    ikm, salt, info, length, okm = RFC_HKDF
    hkdf_cases = [{"name": "rfc5869 case 1", "ikm": ikm, "salt": salt, "info": info, "length": length, "okm": okm}]
    for index, (salt_length, ikm_length, info_length, length) in enumerate([(0, 32, 0, 32), (32, 64, 9, 32), (32, 32, 12, 12),
                                                                            (5, 80, 70, 80), (64, 1, 1, 64)]):
        salt, ikm, info = derive(f"hkdf salt {index}", salt_length), derive(f"hkdf ikm {index}", ikm_length), derive(f"hkdf info {index}", info_length)
        hkdf_cases.append({"name": f"generated {index}", "ikm": ikm.hex(), "salt": salt.hex(), "info": info.hex(), "length": length,
                           "okm": hkdf_expand(hkdf_extract(salt, ikm), info, length).hex()})
    return {"x25519": x25519_cases, "poly1305": poly_cases, "aead": aead_cases, "aeadRejected": rejected, "hkdf": hkdf_cases}


def check(fixture: dict) -> list[str]:
    errors = []
    for case in fixture.get("x25519", []):
        if x25519(bytes.fromhex(case["scalar"]), bytes.fromhex(case["u"])).hex() != case["result"]:
            errors.append(f"x25519 {case['name']}: result differs from the oracle")
    for case in fixture.get("poly1305", []):
        if poly1305(bytes.fromhex(case["key"]), bytes.fromhex(case["message"])).hex() != case["tag"]:
            errors.append(f"poly1305 {case['name']}: tag differs from the oracle")
    for case in fixture.get("aead", []):
        key, nonce, aad = bytes.fromhex(case["key"]), bytes.fromhex(case["nonce"]), bytes.fromhex(case["aad"])
        if aead_seal(key, nonce, aad, bytes.fromhex(case["plaintext"])).hex() != case["sealed"]:
            errors.append(f"aead {case['name']}: sealed bytes differ from the oracle")
    for case in fixture.get("aeadRejected", []):
        if aead_open(bytes.fromhex(case["key"]), bytes.fromhex(case["nonce"]), bytes.fromhex(case["aad"]), bytes.fromhex(case["sealed"])) is not None:
            errors.append(f"aead rejected {case['name']}: the oracle accepts it")
    for case in fixture.get("hkdf", []):
        okm = hkdf_expand(hkdf_extract(bytes.fromhex(case["salt"]), bytes.fromhex(case["ikm"])), bytes.fromhex(case["info"]), case["length"])
        if okm.hex() != case["okm"]:
            errors.append(f"hkdf {case['name']}: output differs from the oracle")
    for section in ("x25519", "poly1305", "aead", "aeadRejected", "hkdf"):
        if not fixture.get(section):
            errors.append(f"section {section} is missing or empty")
    return errors


def self_test() -> int:
    fixture = build_fixture()
    if check(fixture) or check_published():
        print("self-test: the generated fixture or the published vectors fail", file=sys.stderr)
        return 1
    broken = []
    for section, field in (("x25519", "result"), ("poly1305", "tag"), ("aead", "sealed"), ("hkdf", "okm")):
        copy = json.loads(json.dumps(fixture))
        value = copy[section][-1][field]
        copy[section][-1][field] = value[:-2] + format(int(value[-2:], 16) ^ 1, "02x")
        broken.append((section, copy))
    copy = json.loads(json.dumps(fixture))
    copy["aeadRejected"][0]["sealed"] = fixture["aead"][3]["sealed"]
    broken.append(("aeadRejected", copy))
    copy = json.loads(json.dumps(fixture))
    del copy["hkdf"]
    broken.append(("missing section", copy))
    for name, copy in broken:
        if not check(copy):
            print(f"self-test: a broken {name} fixture passes", file=sys.stderr)
            return 1
    print("network crypto self-test passed")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="regenerate the fixture")
    parser.add_argument("--self-test", action="store_true", help="qualify the checks with broken fixtures")
    arguments = parser.parse_args()
    if arguments.self_test:
        return self_test()
    errors = check_published()
    expected = build_fixture()
    errors += check(expected)
    text = json.dumps(expected, indent=1) + "\n"
    if arguments.write and not errors:
        FIXTURE.parent.mkdir(parents=True, exist_ok=True)
        FIXTURE.write_text(text, encoding="utf-8")
    if not arguments.write and (not FIXTURE.exists() or FIXTURE.read_text(encoding="utf-8") != text):
        errors.append(f"{FIXTURE.relative_to(ROOT)} is not current; run with --write")
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    print(f"network crypto: {sum(len(v) for v in expected.values())} cases passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
