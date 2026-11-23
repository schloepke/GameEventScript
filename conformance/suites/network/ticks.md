---
formatVersion: 1
suiteId: network.ticks
title: GES network ticks
kind: networkApi
level: atomic
categories: [conformance]
tags: [portable-api, network]
---

# GES network ticks

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies ticks: several `latest` topics published as one numbered state
([GES network](../../../specs/Network.md#ticks)). The server publishes `:Pose` (4 bytes) on route 0 and `:Score`
(1 byte) on route 1 as latest, `:State` (1 byte) reliably on route 2, and the client publishes `:Input` (1 byte) as
latest on route 3. `beginTick`, `include` and `endTick` steps act on one side; `endTick` traces its status, and a
`tick` event carries the tick number.

Trace lines follow the [session suite](sessions.md).

---

## Test: A tick reports its members, then its number

Both topics published inside the tick arrive as messages, followed by a `tick` event with number 0.

### Case description

```yaml
gesBlock: case
id: tick-members
networkApi:
  sources: ["wire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Score as { points: :Number integer between 0 and 255 }\nwire schema :State as { a: :Number integer between 0 and 255 }\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :Pose latest\n  server publishes :Score latest\n  server publishes :State reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"subscribe": {"side": "server", "route": 1}}
    - {"beginTick": "server"}
    - {"publish": {"side": "server", "route": 0, "payload": "01000200"}}
    - {"publish": {"side": "server", "route": 1, "payload": "05"}}
    - {"endTick": "server"}
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
    - "server publish queued"
    - "server publish queued"
    - "server endTick queued"
    - "client message 0 01000200"
    - "client message 1 05"
    - "client tick 0"
```

---

## Test: An included member is not reported again

The second tick publishes only the score and includes the pose with its last state. The receiver holds that pose already, so it reports the new score and tick 1.

### Case description

```yaml
gesBlock: case
id: tick-include
networkApi:
  sources: ["wire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Score as { points: :Number integer between 0 and 255 }\nwire schema :State as { a: :Number integer between 0 and 255 }\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :Pose latest\n  server publishes :Score latest\n  server publishes :State reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"subscribe": {"side": "server", "route": 1}}
    - {"beginTick": "server"}
    - {"publish": {"side": "server", "route": 0, "payload": "01000200"}}
    - {"publish": {"side": "server", "route": 1, "payload": "05"}}
    - {"endTick": "server"}
    - {"advance": 0}
    - {"beginTick": "server"}
    - {"include": {"side": "server", "route": 0}}
    - {"include": {"side": "server", "route": 0}}
    - {"publish": {"side": "server", "route": 1, "payload": "06"}}
    - {"endTick": "server"}
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
    - "server publish queued"
    - "server publish queued"
    - "server endTick queued"
    - "client message 0 01000200"
    - "client message 1 05"
    - "client tick 0"
    - "server publish queued"
    - "server endTick queued"
    - "client message 1 06"
    - "client tick 1"
```

---

## Test: A replaced waiting tick keeps its published states

The second tick waits behind the first and is replaced by the third before it starts. Its published pose becomes the
latest state of its topic: it replaces the first tick's pose, which has not started yet, so tick 0 carries it. The third
tick then reports its score as tick 1.

### Case description

```yaml
gesBlock: case
id: tick-replaced
networkApi:
  sources: ["wire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Score as { points: :Number integer between 0 and 255 }\nwire schema :State as { a: :Number integer between 0 and 255 }\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :Pose latest\n  server publishes :Score latest\n  server publishes :State reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"subscribe": {"side": "server", "route": 1}}
    - {"beginTick": "server"}
    - {"publish": {"side": "server", "route": 0, "payload": "01000200"}}
    - {"publish": {"side": "server", "route": 1, "payload": "05"}}
    - {"endTick": "server"}
    - {"beginTick": "server"}
    - {"publish": {"side": "server", "route": 0, "payload": "03000400"}}
    - {"endTick": "server"}
    - {"beginTick": "server"}
    - {"publish": {"side": "server", "route": 1, "payload": "06"}}
    - {"endTick": "server"}
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
    - "server publish queued"
    - "server publish queued"
    - "server endTick queued"
    - "server publish queued"
    - "server endTick queued"
    - "server publish queued"
    - "server endTick queued"
    - "client message 0 03000400"
    - "client message 1 05"
    - "client tick 0"
    - "client message 1 06"
    - "client tick 1"
```

---

## Test: Reliable topics stay outside a tick

A reliable topic published inside a tick travels alone; an empty tick sends nothing.

### Case description

```yaml
gesBlock: case
id: tick-reliable
networkApi:
  sources: ["wire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Score as { points: :Number integer between 0 and 255 }\nwire schema :State as { a: :Number integer between 0 and 255 }\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :Pose latest\n  server publishes :Score latest\n  server publishes :State reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"subscribe": {"side": "server", "route": 1}}
    - {"subscribe": {"side": "server", "route": 2}}
    - {"beginTick": "server"}
    - {"publish": {"side": "server", "route": 2, "payload": "09"}}
    - {"endTick": "server"}
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
    - "server publish queued"
    - "server endTick queued"
    - "client message 2 09"
```

---

## Test: Tick usage errors

Ending without an open tick, opening twice and including a reliable topic, a route of the other side or a topic without a published state are usage errors. A tick before the handshake is dropped as not connected.

### Case description

```yaml
gesBlock: case
id: tick-usage
networkApi:
  sources: ["wire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Score as { points: :Number integer between 0 and 255 }\nwire schema :State as { a: :Number integer between 0 and 255 }\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :Pose latest\n  server publishes :Score latest\n  server publishes :State reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"beginTick": "server"}
    - {"endTick": "server"}
    - {"endTick": "server"}
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"subscribe": {"side": "server", "route": 0}}
    - {"beginTick": "server"}
    - {"beginTick": "server"}
    - {"include": {"side": "server", "route": 2}}
    - {"include": {"side": "server", "route": 3}}
    - {"include": {"side": "server", "route": 0}}
    - {"endTick": "server"}
```

### Expectation

```yaml
gesBlock: expect
network:
  trace:
    - "server endTick notConnected"
    - "server error invalidOperation"
    - "server connecting -"
    - "server connected"
    - "client connected"
    - "server error invalidOperation"
    - "server error invalidArgument"
    - "server error invalidArgument"
    - "server error invalidOperation"
    - "server endTick queued"
```

---

## Test: A client publishes a tick

The client's topic is always subscribed, so its tick reaches the server.

### Case description

```yaml
gesBlock: case
id: tick-client
networkApi:
  sources: ["wire schema :Pose as { x: :Number integer between 0 and 65535, y: :Number integer between 0 and 65535 }\nwire schema :Score as { points: :Number integer between 0 and 255 }\nwire schema :State as { a: :Number integer between 0 and 255 }\nwire schema :Input as { aim: :Number integer between 0 and 255 }\n\nnetwork :Game as {\n  server publishes :Pose latest\n  server publishes :Score latest\n  server publishes :State reliable\n  client publishes :Input latest\n}\n"]
  network: "Game"
  steps:
    - {"advance": 0}
    - {"accept": true}
    - {"advance": 0}
    - {"beginTick": "client"}
    - {"publish": {"side": "client", "route": 3, "payload": "07"}}
    - {"endTick": "client"}
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
    - "client publish queued"
    - "client endTick queued"
    - "server message 3 07"
    - "server tick 0"
```
