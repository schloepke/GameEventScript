<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Connect a client and a server

> **Since: Unreleased**

A `network` declaration in a [schema](Schemas.md) names which records a client and a server
exchange and how each one is delivered. A session carries those encoded records between the two
sides over a TCP stream, a WebSocket or an in-process transport. The session core is a
synchronous state machine: you pass it received bytes and the current time, and it hands you
events and the bytes to send. It never opens a socket, starts a thread or reads a clock.

This guide declares a network, connects two sides in one process and then over real sockets.
[GES network](../../specs/Network.md) defines the exact rules.

## 1. Declare a network

Add a network to a schema source, for example `game.gess`:

```gess
wire schema :World as { tick: :Number integer between 0 and 65535 }
wire messages :GameCommands as (Move, Fire)

network :Game as {
  client sends :GameCommands reliable
  server publishes :World latest at most every 50ms
}
```

```bash
ges schema check game.gess
```

Each line is a route. `client` routes go from the client to the server, `server` routes the other
way. The class says how a route is delivered:

| Class | Delivery |
| --- | --- |
| `reliable` | every message, complete and in order |
| `latest` | only the newest message; an unsent older one is replaced |
| `bulk` | every message, complete and in order, after all other traffic |

`at most every` documents how often a topic should be updated; generated code exposes it as a
constant (see below). The position of a route, counting from 0, is its route index.

## 2. Bind it on both sides

Both sides bind the same declaration with the same parameter values. The handshake compares their
hashes and closes the session with `network.schemaMismatch` on any difference, so an outdated
client cannot misread the server's records.

```csharp
using GameEventScript.Network;

var network = GesSchemaSet.Parse(File.ReadAllText("game.gess")).Network("Game").Bind();
```

```swift
import GameEventScriptNetwork
import GameEventScriptRuntime

let network = try GesSchemaSet(parsing: source).network("Game").bind()
```

Generated code saves you from repeating route indexes and rates by hand. Name the network as a root, and
`ges schema generate` writes the wire types of its routes and one type with the network hash, a route index per
route, the rate of each topic and a typed function per route that encodes a record and sends or publishes it:

```bash
ges schema generate game.gess --root Game --language csharp --namespace Game.Net -o Game.g.cs
```

```csharp
var buffer = new byte[GameCommands.MaxByteCount];
var status = Game.ClientSendGameCommands(client, commands, buffer); // encodes and calls client.Send
var interval = Game.ServerPublishesWorldEveryMicroseconds; // 50000
```

```swift
let status = try Game.clientSendGameCommands(commands, on: client, buffer: buffer)
```

The functions allocate nothing; the buffer is yours to reuse. To receive, match the route index of a
message against the constants and read the record with the root's generated `Read`.

Compare `Game.Hash` with the bound network's hash at startup to detect a stale generation.

## 3. Connect two sides in one process

A client connection carries a token for the server, such as a login ticket. The server reports
it in a `connecting` event and accepts or refuses the client. The loopback transport moves the
packets between both connections without latency, which is how tests and single-player modes run.
Packets that never leave the process need no [encryption](#7-protect-the-connection), so both
sides lower their security level to `None`.

```csharp
var local = new GesNetworkOptions { MinimumSecurity = GesNetworkSecurity.None };
var client = GesNetworkConnection.CreateClient(network, "ticket"u8, now: 0, local);
var server = GesNetworkConnection.CreateServer(network, now: 0, local);
var loopback = new GesNetworkLoopback(client, server);

loopback.Advance(0);
for (var e = server.NextEvent(); e.Kind != GesNetworkEventKind.None; e = server.NextEvent())
{
    if (e.Kind == GesNetworkEventKind.Connecting) server.Accept();
}

loopback.Advance(0);
// client.State is now GesNetworkState.Connected.
```

```swift
var local = GesNetworkOptions()
local.minimumSecurity = .none
let client = try GesNetworkConnection.makeClient(network: network, token: Array("ticket".utf8), now: 0, options: local)
let server = try GesNetworkConnection.makeServer(network: network, now: 0, options: local)
let loopback = try GesNetworkLoopback(client: client, server: server)

try loopback.advance(0)
while true {
    let event = server.nextEvent()
    if event.kind == .none { break }
    if event.kind == .connecting { try server.accept() }
}

try loopback.advance(0)
// client.state is now .connected.
```

`GesNetworkSimulator` replaces the loopback when you want seeded latency and jitter. Between
datagram connections it can also lose, duplicate and reorder packets. Equal seeds give equal
packets, events and times in every port, so a network test is reproducible.

## 4. Send and receive

A payload is one record or message of the route's root, encoded with the
[schema writer](../../specs/Schemas.md#writer-and-reader) or generated code. The session never
decodes it.

```csharp
var status = client.Send(route: 0, payload); // GesNetworkSendStatus.Queued
```

```swift
let status = client.send(route: 0, payload: payload) // .queued
```

`Send` never throws. It reports `notConnected`, `queueFull`, `messageTooLarge` or `invalidRoute`
instead. A `message` event on the other side carries the route index and the payload; the payload
bytes stay valid until the next `NextEvent` call, so decode or copy them right away. A `closed`
event carries a `network.*` code, such as `network.timeout`, and whether the peer closed.

A message on a `bulk` route is a transfer: up to 64 MiB by default (`MaxTransferBytes`), sent with
its size and SHA-256 hash and compressed with Deflate when that is shorter. It gets its id when
you send it; read it right afterwards and cancel the transfer with that id, optionally with your
own `network.*` code:

```csharp
client.Send(route: 2, level);
var id = client.LastTransferId;
client.Cancel(route: 2, id); // the peer reports transferCancelled; the session continues
```

```swift
_ = client.send(route: 2, payload: level)
let id = client.lastTransferId
_ = try client.cancel(route: 2, transferId: id)
```

Both sides report `transferProgress` with the bytes so far and the total. The receiver checks
the size before it allocates and the hash before it reports the `message`; a transfer that fails
is cancelled with `network.transferTooLarge` or `network.transferCorrupt`.

## 5. Drive the loop

Without a loopback, your host loop does the transport's work. Each iteration:

1. passes every received packet or stream chunk to `Receive` with the current time;
2. takes events until `NextEvent` reports none, and accepts or refuses `connecting` clients;
3. calls `Advance` with the current time, at the latest at `NextDeadline`;
4. takes packets with `Transmit` into a buffer of at least 1200 bytes until it returns 0, and
   sends each one.

The connection answers with acknowledgements and keepalives by itself and closes with
`network.timeout` when the peer stays silent. `GesNetworkOptions` sets the limits and timings;
a violation by the peer closes the session with a code, never with a crash.

## 6. Use sockets

The socket packages run this loop for TCP, WebSocket and UDP. Their event loops only move bytes; your
host calls `Update` where it runs the connection, then takes the connection's events.

| Port | Package | Transports |
| --- | --- | --- |
| C# | `GameEventScript.Network.Sockets` (.NET 8) | `TcpListener`, `WebSocket.CreateFromStream`, UDP `Socket` |
| Swift | `GameEventScriptNetworkSockets` | SwiftNIO |

A TCP session uses the `stream` transport, a WebSocket session the `message` transport and a UDP
session the `datagram` transport. Listeners
choose the matching transport. With the default options a listener needs the server's
[key](#7-protect-the-connection). A server takes ready clients without waiting:

```csharp
using GameEventScript.Network.Sockets;

var options = new GesNetworkOptions { Key = serverKey };
using var listener = GesNetworkSocketListener.ListenTcp(new IPEndPoint(IPAddress.Loopback, 7000), network, options);
var session = listener.Accept(now); // null while no client is ready
session?.Update(now);
```

```swift
import GameEventScriptNetworkSockets

var options = GesNetworkOptions()
options.key = serverKey
let listener = try await GesNetworkSocketListener.listenWebSocket(host: "::", port: 7000, path: "/game", network: network, options: options)
if let session = try listener.accept(now: now) { try session.update(now: now) }
```

A client opens its session with `ConnectTcpAsync`, `ConnectWebSocketAsync` or `ConnectUdpAsync`
in C# and `connectTcp(host:port:connection:)`, `connectWebSocket(url:connection:)` or
`connectUdp(host:port:connection:)` in Swift.

A UDP session resends what was lost, restores the order of `reliable` and `bulk` messages and
delivers only the newest state of a `latest` topic. A UDP listener answers an unknown address
with a cookie first and creates a connection only for a client that returns it, so a forged
source address gains nothing. Datagrams hold at most 1200 bytes by default, which passes almost
every path.

## 7. Protect the connection

A server with a key of its own encrypts its sessions with X25519, ChaCha20-Poly1305 and
HKDF-SHA256. Generate the key once, store its private key securely and give its public key to
your clients outside the protocol, for example in the client build or a signed configuration:

```csharp
var serverKey = GesNetworkKey.Generate();
var stored = new byte[32];
serverKey.CopyPrivateKey(stored); // later: GesNetworkKey.FromPrivateKey(stored)
var server = GesNetworkConnection.CreateServer(network, now, new GesNetworkOptions { Key = serverKey });
var client = GesNetworkConnection.CreateClient(network, "ticket"u8, now,
    new GesNetworkOptions { ServerPublicKey = serverKey.PublicKey.ToArray() });
// After connecting, client.Security is GesNetworkSecurity.Verified.
```

```swift
let serverKey = GesNetworkKey.generate()
var serverOptions = GesNetworkOptions()
serverOptions.key = serverKey
var clientOptions = GesNetworkOptions()
clientOptions.serverPublicKey = serverKey.publicKey
// After connecting, client.security is .verified.
```

A client with the pinned key reaches `Verified`: the server proved that it holds the key, and
nobody else can read or change the session. The default options require it; a client without
a pinned key closes with `network.unverified`. A client whose lowest level is `Encrypted` takes
the key from the server's first answer instead. That hides the session from attackers who only
read the network, but one who can change packets can pose as the server, so use it only during
development or on a local network. A server without a key closes a client that asks for
encryption with `network.insecure`.

An unencrypted session (`None`) hides and authenticates nothing. Use it only in-process or over
a transport that protects itself, such as TLS: a browser client uses WebSocket over `wss`, for
example behind a TLS-terminating proxy in front of the listener.

## Next steps

- [GES network](../../specs/Network.md) defines the handshake, the limits, the wire format and
  every code.
- [Describe data with schemas](Schemas.md) explains the records a route carries.
