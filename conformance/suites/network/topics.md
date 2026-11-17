---
formatVersion: 1
suiteId: network.topics
title: GES network topics
kind: networkApi
level: atomic
categories: [conformance]
tags: [portable-api, network]
---

# GES network topics

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the topics of a GES network connection: subscriptions, numbering, rings, deltas against confirmed
states and the topic message ([GES network](../../../specs/Network.md#topics)). The network publishes `:State`
(four 16-bit fields, 8 bytes) reliably on route 0, `:Pose` (4 bytes) as latest on route 1, the keyed `:Unit`
(3 bytes) reliably on route 2, and the client publishes `:Input` (1 byte) as latest on route 3.

Trace lines follow the [session suite](sessions.md). Cases without a link drive both sides packet by packet, so the
trace shows each topic message on the wire: after the frame header comes the mode byte, `00` for a full record or the
distance to the base for a delta.

---

## Test: A confirmed state becomes the base of a delta

The first state travels in full, mode `00`. Once its packet is acknowledged, the next state is a delta against it, mode `01`. The state after that is still a delta against the first one, mode `02`, because the second one is not confirmed yet.

### Case description

```yaml
gesBlock: case
id: full-then-delta
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"publish": {"side": "server", "route": 0, "payload": "0100020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05020000010000000909000100020003000400"}}
    - {"advance": 1000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010000000000000000"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0900020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0503010000000000000001010000040401910000"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0900020003000500"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05030200000000000000010200000606029900500000"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server publish queued"
    - "server packet 05020000010000000909000100020003000400"
    - "client message 0 0100020003000400"
    - "client packet 05010000000000000000"
    - "server publish queued"
    - "server packet 0503010000000000000001010000040401910000"
    - "client message 0 0900020003000400"
    - "server publish queued"
    - "server packet 05030200000000000000010200000606029900500000"
    - "client message 0 0900020003000500"
```

---

## Test: The ring holds 32 states

After the confirmed first state, 31 more states are deltas against it. The 33rd state uses it at the largest distance, mode `20`; then the ring holds 32 states and the first one leaves, so the next state travels in full.

### Case description

```yaml
gesBlock: case
id: ring-count
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"publish": {"side": "server", "route": 0, "payload": "0000020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05020000010000000909000000020003000400"}}
    - {"advance": 1000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010000000000000000"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0100020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0200020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0300020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0400020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0500020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0600020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0700020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0800020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0900020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0a00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0b00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0c00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0d00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0e00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0f00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1000020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1100020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1200020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1300020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1400020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1500020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1600020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1700020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1800020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1900020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1a00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1b00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1c00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1d00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1e00020003000400"}}
    - {"publish": {"side": "server", "route": 0, "payload": "1f00020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05030100000000000000010100000404011100000102000004040221000001030000040403310000010400000404044100000105000004040551000001060000040406610000010700000404077100000108000004040881000001090000040409910000010a000004040aa10000010b000004040bb10000010c000004040cc10000010d000004040dd10000010e000004040ee10000010f000004040ff1000001100000040410010100011100000404111101000112000004041221010001130000040413310100011400000404144101000115000004041551010001160000040416610100011700000404177101000118000004041881010001190000040419910100011a000004041aa10100011b000004041bb10100011c000004041cc10100011d000004041dd10100011e000004041ee10100011f000004041ff10100"}}
    - {"publish": {"side": "server", "route": 0, "payload": "2000020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0503020000000000000001200000040420010200"}}
    - {"publish": {"side": "server", "route": 0, "payload": "2100020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05030300000000000000012100000909002100020003000400"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server publish queued"
    - "server packet 05020000010000000909000000020003000400"
    - "client message 0 0000020003000400"
    - "client packet 05010000000000000000"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server publish queued"
    - "server packet 05030100000000000000010100000404011100000102000004040221000001030000040403310000010400000404044100000105000004040551000001060000040406610000010700000404077100000108000004040881000001090000040409910000010a000004040aa10000010b000004040bb10000010c000004040cc10000010d000004040dd10000010e000004040ee10000010f000004040ff1000001100000040410010100011100000404111101000112000004041221010001130000040413310100011400000404144101000115000004041551010001160000040416610100011700000404177101000118000004041881010001190000040419910100011a000004041aa10100011b000004041bb10100011c000004041cc10100011d000004041dd10100011e000004041ee10100011f000004041ff10100"
    - "client message 0 0100020003000400"
    - "client message 0 0200020003000400"
    - "client message 0 0300020003000400"
    - "client message 0 0400020003000400"
    - "client message 0 0500020003000400"
    - "client message 0 0600020003000400"
    - "client message 0 0700020003000400"
    - "client message 0 0800020003000400"
    - "client message 0 0900020003000400"
    - "client message 0 0a00020003000400"
    - "client message 0 0b00020003000400"
    - "client message 0 0c00020003000400"
    - "client message 0 0d00020003000400"
    - "client message 0 0e00020003000400"
    - "client message 0 0f00020003000400"
    - "client message 0 1000020003000400"
    - "client message 0 1100020003000400"
    - "client message 0 1200020003000400"
    - "client message 0 1300020003000400"
    - "client message 0 1400020003000400"
    - "client message 0 1500020003000400"
    - "client message 0 1600020003000400"
    - "client message 0 1700020003000400"
    - "client message 0 1800020003000400"
    - "client message 0 1900020003000400"
    - "client message 0 1a00020003000400"
    - "client message 0 1b00020003000400"
    - "client message 0 1c00020003000400"
    - "client message 0 1d00020003000400"
    - "client message 0 1e00020003000400"
    - "client message 0 1f00020003000400"
    - "server publish queued"
    - "server packet 0503020000000000000001200000040420010200"
    - "client message 0 2000020003000400"
    - "server publish queued"
    - "server packet 05030300000000000000012100000909002100020003000400"
    - "client message 0 2100020003000400"
```

---

## Test: The baseline limit bounds the ring's bytes

With a baseline limit of 8 bytes the ring holds one 8-byte state. The second state is a delta against the confirmed first one; when it enters the ring, the first one leaves. The third state has no confirmed base and travels in full.

### Case description

```yaml
gesBlock: case
id: ring-bytes
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1, "maxBaselineBytes": 8}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c18080010880808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0301808001088080802001"}}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"publish": {"side": "server", "route": 0, "payload": "0100020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05020000010000000909000100020003000400"}}
    - {"advance": 1000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010000000000000000"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0900020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0503010000000000000001010000040401910000"}}
    - {"publish": {"side": "server", "route": 0, "payload": "0900020003000500"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05030200000000000000010200000909000900020003000500"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c18080010880808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 0301808001088080802001"
    - "client connected"
    - "server publish queued"
    - "server packet 05020000010000000909000100020003000400"
    - "client message 0 0100020003000400"
    - "client packet 05010000000000000000"
    - "server publish queued"
    - "server packet 0503010000000000000001010000040401910000"
    - "client message 0 0900020003000400"
    - "server publish queued"
    - "server packet 05030200000000000000010200000909000900020003000500"
    - "client message 0 0900020003000500"
```

---

## Test: A delta that is not shorter travels in full

When both coordinates change, the delta is not shorter than the 4-byte record and the state travels in full. When only one changes, it travels as a delta.

### Case description

```yaml
gesBlock: case
id: delta-not-shorter
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"subscribe": {"side": "server", "route": 1}}
    - {"publish": {"side": "server", "route": 1, "payload": "01000200"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "050200000200000005050001000200"}}
    - {"advance": 1000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010000000000000000"}}
    - {"publish": {"side": "server", "route": 1, "payload": "409c50c3"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0503010000000000000002010000050500409c50c3"}}
    - {"advance": 2000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010100010001000000"}}
    - {"publish": {"side": "server", "route": 1, "payload": "419c50c3"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0503020001000100000002020000040401057102"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server publish queued"
    - "server packet 050200000200000005050001000200"
    - "client message 1 01000200"
    - "client packet 05010000000000000000"
    - "server publish queued"
    - "server packet 0503010000000000000002010000050500409c50c3"
    - "client message 1 409c50c3"
    - "client packet 05010100010001000000"
    - "server publish queued"
    - "server packet 0503020001000100000002020000040401057102"
    - "client message 1 419c50c3"
```

---

## Test: A changed key sends the full record

A delta needs equal keys. The second unit has another `id`, so it travels in full although the first one is confirmed.

### Case description

```yaml
gesBlock: case
id: key-change
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"subscribe": {"side": "server", "route": 2}}
    - {"publish": {"side": "server", "route": 2, "payload": "010500"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0502000003000000040400010500"}}
    - {"advance": 1000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010000000000000000"}}
    - {"publish": {"side": "server", "route": 2, "payload": "020500"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0503010000000000000003010000040400020500"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server publish queued"
    - "server packet 0502000003000000040400010500"
    - "client message 2 010500"
    - "client packet 05010000000000000000"
    - "server publish queued"
    - "server packet 0503010000000000000003010000040400020500"
    - "client message 2 020500"
```

---

## Test: Unsubscribing empties the sender's ring

After unsubscribing and subscribing again, the confirmed first state is no base any more and the same state travels in full again.

### Case description

```yaml
gesBlock: case
id: unsubscribe-clears-ring
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"publish": {"side": "server", "route": 0, "payload": "0100020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05020000010000000909000100020003000400"}}
    - {"advance": 1000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010000000000000000"}}
    - {"unsubscribe": {"side": "server", "route": 0}}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"publish": {"side": "server", "route": 0, "payload": "0100020003000400"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "05030100000000000000010100000909000100020003000400"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server publish queued"
    - "server packet 05020000010000000909000100020003000400"
    - "client message 0 0100020003000400"
    - "client packet 05010000000000000000"
    - "server publish queued"
    - "server packet 05030100000000000000010100000909000100020003000400"
    - "client message 0 0100020003000400"
```

---

## Test: Unsubscribing drops the waiting latest state

The waiting pose is dropped, so the server sends nothing, and publishing reports `notSubscribed`. After subscribing again, a pose replaced before it started gets no number: the next pose is a delta at distance 1.

### Case description

```yaml
gesBlock: case
id: unsubscribe-latest
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"subscribe": {"side": "server", "route": 1}}
    - {"publish": {"side": "server", "route": 1, "payload": "01000200"}}
    - {"unsubscribe": {"side": "server", "route": 1}}
    - {"transmit": "server"}
    - {"publish": {"side": "server", "route": 1, "payload": "03000400"}}
    - {"subscribe": {"side": "server", "route": 1}}
    - {"publish": {"side": "server", "route": 1, "payload": "05000600"}}
    - {"publish": {"side": "server", "route": 1, "payload": "07000800"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "050200000200000005050007000800"}}
    - {"advance": 1000}
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "05010000000000000000"}}
    - {"publish": {"side": "server", "route": 1, "payload": "07000900"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0503010000000000000002010000040401260000"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server publish queued"
    - "server publish notSubscribed"
    - "server publish queued"
    - "server publish queued"
    - "server packet 050200000200000005050007000800"
    - "client message 1 07000800"
    - "client packet 05010000000000000000"
    - "server publish queued"
    - "server packet 0503010000000000000002010000040401260000"
    - "client message 1 07000900"
```

---

## Test: Subscription usage errors

A client cannot subscribe, and route 3 is a topic the client publishes. Subscribing twice and unsubscribing an unsubscribed topic do nothing.

### Case description

```yaml
gesBlock: case
id: subscribe-usage
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"subscribe": {"side": "client", "route": 0}}
    - {"subscribe": {"side": "server", "route": 3}}
    - {"unsubscribe": {"side": "client", "route": 0}}
    - {"unsubscribe": {"side": "server", "route": 3}}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"unsubscribe": {"side": "server", "route": 1}}
    - {"publish": {"side": "server", "route": 0, "payload": "0100020003000400"}}
    - {"advance": 0}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "server connecting -"
    - "server connected"
    - "client connected"
    - "client error invalidOperation"
    - "server error invalidArgument"
    - "client error invalidOperation"
    - "server error invalidArgument"
    - "server publish queued"
    - "client message 0 0100020003000400"
```

---

## Test: A client's topic is always subscribed

The client publishes its input without a subscription; the server cannot publish on that route, and the client cannot publish on the server's topics.

### Case description

```yaml
gesBlock: case
id: client-topic
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"publish": {"side": "client", "route": 3, "payload": "07"}}
    - {"publish": {"side": "server", "route": 3, "payload": "07"}}
    - {"publish": {"side": "client", "route": 0, "payload": "0100020003000400"}}
    - {"advance": 0}
    - {"advance": 100000}
    - {"publish": {"side": "client", "route": 3, "payload": "08"}}
    - {"advance": 200000}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "server connecting -"
    - "server connected"
    - "client connected"
    - "client publish queued"
    - "server publish invalidRoute"
    - "client publish invalidRoute"
    - "server message 3 07"
    - "client publish queued"
    - "server message 3 08"
```

---

## Test: An empty topic message

A topic message needs at least its mode byte.

### Case description

```yaml
gesBlock: case
id: malformed-empty
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"receive": {"side": "client", "bytes": "05020000010000000000"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "client closed network.malformed local \"\""
```

---

## Test: A mode above 32

Mode `21` names a distance beyond any ring.

### Case description

```yaml
gesBlock: case
id: malformed-mode
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"receive": {"side": "client", "bytes": "05020000010000000909210000000000000000"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "client closed network.malformed local \"\""
```

---

## Test: A delta without its base

The ring is empty, so mode `01` has no base.

### Case description

```yaml
gesBlock: case
id: malformed-base
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"receive": {"side": "client", "bytes": "050200000100000002020100"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "client closed network.malformed local \"\""
```

---

## Test: Bytes after a delta

The delta decodes in fewer bytes than the message holds.

### Case description

```yaml
gesBlock: case
id: malformed-rest
networkApi:
  sources: ["wire schema :State as {\n  a: :Number integer between 0 and 65535\n  b: :Number integer between 0 and 65535\n  c: :Number integer between 0 and 65535\n  d: :Number integer between 0 and 65535\n}\nwire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Unit as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 65535\n}\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :State reliable\n  server publishes :Pose latest\n  server publishes :Unit reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  options: {"ackDelayMicroseconds": 1}
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "server", "bytes": "014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
    - {"receive": {"side": "client", "bytes": "05020000010000000909000100020003000400"}}
    - {"receive": {"side": "client", "bytes": "050201000101000005050191000000"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e01c3a50292f985d5f604f8b212b1b59d58a3229c7718ecbbebba0067584bd2c51ab8721a3a2d1b7a15c180800180800480808020010000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "client message 0 0100020003000400"
    - "client closed network.malformed local \"\""
```
