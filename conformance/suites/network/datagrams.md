---
formatVersion: 1
suiteId: network.datagrams
title: GES network datagrams
kind: networkApi
level: atomic
categories: [conformance]
tags: [portable-api, network]
---

# GES network datagrams

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the datagram transport of a GES network connection: the padded and repeated `hello`, address
validation, acknowledgement with loss detection and selective resends, confirmation frames of latest topics and the
simulated network with loss, duplicates and reordering ([GES network](../../../specs/Network.md#datagram-delivery)).
The network is the one of the [session suite](sessions.md).

Trace lines follow the session suite; a `packet` line longer than 512 bytes shows its length and FNV-1a 64 hash. A
`deliver` step passes packets of the other side's last `transmit` step by their index, so a case loses, duplicates and
reorders packets by the list it gives. A `cookie` step checks a datagram, given as bytes or as a packet of the client's
last `transmit` step, at a fresh address validation gate; its line names the result and, for a retry, its bytes.

---

## Test: A hello fills the minimum datagram and repeats with doubling interval

The client pads `hello` to 1200 bytes and repeats it after 1 s, then after 2 s more, while no `welcome` arrives.

### Case description

```yaml
gesBlock: case
id: datagram-hello-repeats
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {"transport": "datagram"}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"advance": 999999}
    - {"transmit": "client"}
    - {"advance": 1000000}
    - {"transmit": "client"}
    - {"advance": 2999999}
    - {"transmit": "client"}
    - {"advance": 3000000}
    - {"transmit": "client"}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:1e35caffd7b4efd5"
    - "client packet 1200:1e35caffd7b4efd5"
    - "client packet 1200:1e35caffd7b4efd5"
```

---

## Test: Address validation with a retry

A `hello` without a cookie gets a `retry`; the client sends `hello` again with its cookie at once. The cookie is valid in its window and the next one, only for its address, and the server connection then accepts the `hello`. A datagram that is not a full `hello` is dropped.

### Case description

```yaml
gesBlock: case
id: datagram-retry
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {"transport": "datagram"}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"cookie": {"key": "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", "windowMicroseconds": 10000000, "now": 0, "address": "7f0000013039", "packet": 0}}
    - {"receive": {"side": "client", "bytes": "02180000000000000000466cc01f6c2a8381ba5ad3c675cee84c00"}}
    - {"transmit": "client"}
    - {"cookie": {"key": "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", "windowMicroseconds": 10000000, "now": 15000000, "address": "7f0000013039", "packet": 0}}
    - {"cookie": {"key": "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", "windowMicroseconds": 10000000, "now": 15000000, "address": "7f000001303a", "packet": 0}}
    - {"cookie": {"key": "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", "windowMicroseconds": 10000000, "now": 25000000, "address": "7f0000013039", "packet": 0}}
    - {"cookie": {"key": "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", "windowMicroseconds": 10000000, "now": 0, "address": "7f0000013039", "bytes": "0100"}}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:1e35caffd7b4efd5"
    - "cookie retry 02180000000000000000466cc01f6c2a8381ba5ad3c675cee84c00"
    - "client packet 1200:4e7137565eda3c96"
    - "cookie accept"
    - "cookie retry 021801000000000000005a7642b7bbd6c48c1fe81613016538fa00"
    - "cookie retry 021802000000000000001668be81d3a701b8d6d098846ce38f8300"
    - "cookie drop"
    - "server connecting -"
    - "server connected"
    - "server packet 0301b0098080048080802001"
    - "client connected"
```

---

## Test: A lost packet is resent and the receiver restores the order

The client sends five commands in five packets; the second is lost and the fourth arrives twice. The server reports the first command, ignores the duplicate and holds the later ones. Its acknowledgement shows the gap, the client declares the packet lost three packets later and resends the command, and the server reports the rest in order.

### Case description

```yaml
gesBlock: case
id: datagram-selective-resend
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {"transport": "datagram", "ackDelayMicroseconds": 1}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "client", "route": 0, "payload": "00"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"send": {"side": "client", "route": 0, "payload": "01"}}
    - {"transmit": "client"}
    - {"send": {"side": "client", "route": 0, "payload": "02"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"send": {"side": "client", "route": 0, "payload": "03"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0, 0]}}
    - {"send": {"side": "client", "route": 0, "payload": "04"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"advance": 1}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:1e35caffd7b4efd5"
    - "server connecting -"
    - "server connected"
    - "server packet 0301b0098080048080802001"
    - "client connected"
    - "client send queued"
    - "client packet 0502000001000000010100"
    - "server message 0 00"
    - "client send queued"
    - "client packet 0502010001010000010101"
    - "client send queued"
    - "client packet 0502020001020000010102"
    - "client send queued"
    - "client packet 0502030001030000010103"
    - "client send queued"
    - "client packet 0502040001040000010104"
    - "server packet 0501000004000b000000"
    - "client packet 0503050000000000000001010000010101"
    - "server message 0 01"
    - "server message 0 02"
    - "server message 0 03"
    - "server message 0 04"
```

---

## Test: A probe after silence

When no acknowledgement arrives, the probe timeout sends a packet that asks for one: the estimate of 333 ms, plus four times the variation of 166.5 ms, plus the acknowledgement delay of 1 ms, so the first probe leaves at 1 s. The second expiry doubles the interval, so the next probe leaves 2 s later.

### Case description

```yaml
gesBlock: case
id: datagram-probe
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {"transport": "datagram", "ackDelayMicroseconds": 1000}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "client", "route": 0, "payload": "01"}}
    - {"transmit": "client"}
    - {"advance": 999000}
    - {"transmit": "client"}
    - {"advance": 1000000}
    - {"transmit": "client"}
    - {"advance": 2999999}
    - {"transmit": "client"}
    - {"advance": 3000000}
    - {"transmit": "client"}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:1e35caffd7b4efd5"
    - "server connecting -"
    - "server connected"
    - "server packet 0301b0098080048080802001"
    - "client connected"
    - "client send queued"
    - "client packet 0502000001000000010101"
    - "client packet 05020100"
    - "client packet 05020200"
```

---

## Test: A latest topic confirms its states

On datagrams the client confirms each state of a latest topic it receives with a confirmation frame, channel `00`. The confirmed state becomes the base of the next state, which travels as a delta of mode 1. The case uses a topic record of eight bytes, so that the delta is shorter than the full record.

### Case description

```yaml
gesBlock: case
id: datagram-confirmation
networkApi:
  sources: ["wire schema :World as { a: :Number integer between 0 and 255, b: :Number integer between 0 and 255, c: :Number integer between 0 and 255, d: :Number integer between 0 and 255, e: :Number integer between 0 and 255, f: :Number integer between 0 and 255, g: :Number integer between 0 and 255, h: :Number integer between 0 and 255 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {"transport": "datagram"}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"subscribe": {"side": "server", "route": 2}}
    - {"publish": {"side": "server", "route": 2, "payload": "0102030405060708"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"advance": 25000}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"advance": 50000}
    - {"publish": {"side": "server", "route": 2, "payload": "0102030405060709"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:2bcd393071b5ea6c"
    - "server connecting -"
    - "server connected"
    - "server packet 0301b0098080048080802001"
    - "client connected"
    - "server publish queued"
    - "server packet 05020000030000000909000102030405060708"
    - "client message 2 0102030405060708"
    - "client packet 050100000000000000000000030000"
    - "server publish queued"
    - "server packet 05030100000000000000030100000303018009"
    - "client message 2 0102030405060709"
```

---

## Test: A seeded link with loss, duplicates and reordering

Reliable and bulk messages arrive complete and in order and the latest topic ends with its newest state, although the simulated link loses, duplicates and reorders packets. Equal seeds give equal traces in every port.

### Case description

```yaml
gesBlock: case
id: datagram-simulated-loss
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {"transport": "datagram"}
  link: {"seed": 11, "latencyMicroseconds": 20000, "jitterMicroseconds": 15000, "lossPerMille": 250, "duplicatePerMille": 150, "reorder": true}
  steps:
    - {"advance": 0}
    - {"advance": 2000000}
    - {"accept": true}
    - {"advance": 4000000}
    - {"subscribe": {"side": "server", "route": 2}}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 1, "seed": 0}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 3, "seed": 9}}}
    - {"send": {"side": "client", "route": 0, "payload": "00"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0000"}}
    - {"advance": 4050000}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 401, "seed": 1}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 703, "seed": 10}}}
    - {"send": {"side": "client", "route": 0, "payload": "01"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0100"}}
    - {"advance": 4100000}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 801, "seed": 2}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 1403, "seed": 11}}}
    - {"send": {"side": "client", "route": 0, "payload": "02"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0200"}}
    - {"advance": 4150000}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 1201, "seed": 3}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 2103, "seed": 12}}}
    - {"send": {"side": "client", "route": 0, "payload": "03"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0300"}}
    - {"advance": 4200000}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 1601, "seed": 4}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 2803, "seed": 13}}}
    - {"send": {"side": "client", "route": 0, "payload": "04"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0400"}}
    - {"advance": 4250000}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 2001, "seed": 5}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 3503, "seed": 14}}}
    - {"send": {"side": "client", "route": 0, "payload": "05"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0500"}}
    - {"advance": 4300000}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 2401, "seed": 6}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 4203, "seed": 15}}}
    - {"send": {"side": "client", "route": 0, "payload": "06"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0600"}}
    - {"advance": 4350000}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 2801, "seed": 7}}}
    - {"send": {"side": "server", "route": 3, "payload": {"length": 4903, "seed": 16}}}
    - {"send": {"side": "client", "route": 0, "payload": "07"}}
    - {"publish": {"side": "server", "route": 2, "payload": "0700"}}
    - {"advance": 4400000}
    - {"advance": 8000000}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "server connecting -"
    - "server connected"
    - "client connected"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "client message 1 00"
    - "client message 2 0000"
    - "client progress 3 0 37/37"
    - "client message 3 090a0b"
    - "server message 0 00"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "client message 1 401:7a4418c6cf6cb989"
    - "client message 2 0100"
    - "client progress 3 1 306/306"
    - "client message 3 703:cbcfac019450cd23"
    - "server progress 3 0 37/37"
    - "server message 0 01"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "server progress 3 1 306/306"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "client message 2 0300"
    - "client progress 3 3 320/320"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "client message 2 0400"
    - "client progress 3 4 325/325"
    - "server progress 3 3 320/320"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "client message 2 0500"
    - "client progress 3 5 333/333"
    - "server progress 3 4 325/325"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "client progress 3 2 313/313"
    - "client message 3 1403:c52c27d7822c4954"
    - "client message 3 2103:7604fbd8cb7e6434"
    - "client message 3 2803:2e300258e0a876d7"
    - "client message 3 3503:4ad405c6941d5867"
    - "server progress 3 5 333/333"
    - "server message 0 02"
    - "server message 0 03"
    - "server message 0 04"
    - "server message 0 05"
    - "server send queued"
    - "server send queued"
    - "client send queued"
    - "server publish queued"
    - "client message 1 801:96c156b1a801c2e8"
    - "client message 1 1201:26237fb36eb28abe"
    - "client message 1 1601:b1deafbec2a9fdee"
    - "server progress 3 2 313/313"
    - "server message 0 06"
    - "server message 0 07"
    - "client progress 3 6 339/339"
    - "client message 3 4203:006b80e1ff3e4ca9"
    - "client message 1 2001:1c8862936be39e24"
    - "client message 1 2401:2345f41f0ac16ad9"
    - "client message 2 0700"
    - "client progress 3 7 346/346"
    - "client message 3 4903:cdb7a8bc65ac75a9"
    - "client message 1 2801:8742cb7ef59aceaa"
    - "server progress 3 6 339/339"
    - "server progress 3 7 346/346"
  client: {"state": "connected"}
  server: {"state": "connected"}
```

---

## Test: An empty datagram is malformed

A datagram without bytes closes the connection with `network.malformed`.

### Case description

```yaml
gesBlock: case
id: datagram-empty
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {"transport": "datagram"}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"receive": {"side": "server", "bytes": ""}}
    - {"transmit": "server"}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:1e35caffd7b4efd5"
    - "server connecting -"
    - "server connected"
    - "server packet 0301b0098080048080802001"
    - "client connected"
    - "server closed network.malformed local \"\""
    - "server packet 04116e6574776f726b2e6d616c666f726d656400"
```

---

## Test: Loss requires datagram connections

The simulated network loses packets only between datagram connections; otherwise its options are an invalid argument.

### Case description

```yaml
gesBlock: case
id: datagram-loss-needs-datagrams
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  options: {}
  link: {"seed": 1, "latencyMicroseconds": 0, "jitterMicroseconds": 0, "lossPerMille": 10}
  steps:
    - {"advance": 0}
```

### Expectation

```yaml
gesBlock: expect
network:
  error: {"code": "invalidArgument"}
```
