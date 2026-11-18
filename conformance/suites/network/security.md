---
formatVersion: 1
suiteId: network.security
title: GES network security
kind: networkApi
level: atomic
categories: [conformance]
tags: [portable-api, network]
---

# GES network security

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the encryption of a GES network connection: sealed `hello`, `welcome` and data packets, the key
in a `retry`, the security levels and their failures ([GES network](../../../specs/Network.md#encryption)). The network
is the one of the [session suite](sessions.md); the server key is the private key 01..20, and every encrypting side
uses the test seed 7, so the sealed packets are deterministic. Trace lines follow the
[session suite](sessions.md) and the [datagram suite](datagrams.md).

---

## Test: A pinned key gives a verified session whose packets are sealed

The client seals `hello` to the pinned server key; the server answers with a sealed `welcome`, and every later packet is a sealed packet of type 6. The client reports `verified`, the server `encrypted`.

### Case description

```yaml
gesBlock: case
id: verified-handshake
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"minimumSecurity": "verified", "serverPublicKey": "07a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "client", "route": 0, "payload": "05"}}
    - {"send": {"side": "server", "route": 1, "payload": "00"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000240d87ce093ab5d739dc153383b6bce22d93a9d476802bd599efa5864d22128191166a9b7f12a3430311523a8471a89b1938e"
    - "server connecting 74"
    - "server connected"
    - "server packet 0313161fee9cd11698667241f20bdb4fd76e462ce37311e308d7b28358b260276294fc98f38db465f9da88a4291c5ac11bc8c1f9602665615fdc75c5e1"
    - "client connected"
    - "client send queued"
    - "server send queued"
    - "client packet 0600005a6536be42426bbb5770f73863d526a94e1df8d696eaeccec5"
    - "server message 0 05"
    - "server packet 060000bc49e38cd80377d28d2be50a015fad57c2adf75d327b337573c335d5e566c1"
    - "client message 1 00"
  client: {"state": "connected", "security": "verified"}
  server: {"state": "connected", "security": "encrypted"}
```
---

## Test: A sealed session over a stream transport

Sealed packets carry the stream length prefix like any other packet; a stream needs no sequence number for decryption.

### Case description

```yaml
gesBlock: case
id: verified-stream
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"transport": "stream", "minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"transport": "stream", "minimumSecurity": "verified", "serverPublicKey": "07a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "client", "route": 0, "payload": "05"}}
    - {"send": {"side": "server", "route": 1, "payload": "00"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 6e014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000240d87ce093ab5d739dc153383b6bce22d93a9d476802bd599efa5864d22128191166a9b7f12a3430311523a8471a89b1938e"
    - "server connecting 74"
    - "server connected"
    - "server packet 3d0313161fee9cd11698667241f20bdb4fd76e462ce37311e308d7b28358b260276294fc98f38db465f9da88a4291c5ac11bc8c1f9602665615fdc75c5e1"
    - "client connected"
    - "client send queued"
    - "server send queued"
    - "client packet 1c0600005a6536be42426bbb5770f73863d526a94e1df8d696eaeccec5"
    - "server message 0 05"
    - "server packet 22060000bc49e38cd80377d28d2be50a015fad57c2adf75d327b337573c335d5e566c1"
    - "client message 1 00"
  client: {"state": "connected", "security": "verified"}
  server: {"state": "connected", "security": "encrypted"}
```
---

## Test: A sealed session over the datagram transport

The sealed `hello` is padded to 1200 bytes; a datagram receiver reconstructs the packet number from the 16-bit sequence.

### Case description

```yaml
gesBlock: case
id: verified-datagram
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"transport": "datagram", "minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"transport": "datagram", "minimumSecurity": "verified", "serverPublicKey": "07a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"send": {"side": "client", "route": 0, "payload": "05"}}
    - {"send": {"side": "server", "route": 1, "payload": "00"}}
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:abce6247562c84f5"
    - "server connecting 74"
    - "server connected"
    - "server packet 0313161fee9cd11698667241f20bdb4fd76e462ce37311e308d7b28358b2602762c33b68c9789039f142a91532bbacf61d26e89929702052238b51d4"
    - "client connected"
    - "client send queued"
    - "server send queued"
    - "client packet 0600006d74ecaa3b2297798e426a09fa7a609702a94b2dad083cfb43"
    - "server message 0 05"
    - "server packet 06000020c6ac55abff259b1aa840d174434fd13050b4f8703ec2ee99eb66a38f0db6"
    - "client message 1 00"
  client: {"state": "connected", "security": "verified"}
  server: {"state": "connected", "security": "encrypted"}
```
---

## Test: Without a pinned key the server key arrives in a retry

A client without a pinned key asks for the key; the server answers with a `retry` that carries its public key, and the client seals its next `hello` to it. The session is `encrypted`, not `verified`.

### Case description

```yaml
gesBlock: case
id: key-retry
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"minimumSecurity": "encrypted", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"transmit": "client"}
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
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000100"
    - "server packet 02002007a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c"
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000240d87ce093ab5d739dc153383b6bce22d93a9d476802bd599efa5864d22128191166a9b7f12a3430311523a8471a89b1938e"
    - "server connecting 74"
    - "server connected"
    - "server packet 0313161fee9cd11698667241f20bdb4fd76e462ce37311e308d7b28358b260276294fc98f38db465f9da88a4291c5ac11bc8c1f9602665615fdc75c5e1"
    - "client connected"
  client: {"state": "connected", "security": "encrypted"}
  server: {"state": "connected", "security": "encrypted"}
```
---

## Test: A server with a key encrypts a plain client

A client that accepts any level sends a plain `hello`; a server with a key answers with its key and the session is `encrypted`.

### Case description

```yaml
gesBlock: case
id: plain-client-encrypted
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"minimumSecurity": "none", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"transmit": "client"}
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
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb5808001808004808080200100000174"
    - "server packet 02002007a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c"
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000240d87ce093ab5d739dc153383b6bce22d93a9d476802bd599efa5864d22128191166a9b7f12a3430311523a8471a89b1938e"
    - "server connecting 74"
    - "server connected"
    - "server packet 0313161fee9cd11698667241f20bdb4fd76e462ce37311e308d7b28358b260276294fc98f38db465f9da88a4291c5ac11bc8c1f9602665615fdc75c5e1"
    - "client connected"
  client: {"state": "connected", "security": "encrypted"}
  server: {"state": "connected", "security": "encrypted"}
```
---

## Test: Without a key the session stays plain

Neither side has a key and both accept the level none; both report `none`.

### Case description

```yaml
gesBlock: case
id: plain-session
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {}
  clientOptions: {}
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "server connecting 74"
    - "server connected"
    - "client connected"
  client: {"state": "connected", "security": "none"}
  server: {"state": "connected", "security": "none"}
```
---

## Test: A client that requires verification closes without a pinned key

The client's lowest level is `verified` but it has no pinned key, so it closes with `network.unverified` before sending anything.

### Case description

```yaml
gesBlock: case
id: unverified
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"minimumSecurity": "verified"}
  steps:
    - {"advance": 0}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client closed network.unverified local \"\""
    - "server closed network.unverified peer \"\""
  client: {"state": "closed"}
  server: {"state": "closed"}
```
---

## Test: A server without a key closes a key request

The client asks for the key of a server that has none: the server closes with `network.insecure`.

### Case description

```yaml
gesBlock: case
id: insecure
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {}
  clientOptions: {"minimumSecurity": "encrypted", "testKeySeed": 7}
  steps:
    - {"advance": 0}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client closed network.insecure peer \"\""
    - "server closed network.insecure local \"\""
  client: {"state": "closed"}
  server: {"state": "closed"}
```
---

## Test: A plain welcome to a sealed hello fails authentication

A client that sealed its `hello` opens every `welcome` as a sealed one; a plain `welcome` does not authenticate and closes the session with `network.authenticationFailed`.

### Case description

```yaml
gesBlock: case
id: plain-welcome
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {}
  clientOptions: {"minimumSecurity": "encrypted", "serverPublicKey": "07a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"receive": {"side": "client", "bytes": "03018080018080048080802001"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000240d87ce093ab5d739dc153383b6bce22d93a9d476802bd599efa5864d22128191166a9b7f12a3430311523a8471a89b1938e"
    - "client closed network.authenticationFailed local \"\""
  client: {"state": "closed"}
  server: {"state": "awaitHello"}
```
---

## Test: A hello sealed to another key fails authentication

The server cannot open a `hello` sealed to a key it does not hold and closes with `network.authenticationFailed`.

### Case description

```yaml
gesBlock: case
id: wrong-key
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"minimumSecurity": "verified", "serverPublicKey": "2fe57da347cd62431528daac5fbb290730fff684afc4cfc2ed90995f58cb3b74", "testKeySeed": 7}
  steps:
    - {"advance": 0}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client closed network.authenticationFailed peer \"\""
    - "server closed network.authenticationFailed local \"\""
  client: {"state": "closed"}
  server: {"state": "closed"}
```
---

## Test: A changed sealed packet closes a session

On the message and stream transports a sealed packet that does not authenticate closes the session with `network.authenticationFailed`.

### Case description

```yaml
gesBlock: case
id: changed-packet
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"minimumSecurity": "verified", "serverPublicKey": "07a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"receive": {"side": "server", "bytes": "060000000000000000000000000000000000"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 014745534e0166c53b960403735304a1a6879dd34d23b7d28acffa5b3c6dc56f93184d6c347b7742157d68bb79cfb58080018080048080802001000240d87ce093ab5d739dc153383b6bce22d93a9d476802bd599efa5864d22128191166a9b7f12a3430311523a8471a89b1938e"
    - "server connecting 74"
    - "server connected"
    - "server packet 0313161fee9cd11698667241f20bdb4fd76e462ce37311e308d7b28358b260276294fc98f38db465f9da88a4291c5ac11bc8c1f9602665615fdc75c5e1"
    - "client connected"
    - "server closed network.authenticationFailed local \"\""
  client: {"state": "connected"}
  server: {"state": "closed"}
```
---

## Test: A changed sealed datagram is dropped

On the datagram transport a sealed packet that does not authenticate is dropped, and so is a plain data packet on an encrypted session.

### Case description

```yaml
gesBlock: case
id: changed-datagram
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"transport": "datagram", "minimumSecurity": "encrypted", "key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20", "testKeySeed": 7}
  clientOptions: {"transport": "datagram", "minimumSecurity": "verified", "serverPublicKey": "07a37cbc142093c8b755dc1b10e86cb426374ad16aa853ed0bdfc0b2b86d1c7c", "testKeySeed": 7}
  link: "none"
  steps:
    - {"transmit": "client"}
    - {"deliver": {"side": "server", "packets": [0]}}
    - {"accept": true}
    - {"transmit": "server"}
    - {"deliver": {"side": "client", "packets": [0]}}
    - {"receive": {"side": "server", "bytes": "060000000000000000000000000000000000"}}
    - {"receive": {"side": "server", "bytes": "05020900"}}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "client packet 1200:abce6247562c84f5"
    - "server connecting 74"
    - "server connected"
    - "server packet 0313161fee9cd11698667241f20bdb4fd76e462ce37311e308d7b28358b2602762c33b68c9789039f142a91532bbacf61d26e89929702052238b51d4"
    - "client connected"
  client: {"state": "connected"}
  server: {"state": "connected"}
```
---

## Test: A server above the level none needs a key

Creating a server connection whose lowest level is above none without a key is an invalid argument.

### Case description

```yaml
gesBlock: case
id: key-required
networkApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nwire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire schema :Pack as { data: :List(:Number integer between 0 and 255) max 6000 }\nwire messages :Commands as (Move)\nmessage Move(x: :Number integer between 0 and 255)\n\nnetwork :Game as {\n  client sends :Commands reliable\n  server sends :Blob reliable\n  server publishes :World latest at most every 50ms\n  server sends :Pack bulk\n}\n"]
  network: "Game"
  token: "74"
  options: {"minimumSecurity": "encrypted"}
  clientOptions: {}
  steps:
    - {"advance": 0}
```

### Expectation

```yaml
gesBlock: expect
network:
  error: {"code": "invalidArgument"}
```
