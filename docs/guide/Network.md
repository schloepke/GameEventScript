<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Connect a client and a server

> **Since: 0.6.0**

A `network` declaration in a [schema](Schemas.md) names which records a client and a server
exchange and how each one is delivered. A session carries those records between the two sides over
a TCP stream, a WebSocket, UDP datagrams or an in-process transport, in as few bits as their schema
allows and states only as far as they changed.

From the declaration, `ges schema generate` writes a typed client and a typed server: you send and
publish records and receive them in one handler method per route, without route numbers, buffers or
bytes. Everything runs on your thread when you poll; nothing starts a thread or reads a clock behind
your back. This guide declares a network, generates its code, connects two sides in one process and
then on two machines over TCP. [Without generated code](#8-without-generated-code) shows the layers below.
[GES network](../../specs/Network.md) defines the exact rules.

## 1. Declare a network

Add a network to a schema source, for example `game.gess`:

```gess
wire schema :World as {
  tick: :Number integer between 0 and 65535
  x: :Number between 0 and 100 step 0.5
}

wire messages Commands as (Move, Fire)
message Move(x: :Number between 0 and 100 step 0.5)
message Fire()

network :Game as {
  client sends Commands reliable
  server publishes :World latest at most every 50ms
}
```

```bash
ges schema check game.gess
```

Each line is a route. `client` routes go from the client to the server, `server` routes the other
way. `sends` delivers messages; `publishes` makes the route a topic whose state the receiver keeps.
The class says how a route is delivered:

| Class | Delivery |
| --- | --- |
| `reliable` | every message, complete and in order |
| `latest` | only the newest message; an unsent older one is replaced |
| `bulk` | every message, complete and in order, after all other traffic |

`at most every` documents how often a topic should be updated; generated code exposes it as a
constant, such as `Game.ServerPublishesWorldEveryMicroseconds`.

## 2. Generate the code

Name the network as a root:

```bash
ges schema generate game.gess --root Game --schemas GameSchemas --language csharp --namespace Net -o Game.g.cs
ges schema generate game.gess --root Game --schemas GameSchemas --language swift --namespace Net -o Game.swift
```

The generation holds the wire types of the routes (`World`, `Commands` with its cases `Move` and
`Fire`) and the network type `Game` with:

- `Game.Client` and `Game.Server`, the typed ends of both sides;
- `Game.Bound` (Swift `Game.bound`), the network bound once from the embedded sources;
- the network hash, the route indexes and the topic rates as constants.

Both sides must use the same declaration with the same parameter values. The handshake compares
the network hashes and closes the session with `network.schemaMismatch` on any difference, so an
outdated client cannot misread the server's records. `Game.Bound` throws when the embedded sources no
longer bind like the generated code. `--use-schemas Net.GameSchemas` lets a second generation, such as
the [models](Schemas.md#14-mark-roots-and-generate-models), share the embedded sources; see
[Schema sets](../../specs/Schemas.md#schema-sets).

## 3. Connect two sides in one process

A typed end wraps an end of its side. A pair connects a client end and a server end in one process
without sockets, which is how tests and single-player modes run. A synchronous pair moves packets at
once and runs on its own clock; a realtime pair uses the system clock.

```csharp
using GameEventScript.Api; // GesSystemClock, GesSchemaSet
using GameEventScript.Network;
using Net;

using var pair = GesNetworkPair.Synchronous(Game.Bound, "ticket"u8);
var client = new Game.Client(pair.Client);
var server = new Game.Server(pair.Server);

var view = new ClientView();
var rules = new ServerRules();
client.Poll(ref view);   // OnConnected
server.Poll(ref rules);  // OnClientConnected

struct ClientView : Game.IClientHandler
{
    public World World;
    public void OnWorld(in World value) => World = value;
    public void OnReceived() { /* the view is complete: draw it */ }
}

struct ServerRules : Game.IServerHandler
{
    public void OnCommands(int client, Commands.Case value)
    {
        if (value is Commands.Case.Move move) Console.WriteLine($"{client} moves to {move.Value.X}");
    }
}
```

```swift
import GameEventScriptNetwork

let pair = try GesNetworkPair.synchronous(network: Net.Game.bound, token: Array("ticket".utf8))
defer { pair.release() }
let client = try Net.Game.Client(pair.client)
let server = try Net.Game.Server(pair.server)

try client.poll { event in
    switch event {
    case .connected(let security): print("connected", security)
    case .world(let world): view.world = world
    case .received: view.draw()
    default: break
    }
}
try server.poll { event in
    if case .commands(let client, .move(let move)) = event { print(client, "moves to", move.x) }
}
```

A C# handler implements only the methods it needs; every method has an empty default, and a struct
handler is called without allocating. A Swift closure switches over the end's event enum. A received
record is borrowed until the handler returns: copy what you keep. The client token, such as a login
ticket, reaches the server's `OnConnecting` (Swift `.connecting`); the server accepts the client
after the handler unless it called `Refuse`. A synchronous pair accepts without that event.

## 4. Send, publish and receive

Each outgoing route has a method named after its verb. It encodes the record into a buffer the end
owns, allocates nothing and returns a status instead of throwing on network conditions:

```csharp
client.Send(new Commands.Case.Move(new Move { X = 12.5 })); // GesNetworkSendStatus.Queued
server.Publish(world);                                     // every subscribed client
```

```swift
var move = Net.Move()
move.x = 12.5
_ = try client.send(.move(move)) // throws only when the record does not encode
_ = try server.publish(world)
```

The status reports `queued`, `notConnected`, `queueFull` or `messageTooLarge`. A server also sends to
one client with `Send(value, client)` (Swift `send(_:client:)`). Sends made before the handshake
completes wait in the end and leave once it does.

**Topics.** `Publish` sends a state, a complete record of the topic's schema. The session sends it
as a [delta](Schemas.md#12-send-only-what-changed) against the newest state the receiver has
confirmed when that is shorter, and the receiver always reports the full record, so you never handle
baselines. A server end subscribes every client to every topic; create its end with
`subscribeAll: false` and call `Subscribe(client, Game.Server.Topic.World)` and `Unsubscribe` (Swift
`subscribe(client:topic:)`, `unsubscribe(client:topic:)`) to decide who receives what.
[Topics](../../specs/Network.md#topics) defines when a state counts as confirmed.

**Ticks.** A server that publishes several topics opens a tick, so a client receives them as one
consistent state. A client that joins later gets the last state of every included topic with its
first tick:

```csharp
using (var tick = server.BeginTick())
{
    tick.Publish(world);
}
```

```swift
try server.publishTick { tick in
    try tick.publish(world)
}
```

**Receiving.** A poll reports the records of every route, then `OnTick` (Swift `.tick`) per complete
tick and finally `OnReceived` (Swift `.received`) once per poll that received anything. At
`OnReceived` every state of the latest tick and every message of the poll has arrived, so commit your
view there.

**Large data.** A message on a `bulk` route is a transfer of up to 64 MiB by default, sent with its
size and SHA-256 hash and compressed when that is shorter. Both sides report `OnTransferProgress`; a
transfer that fails is cancelled with `network.transferTooLarge` or `network.transferCorrupt` and the
session continues.

**Closing.** `Close(detail)` closes the client's connection or every client's; on the server
`Close(client, detail)` closes one. The other side reports `OnDisconnected` or `OnClientDisconnected`
with a `network.*` code, such as `network.timeout`, and whether the peer closed.

## 5. Drive the loop

Call `Poll` once per frame, or whenever the end has work. One poll reads the end's clock, accepts
clients, passes received packets, reports events to your handler and transmits what is due,
including what the handler sent. Sends, publishes and ticks outside a poll leave with the next poll.
Poll no later than the end's `NextDeadline` (Swift `nextDeadline`) so acknowledgements, keepalives and
resends happen in time.

A synchronous pair moves on its own clock: `pair.Advance(microseconds)` lets timers such as keepalives
and timeouts fire in a test. `GesNetworkSimulator` adds seeded latency and jitter, and between datagram
connections loss, duplicates and reordering; equal seeds give equal packets, events and times in every
port, so a network test is reproducible.

## 6. Connect two machines over TCP

The socket packages provide the listeners and connectors that put an end on a real network, and a
wait that sleeps until the end has work:

| Port | Package | Transports |
| --- | --- | --- |
| C# | `GameEventScript.Network.Sockets` (.NET 8) | `TcpListener`, `WebSocket.CreateFromStream`, UDP `Socket` |
| Swift | `GameEventScriptNetworkSockets` | SwiftNIO |

A server end takes a listener as its acceptor, a client end a connector. The presets `Stream` (TCP),
`Message` (WebSocket) and `Datagram` (UDP) of `GesNetworkOptions` set the transport and the
[security](#7-protect-the-connection) of one side. The following two programs run on two machines:
a dedicated server and a client that connects to it by host name or IP address.

**The server.** It creates its key on the first start, keeps the private key in `server.key` and prints
the public key, which every client pins. It listens on port 7777 of every network interface, applies
the clients' moves to the world and publishes the world after each change:

```csharp
using System.Net;
using GameEventScript.Api;
using GameEventScript.Network;
using GameEventScript.Network.Sockets;
using Net;

if (!File.Exists("server.key"))
{
    var privateKey = new byte[32];
    GesNetworkKey.Generate().CopyPrivateKey(privateKey);
    File.WriteAllBytes("server.key", privateKey);
}
var serverKey = GesNetworkKey.FromPrivateKey(File.ReadAllBytes("server.key"));
Console.WriteLine($"Public key: {Convert.ToHexString(serverKey.PublicKey)}");

var listener = GesNetworkSocketListener.ListenTcp(new IPEndPoint(IPAddress.Any, 7777), Game.Bound,
    GesNetworkOptions.Stream(GesNetworkServerSecurity.Key(serverKey)));
using var end = new GesNetworkServerEnd(Game.Bound, listener, new GesSystemClock());
var server = new Game.Server(end);
var rules = new ServerRules();
while (true)
{
    await end.WaitAsync(); // until a socket has input or the next deadline passes
    server.Poll(ref rules);
    if (rules.Changed)
    {
        rules.World.Tick = (rules.World.Tick + 1) % 65536;
        using (var tick = server.BeginTick()) tick.Publish(rules.World);
        rules.Changed = false;
    }
}

struct ServerRules : Game.IServerHandler
{
    public World World;
    public bool Changed;

    public void OnClientConnected(int client, GesNetworkSecurity security) =>
        Console.WriteLine($"Client {client} joined ({security})");

    public void OnClientDisconnected(int client, string code, string detail, bool byPeer) =>
        Console.WriteLine($"Client {client} left: {code}");

    public void OnCommands(int client, Commands.Case value)
    {
        if (value is Commands.Case.Move move)
        {
            World.X = move.Value.X;
            Changed = true;
        }
    }
}
```

```swift
import Foundation
import GameEventScriptNetwork
import GameEventScriptNetworkSockets
import GameEventScriptRuntime // GesSystemClock

let keyFile = URL(fileURLWithPath: "server.key")
if !FileManager.default.fileExists(atPath: keyFile.path) {
    var privateKey = [UInt8](repeating: 0, count: 32)
    try privateKey.withUnsafeMutableBytes { try GesNetworkKey.generate().copyPrivateKey(into: $0) }
    try Data(privateKey).write(to: keyFile)
}
let serverKey = try GesNetworkKey(privateKey: [UInt8](try Data(contentsOf: keyFile)))
print("Public key:", serverKey.publicKey.map { String(format: "%02X", $0) }.joined())

let listener = try await GesNetworkSocketListener.listenTcp(host: "0.0.0.0", port: 7777,
    network: Net.Game.bound, options: .stream(server: .key(serverKey)))
let server = try Net.Game.Server(GesNetworkServerEnd(network: Net.Game.bound, acceptor: listener, clock: GesSystemClock()))
var world = Net.World()
while true {
    try await server.end.wait() // until a socket has input or the next deadline passes
    var changed = false
    try server.poll { event in
        switch event {
        case .clientConnected(let client, let security): print("Client \(client) joined (\(security))")
        case .clientDisconnected(let client, let code, _, _): print("Client \(client) left: \(code)")
        case .commands(_, .move(let move)):
            world.x = move.x
            changed = true
        default: break
        }
    }
    if changed {
        world.tick = (world.tick + 1) % 65536
        try server.publishTick { tick in try tick.publish(world) }
    }
}
```

The server publishes inside a tick, so a client that joins later receives the current world with its
first tick. `IPAddress.Any` and `"0.0.0.0"` accept IPv4 clients on every network interface; listen on
`IPAddress.IPv6Any` or `"::"` for IPv6, or on one address to restrict the interface. Open TCP port
7777 in the server's firewall, and forward it on the router when clients connect from the internet.

**The client.** It takes the server's host name or address and the printed public key as arguments,
for example `dotnet run -- 192.168.1.20 5F3A…`. A shipped game builds the public key into the client
or reads it from a signed configuration instead:

```csharp
using GameEventScript.Api;
using GameEventScript.Network;
using GameEventScript.Network.Sockets;
using Net;

var serverPublicKey = Convert.FromHexString(args[1]);
using var end = new GesNetworkClientEnd(Game.Bound, GesNetworkSocketConnector.Tcp(args[0], 7777),
    GesNetworkOptions.Stream(GesNetworkClientSecurity.Pinned(serverPublicKey)), "ticket"u8, new GesSystemClock());
var client = new Game.Client(end);
var view = new ClientView();
client.Send(new Commands.Case.Move(new Move { X = 12.5 })); // leaves once the handshake completes
while (!view.Disconnected)
{
    await end.WaitAsync(); // a game polls once per frame instead
    client.Poll(ref view);
}

struct ClientView : Game.IClientHandler
{
    public bool Disconnected;

    public void OnConnected(GesNetworkSecurity security) => Console.WriteLine($"Connected ({security})");

    public void OnWorld(in World value) => Console.WriteLine($"Tick {value.Tick}: x = {value.X}");

    public void OnDisconnected(string code, string detail, bool byPeer)
    {
        Console.WriteLine($"Disconnected: {code}");
        Disconnected = true;
    }
}
```

```swift
import GameEventScriptNetwork
import GameEventScriptNetworkSockets
import GameEventScriptRuntime // GesSystemClock

let hex = Array(CommandLine.arguments[2])
let serverPublicKey = stride(from: 0, to: hex.count, by: 2).map { UInt8(String(hex[$0 ... $0 + 1]), radix: 16)! }
let client = try Net.Game.Client(GesNetworkClientEnd(network: Net.Game.bound,
    connector: try GesNetworkSocketConnector.tcp(host: CommandLine.arguments[1], port: 7777),
    options: .stream(client: try .pinned(serverPublicKey)), token: Array("ticket".utf8), clock: GesSystemClock()))
var move = Net.Move()
move.x = 12.5
_ = try client.send(.move(move)) // leaves once the handshake completes
var connected = true
while connected {
    try await client.end.wait() // a game polls once per frame instead
    try client.poll { event in
        switch event {
        case .connected(let security): print("Connected (\(security))")
        case .world(let world): print("Tick \(world.tick): x = \(world.x)")
        case .disconnected(let code, _, _):
            print("Disconnected: \(code)")
            connected = false
        default: break
        }
    }
}
```

The client prints `Connected (Verified)` (Swift `verified`): the server proved that it holds the key
the client pinned, and the session is encrypted. The server prints `Client 0 joined (Encrypted)`,
because only the client knows whether it pinned the key. A client that cannot reach the
server reports `OnDisconnected` (Swift `.disconnected`) with `network.timeout` or a connection error.

A game with a frame loop polls once per frame and needs no wait; a dedicated server waits between
polls (C# `WaitAsync` or the blocking `Wait`, Swift `wait()`). An end belongs to one caller at a time;
a Swift end lives inside an actor, such as a `@MainActor` view model, and its handler reads that
actor's state directly.

**WebSocket.** Browsers and many hosting providers reach a server only through WebSocket. Only the
listener, the connector and the preset change:

```csharp
var listener = GesNetworkSocketListener.ListenWebSocket(new IPEndPoint(IPAddress.Any, 7777), "/game",
    Game.Bound, GesNetworkOptions.Message(GesNetworkServerSecurity.Key(serverKey)));
var connector = GesNetworkSocketConnector.WebSocket(new Uri("wss://game.example.com/game"));
var options = GesNetworkOptions.Message(GesNetworkClientSecurity.Pinned(serverPublicKey));
```

```swift
let listener = try await GesNetworkSocketListener.listenWebSocket(host: "0.0.0.0", port: 7777, path: "/game",
    network: Net.Game.bound, options: .message(server: .key(serverKey)))
let connector = try GesNetworkSocketConnector.webSocket("wss://game.example.com/game")
let options = GesNetworkOptions.message(client: try .pinned(serverPublicKey))
```

The listener speaks plain `ws`; for `wss`, put a TLS-terminating proxy in front of it.

**UDP.** A UDP session never waits for a lost packet before delivering newer ones, which suits fast
games:

```csharp
var listener = GesNetworkSocketListener.ListenUdp(new IPEndPoint(IPAddress.Any, 7777), Game.Bound,
    GesNetworkOptions.Datagram(GesNetworkServerSecurity.Key(serverKey)));
var connector = GesNetworkSocketConnector.Udp("game.example.com", 7777);
var options = GesNetworkOptions.Datagram(GesNetworkClientSecurity.Pinned(serverPublicKey));
```

```swift
let listener = try await GesNetworkSocketListener.listenUdp(host: "0.0.0.0", port: 7777,
    network: Net.Game.bound, options: .datagram(server: .key(serverKey)))
let connector = try GesNetworkSocketConnector.udp(host: "game.example.com", port: 7777)
let options = GesNetworkOptions.datagram(client: try .pinned(serverPublicKey))
```

It resends what was lost, restores the order of `reliable` and `bulk` messages and delivers only the
newest state of a `latest` topic. A UDP listener answers an unknown address with a cookie first and
creates a connection only for a client that returns it, so a forged source address gains nothing.
Datagrams hold at most 1200 bytes by default, which passes almost every path. A listener holds at
most 64 clients that wait for the end, at most 8 of them from one IP address. Open the port for UDP
in the firewall.

## 7. Protect the connection

A server with a key of its own encrypts its sessions with X25519, ChaCha20-Poly1305 and
HKDF-SHA256. Generate the key once, store its private key securely and give its public key to
your clients outside the protocol, for example in the client build or a signed configuration:

```csharp
var serverKey = GesNetworkKey.Generate();
var stored = new byte[32];
serverKey.CopyPrivateKey(stored); // later: GesNetworkKey.FromPrivateKey(stored)
var serverOptions = GesNetworkOptions.Message(GesNetworkServerSecurity.Key(serverKey));
var clientOptions = GesNetworkOptions.Message(GesNetworkClientSecurity.Pinned(serverKey.PublicKey));
```

```swift
let serverKey = GesNetworkKey.generate()
let serverOptions = GesNetworkOptions.message(server: .key(serverKey))
let clientOptions = GesNetworkOptions.message(client: try .pinned(serverKey.publicKey))
```

A client with the pinned key (`Pinned`, Swift `.pinned`) reaches `Verified`: the server proved
that it holds the key, and nobody else can read or change the session. A client in development
mode (`Development`) takes the key from the server's first answer instead and reaches
`Encrypted`; `OnConnected` (Swift `.connected`) reports the level the connection reached. That hides the session from attackers who only
read the network, but one who can change packets can pose as the server, so use it only during
development or on a local network. A server without a key closes a client that asks for
encryption with `network.insecure`.

An unencrypted session (`GesNetworkClientSecurity.None` and `GesNetworkServerSecurity.None`,
Swift `.unencrypted`) hides and authenticates nothing. Use it only in-process or over
a transport that protects itself, such as TLS: a browser client uses WebSocket over `wss`, for
example behind a TLS-terminating proxy in front of the listener.

## 8. Without generated code

The typed ends are a thin layer; everything below them stays public for tools, proxies and hosts that
read a schema only at run time.

**Bind at run time.** `Bind` parses the declaration and returns a `GesBoundNetwork`, a description of
the routes, their classes, the largest record sizes and the hash. It encodes nothing itself: payloads
come from the [schema writer](../../specs/Schemas.md#writer-and-reader), from
`GesSchemaValues` or from generated wire types.

```csharp
var network = GesSchemaSet.Parse(File.ReadAllText("game.gess")).Network("Game").Bind();
```

```swift
let network = try GesSchemaSet(parsing: source).network("Game").bind()
```

**Untyped ends.** `GesNetworkClientEnd` and `GesNetworkServerEnd` do everything a typed end does, with
route indexes and payload bytes instead of records. The position of a route in the declaration,
counting from 0, is its index; generated code names them, such as `Game.ClientSendsCommands`. A handler
implements `IGesNetworkEndHandler` (Swift: a closure) and receives `GesNetworkEndEvent`s whose payload
is borrowed until it returns. A typed end exposes its untyped end as `End` (Swift `end`).

```csharp
var status = pair.Client.Send(route: 0, payload);
pair.Server.Poll(ref handler); // a message event with the route index and the payload
```

```swift
let status = pair.client.send(route: 0, payload: payload)
try pair.server.poll { event in print(event.kind, event.route) }
```

**Connections.** `GesNetworkConnection` is the synchronous state machine of one session, without
transport or clock. A typed or untyped end exposes it as `Connection` (Swift `connection`), on the server
`Connection(client)`, for example to cancel a bulk transfer:

```csharp
var connection = client.End.Connection;
connection.Send(levelRoute, level);
connection.Cancel(levelRoute, connection.LastTransferId); // the peer reports transferCancelled
```

```swift
let connection = client.end.connection
_ = connection.send(route: levelRoute, payload: level)
_ = try connection.cancel(route: levelRoute, transferId: connection.lastTransferId)
```

To drive a bare connection yourself, each iteration of your host loop:

1. passes every received packet or stream chunk to `Receive` with the current time;
2. takes events until `NextEvent` reports none, and accepts or refuses `connecting` clients;
3. calls `Advance` with the current time, at the latest at `NextDeadline`;
4. takes packets with `Transmit` into a buffer of at least 1200 bytes until it returns 0, and
   sends each one.

The connection answers with acknowledgements and keepalives by itself and closes with
`network.timeout` when the peer stays silent. `GesNetworkOptions` sets the limits and timings; a
violation by the peer closes the session with a code, never with a crash. A listener's `Accept`
hands out socket sessions with their connection for a host that drives them this way.

## Next steps

- [GES network](../../specs/Network.md) defines the handshake, the limits, the wire format and
  every code.
- [Describe data with schemas](Schemas.md) explains the records a route carries.
