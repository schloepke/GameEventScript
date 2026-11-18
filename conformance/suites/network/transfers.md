---
formatVersion: 1
suiteId: network.transfers
title: GES network transfers
kind: networkApi
level: atomic
categories: [conformance]
tags: [portable-api, network]
---

# GES network transfers

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies [transfers](../../../specs/Network.md#transfers) on bulk routes: the header with compression, size
and SHA-256 hash, [Deflate](../../../specs/Network.md#deflate), the checks of the receiver, progress and
[cancel frames](../../../specs/Network.md#control-frames). Traces follow the [session suite](sessions.md); a transfer
adds `<side> progress <route> <id> <bytes>/<total>`, `<side> cancelled <route> <id> <code> peer|local` and
`<side> cancel true|false` for a cancel step.

---

## Test: A compressed transfer

Ten zero bytes compress to a shorter Deflate stream; the header carries compression `1`, the size `10` and the hash of the uncompressed bytes. The receiver reports progress and the message, and the sender reports progress when the acknowledgement arrives.

### Case description

```yaml
gesBlock: case
id: compressed-transfer
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "00000000000000000000"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"advance": 20000}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server packet 05020000040000002626010a01d448afd928065458cf670b60f5a594d735af0172c8d67f22a81680132681ca63800300"
    - "client progress 3 0 38/38"
    - "client message 3 00000000000000000000"
    - "client packet 05010000000000000000"
    - "server progress 3 0 38/38"
```

---

## Test: An uncompressed transfer

A receiver that does not accept Deflate gets the data with compression `0`; so does data that does not shrink.

### Case description

```yaml
gesBlock: case
id: uncompressed-transfer
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  clientOptions: {"deflateTransfers": false}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "00000000000000000000"}}
    - {"send": {"side": "server", "route": 3, "payload": "0102"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802000000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server send queued"
    - "server packet 05020000040000002c2c000a01d448afd928065458cf670b60f5a594d735af0172c8d67f22a81680132681ca000000000000000000000401000024240002a12871fee210fb8619291eaea194581cbd2531e4b23759d225f6806923f632220102"
    - "client progress 3 0 44/44"
    - "client message 3 00000000000000000000"
    - "client progress 3 1 36/36"
    - "client message 3 0102"
```

---

## Test: An empty transfer

Empty data travels uncompressed with size `0` and the hash of no bytes.

### Case description

```yaml
gesBlock: case
id: empty-transfer
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": ""}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server packet 050200000400000022220000e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
    - "client progress 3 0 34/34"
    - "client message 3 -"
```

---

## Test: Transfers around the Deflate window

Transfers of 32767, 32768 and 32769 bytes arrive intact over a loopback link.

### Case description

```yaml
gesBlock: case
id: window-transfers
networkApi:
  sources: ["wire schema :Pack as { data: :List(:Number integer between 0 and 255) max 200000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Files as {\n  client sends :Commands reliable\n  server sends :Pack bulk\n}\n"]
  network: "Files"
  token: ""
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 32767, "seed": 7}}}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 32768, "seed": 7}}}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 32769, "seed": 7}}}
    - {"advance": 1000}
    - {"advance": 50000}
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
    - "server send queued"
    - "client progress 1 0 604/604"
    - "client message 1 32767:fecc5234d7850e94"
    - "client progress 1 1 604/604"
    - "client message 1 32768:7a3db3ca3716d0cc"
    - "client progress 1 2 604/604"
    - "client message 1 32769:cda8dd9b9bc40e07"
    - "server progress 1 0 604/604"
    - "server progress 1 1 604/604"
    - "server progress 1 2 604/604"
```

---

## Test: A large transfer over a stream

A transfer larger than a packet travels in fragments and arrives intact; both sides report its progress.

### Case description

```yaml
gesBlock: case
id: large-transfer
networkApi:
  sources: ["wire schema :Pack as { data: :List(:Number integer between 0 and 255) max 200000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Files as {\n  client sends :Commands reliable\n  server sends :Pack bulk\n}\n"]
  network: "Files"
  token: ""
  options: {"transport": "stream"}
  clientOptions: {"transport": "stream", "deflateTransfers": false}
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"send": {"side": "server", "route": 1, "payload": {"length": 100000, "seed": 3}}}
    - {"advance": 1000}
    - {"advance": 50000}
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
    - "client progress 1 0 100036/100036"
    - "client message 1 100000:6fe3de7de1533be4"
    - "server progress 1 0 100036/100036"
```

---

## Test: The sender checks the smaller transfer limit

Data above the smaller transfer limit of both sides is `messageTooLarge`; queued data up to that limit is `queueFull` beyond it.

### Case description

```yaml
gesBlock: case
id: sender-limit
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  clientOptions: {"maxTransferBytes": 4}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "0102030405"}}
    - {"send": {"side": "server", "route": 3, "payload": "01020304"}}
    - {"send": {"side": "server", "route": 3, "payload": "01"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080040401000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send messageTooLarge"
    - "server send queued"
    - "server send queueFull"
```

---

## Test: A transfer above the receiver's limit

The server sends four bytes, which arrive altered. A size above the receiver's transfer limit is cancelled with `network.transferTooLarge` before any data is kept; the sender receives the cancel frame and the session continues with the next transfer.

### Case description

```yaml
gesBlock: case
id: receiver-too-large
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  clientOptions: {"maxTransferBytes": 4}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "01020304"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0502000004000000262600059f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a01020304"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "05"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080040401000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server packet 0502000004000000262600049f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a01020304"
    - "client progress 3 0 38/38"
    - "client cancelled 3 0 network.transferTooLarge local"
    - "client packet 050300000000000000000001040000186e6574776f726b2e7472616e73666572546f6f4c61726765"
    - "server progress 3 0 38/38"
    - "server cancelled 3 0 network.transferTooLarge peer"
    - "server send queued"
    - "server packet 050301000000000000000401000023230001e77b9a9ae9e30b0dbdb6f510a264ef9de781501d7b6b92ae89eb059c5ab743db05"
    - "client progress 3 1 35/35"
    - "client message 3 05"
```

---

## Test: A transfer with a wrong hash

Altered data whose SHA-256 hash differs from the header is cancelled with `network.transferCorrupt` and not delivered.

### Case description

```yaml
gesBlock: case
id: corrupt-hash
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  clientOptions: {"maxTransferBytes": 4}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "01020304"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0502000004000000262600049f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a01020305"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "05"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080040401000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server packet 0502000004000000262600049f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a01020304"
    - "client progress 3 0 38/38"
    - "client cancelled 3 0 network.transferCorrupt local"
    - "client packet 050300000000000000000001040000176e6574776f726b2e7472616e73666572436f7272757074"
    - "server progress 3 0 38/38"
    - "server cancelled 3 0 network.transferCorrupt peer"
    - "server send queued"
    - "server packet 050301000000000000000401000023230001e77b9a9ae9e30b0dbdb6f510a264ef9de781501d7b6b92ae89eb059c5ab743db05"
    - "client progress 3 1 35/35"
    - "client message 3 05"
```

---

## Test: A transfer with an unknown compression

An altered header with a compression other than `0` and `1` is cancelled with `network.transferCorrupt`.

### Case description

```yaml
gesBlock: case
id: unknown-compression
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  clientOptions: {"maxTransferBytes": 4}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "01020304"}}
    - {"transmit": "server"}
    - {"receive": {"side": "client", "bytes": "0502000004000000262602049f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a01020304"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "05"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080040401000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server packet 0502000004000000262600049f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a01020304"
    - "client progress 3 0 38/38"
    - "client cancelled 3 0 network.transferCorrupt local"
    - "client packet 050300000000000000000001040000176e6574776f726b2e7472616e73666572436f7272757074"
    - "server progress 3 0 38/38"
    - "server cancelled 3 0 network.transferCorrupt peer"
    - "server send queued"
    - "server packet 050301000000000000000401000023230001e77b9a9ae9e30b0dbdb6f510a264ef9de781501d7b6b92ae89eb059c5ab743db05"
    - "client progress 3 1 35/35"
    - "client message 3 05"
```

---

## Test: Cancelling an unsent transfer

A transfer cancelled before it is sent never travels, but the peer still receives a cancel frame; the session continues with the next transfer.

### Case description

```yaml
gesBlock: case
id: cancel-unsent
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "0102"}}
    - {"send": {"side": "server", "route": 3, "payload": "0304"}}
    - {"cancel": {"side": "server", "route": 3, "id": 0}}
    - {"cancel": {"side": "server", "route": 3, "id": 0}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server send queued"
    - "server cancel true"
    - "server cancelled 3 0 network.transferCancelled local"
    - "server cancel false"
    - "server packet 050200000001040000196e6574776f726b2e7472616e7366657243616e63656c6c656404010000242400020ce3940bebf2b22a5d2108ecf0c368a0541c7e3c45703f8540921b4eafc829470304"
    - "client cancelled 3 0 network.transferCancelled peer"
    - "client progress 3 1 36/36"
    - "client message 3 0304"
```

---

## Test: Cancelling with a host code

The host may name its own code, which the peer receives.

### Case description

```yaml
gesBlock: case
id: cancel-code
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "server", "route": 3, "payload": "0102"}}
    - {"cancel": {"side": "server", "route": 3, "id": 0, "code": "game.stale"}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server send queued"
    - "server cancel true"
    - "server cancelled 3 0 game.stale local"
    - "server packet 0502000000010400000a67616d652e7374616c65"
    - "client cancelled 3 0 game.stale peer"
```

---

## Test: Cancel arguments

Only a bulk route this side sends can be cancelled, with a valid code; an unknown id or a sent transfer is `false`.

### Case description

```yaml
gesBlock: case
id: cancel-arguments
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"cancel": {"side": "server", "route": 1, "id": 0}}
    - {"cancel": {"side": "client", "route": 3, "id": 0}}
    - {"cancel": {"side": "server", "route": 3, "id": 0, "code": "a b"}}
    - {"cancel": {"side": "server", "route": 3, "id": 7}}
    - {"send": {"side": "server", "route": 3, "payload": "01"}}
    - {"transmit": "server"}
    - {"cancel": {"side": "server", "route": 3, "id": 0}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000000"
    - "server connecting -"
    - "server connected"
    - "server packet 03018080018080048080802001"
    - "client connected"
    - "server error invalidArgument"
    - "client error invalidArgument"
    - "server error invalidArgument"
    - "server cancel false"
    - "server send queued"
    - "server packet 0502000004000000232300014bf5122f344554c53bde2ebb8cd2b7e3d1600ad631c385a5d7cce23c7785459a01"
    - "server cancel false"
```

---

## Test: Cancelling over datagrams

Over datagrams the cancel frame reaches the receiver, which skips the cancelled transfer and delivers the next.

### Case description

```yaml
gesBlock: case
id: cancel-datagram
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: ""
  options: {"transport": "datagram"}
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"send": {"side": "server", "route": 3, "payload": "0102"}}
    - {"send": {"side": "server", "route": 3, "payload": "0304"}}
    - {"cancel": {"side": "server", "route": 3, "id": 0}}
    - {"advance": 1000}
    - {"advance": 50000}
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
    - "server cancel true"
    - "server cancelled 3 0 network.transferCancelled local"
    - "client cancelled 3 0 network.transferCancelled peer"
    - "client progress 3 1 36/36"
    - "client message 3 0304"
    - "server progress 3 1 36/36"
```
