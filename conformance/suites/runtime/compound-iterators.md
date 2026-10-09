---
formatVersion: 1
suiteId: runtime.compound-iterators
title: Compound iterator execution
kind: programBinary
level: atomic
categories: [conformance, program-binary]
---

# Compound iterator execution

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

Hand-encoded Programs exercise instruction execution independently of source lowering.

---

## Test: cartesian

This case checks cartesian through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: cartesian
binaryFixture:
  id: execute-cartesian
  resourceId: gesb-v1.execute-cartesian
  relativePath: GesbV1/execute-cartesian.gesb
  sha256: 3E38D7D008C0B0011A5E8A1D0F48127D8A3FC0EF0B4BB1ADF6DF231C63855A72
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,bea00200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "3E38D7D008C0B0011A5E8A1D0F48127D8A3FC0EF0B4BB1ADF6DF231C63855A72"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
                  -
                    type: ":Number.int64"
                    value: "4"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "1"
                -
                  type: ":Number.int64"
                  value: "3"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "1"
                -
                  type: ":Number.int64"
                  value: "4"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "2"
                -
                  type: ":Number.int64"
                  value: "3"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "2"
                -
                  type: ":Number.int64"
                  value: "4"
```

---

## Test: cartesian-components

This case checks cartesian-components through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: cartesian-components
binaryFixture:
  id: execute-cartesian-components
  resourceId: gesb-v1.execute-cartesian-components
  relativePath: GesbV1/execute-cartesian-components.gesb
  sha256: 4717F86A56832E2645089933E4EAC25D27E8C24B0B20E87E6770F36DA1E8ACB0
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,bea00200030000000000000000000000,c0200400020008000000000000000000,0c000000000000000000000000000000,0c000000000002000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4717F86A56832E2645089933E4EAC25D27E8C24B0B20E87E6770F36DA1E8ACB0"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
                  -
                    type: ":Number.int64"
                    value: "4"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "4"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "4"
```

---

## Test: lockstep

This case checks lockstep through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: lockstep
binaryFixture:
  id: execute-lockstep
  resourceId: gesb-v1.execute-lockstep
  relativePath: GesbV1/execute-lockstep.gesb
  sha256: 1684C120A2CE9E25903EAC2C411336C0129F288871C17BC2DE9F278F74CC7952
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be800200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "1684C120A2CE9E25903EAC2C411336C0129F288871C17BC2DE9F278F74CC7952"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "4"
                  -
                    type: ":Number.int64"
                    value: "5"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "1"
                -
                  type: ":Number.int64"
                  value: "4"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "2"
                -
                  type: ":Number.int64"
                  value: "5"
```

---

## Test: lockstep-components

This case checks lockstep-components through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: lockstep-components
binaryFixture:
  id: execute-lockstep-components
  resourceId: gesb-v1.execute-lockstep-components
  relativePath: GesbV1/execute-lockstep-components.gesb
  sha256: 4D1E19725CF4473C964AC3886CA23D58E78A53EF798C67621DB5A7C915121E6E
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be800200030000000000000000000000,c0200400020008000000000000000000,0c000000000000000000000000000000,0c000000000002000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4D1E19725CF4473C964AC3886CA23D58E78A53EF798C67621DB5A7C915121E6E"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: empty-cartesian

This case checks empty-cartesian through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: empty-cartesian
binaryFixture:
  id: execute-empty-cartesian
  resourceId: gesb-v1.execute-empty-cartesian
  relativePath: GesbV1/execute-empty-cartesian.gesb
  sha256: 4717F86A56832E2645089933E4EAC25D27E8C24B0B20E87E6770F36DA1E8ACB0
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,bea00200030000000000000000000000,c0200400020008000000000000000000,0c000000000000000000000000000000,0c000000000002000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4717F86A56832E2645089933E4EAC25D27E8C24B0B20E87E6770F36DA1E8ACB0"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items: []
    local: []
```

---

## Test: union-list

This case checks union-list through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: union-list
binaryFixture:
  id: execute-union-list
  resourceId: gesb-v1.execute-union-list
  relativePath: GesbV1/execute-union-list.gesb
  sha256: 4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be200200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: intersect-list

This case checks intersect-list through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: intersect-list
binaryFixture:
  id: execute-intersect-list
  resourceId: gesb-v1.execute-intersect-list
  relativePath: GesbV1/execute-intersect-list.gesb
  sha256: 27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be400200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
                  -
                    type: ":Number.int64"
                    value: "3"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: difference-list

This case checks difference-list through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: difference-list
binaryFixture:
  id: execute-difference-list
  resourceId: gesb-v1.execute-difference-list
  relativePath: GesbV1/execute-difference-list.gesb
  sha256: 49E674A33D5826B27140CC5D4014FAE3AE407D1ACB90DD333331494F1D3B5023
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be600200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "49E674A33D5826B27140CC5D4014FAE3AE407D1ACB90DD333331494F1D3B5023"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "2"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: difference-nothing

This case checks difference-nothing through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: difference-nothing
binaryFixture:
  id: execute-difference-nothing
  resourceId: gesb-v1.execute-difference-nothing
  relativePath: GesbV1/execute-difference-nothing.gesb
  sha256: 49E674A33D5826B27140CC5D4014FAE3AE407D1ACB90DD333331494F1D3B5023
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be600200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "49E674A33D5826B27140CC5D4014FAE3AE407D1ACB90DD333331494F1D3B5023"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Nothing"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Nothing"
              -
                type: ":Nothing"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Nothing"
```

---

## Test: map-union

This case checks map-union through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: map-union
binaryFixture:
  id: execute-map-union
  resourceId: gesb-v1.execute-map-union
  relativePath: GesbV1/execute-map-union.gesb
  sha256: 4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be200200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":Map"
                entries:
                  -
                    key: "a"
                    value:
                      type: ":Number.int64"
                      value: "1"
                  -
                    key: "b"
                    value:
                      type: ":Number.int64"
                      value: "2"
              -
                type: ":Map"
                entries:
                  -
                    key: "b"
                    value:
                      type: ":Number.int64"
                      value: "3"
                  -
                    key: "c"
                    value:
                      type: ":Number.int64"
                      value: "4"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "4"
```

---

## Test: map-keys-union

This case checks map-keys-union through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: map-keys-union
binaryFixture:
  id: execute-map-keys-union
  resourceId: gesb-v1.execute-map-keys-union
  relativePath: GesbV1/execute-map-keys-union.gesb
  sha256: 4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be200200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":Map"
                entries:
                  -
                    key: "a"
                    value:
                      type: ":Number.int64"
                      value: "1"
              -
                type: ":List"
                items:
                  -
                    type: ":Text"
                    value: "a"
                  -
                    type: ":Text"
                    value: "b"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: map-intersect

This case checks map-intersect through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: map-intersect
binaryFixture:
  id: execute-map-intersect
  resourceId: gesb-v1.execute-map-intersect
  relativePath: GesbV1/execute-map-intersect.gesb
  sha256: 27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be400200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":Map"
                entries:
                  -
                    key: "a"
                    value:
                      type: ":Number.int64"
                      value: "1"
                  -
                    key: "b"
                    value:
                      type: ":Number.int64"
                      value: "2"
              -
                type: ":Map"
                entries:
                  -
                    key: "b"
                    value:
                      type: ":Number.int64"
                      value: "3"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: map-difference

This case checks map-difference through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: map-difference
binaryFixture:
  id: execute-map-difference
  resourceId: gesb-v1.execute-map-difference
  relativePath: GesbV1/execute-map-difference.gesb
  sha256: 49E674A33D5826B27140CC5D4014FAE3AE407D1ACB90DD333331494F1D3B5023
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be600200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "49E674A33D5826B27140CC5D4014FAE3AE407D1ACB90DD333331494F1D3B5023"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":Map"
                entries:
                  -
                    key: "a"
                    value:
                      type: ":Number.int64"
                      value: "1"
                  -
                    key: "b"
                    value:
                      type: ":Number.int64"
                      value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Text"
                    value: "a"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: invalid-list-map

This case checks invalid-list-map through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: invalid-list-map
binaryFixture:
  id: execute-invalid-list-map
  resourceId: gesb-v1.execute-invalid-list-map
  relativePath: GesbV1/execute-invalid-list-map.gesb
  sha256: 4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be200200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
              -
                type: ":Map"
                entries:
                  -
                    key: "a"
                    value:
                      type: ":Number.int64"
                      value: "2"
    local: []
```

---

## Test: invalid-map-key

This case checks invalid-map-key through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: invalid-map-key
binaryFixture:
  id: execute-invalid-map-key
  resourceId: gesb-v1.execute-invalid-map-key
  relativePath: GesbV1/execute-invalid-map-key.gesb
  sha256: 4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be200200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4F66E9F1DC75B00F809B581931131F5A97B07034922B24A7C7D724BF1FD81D99"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":Map"
                entries:
                  -
                    key: "a"
                    value:
                      type: ":Number.int64"
                      value: "1"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "2"
    local: []
```

---

## Test: entries

This case checks entries through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: entries
binaryFixture:
  id: execute-entries
  resourceId: gesb-v1.execute-entries
  relativePath: GesbV1/execute-entries.gesb
  sha256: 2F8CF9A5D388D1C9162F2974ED384C6493B3027CECE1D10E06BB672DB5D18266
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bec00200000000000000000000000000,c0000000020005000000000000000000,0c000000000000000000000000000000,02000000000002000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "2F8CF9A5D388D1C9162F2974ED384C6493B3027CECE1D10E06BB672DB5D18266"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":Map"
            entries:
              -
                key: "a"
                value:
                  type: ":Number.int64"
                  value: "1"
              -
                key: "b"
                value:
                  type: ":Number.int64"
                  value: "2"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Map"
              entries:
                -
                  key: "key"
                  value:
                    type: ":Text"
                    value: "a"
                -
                  key: "value"
                  value:
                    type: ":Number.int64"
                    value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Map"
              entries:
                -
                  key: "key"
                  value:
                    type: ":Text"
                    value: "b"
                -
                  key: "value"
                  value:
                    type: ":Number.int64"
                    value: "2"
```

---

## Test: entries-components

This case checks entries-components through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: entries-components
binaryFixture:
  id: execute-entries-components
  resourceId: gesb-v1.execute-entries-components
  relativePath: GesbV1/execute-entries-components.gesb
  sha256: BCDE2AF6610F33442F2CE5B745564DB94ABAA5812F638C60AA27112DCFDEF5AA
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bec00200000000000000000000000000,c0200400020006000000000000000000,0c000000000000000000000000000000,0c000000000002000000000000000000,02000000000002000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "BCDE2AF6610F33442F2CE5B745564DB94ABAA5812F638C60AA27112DCFDEF5AA"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":Map"
            entries:
              -
                key: "a"
                value:
                  type: ":Number.int64"
                  value: "1"
              -
                key: "b"
                value:
                  type: ":Number.int64"
                  value: "2"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Text"
              value: "a"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Text"
              value: "b"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: normal-components

This case checks normal-components through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: normal-components
binaryFixture:
  id: execute-normal-components
  resourceId: gesb-v1.execute-normal-components
  relativePath: GesbV1/execute-normal-components.gesb
  sha256: 5F40ED0229F769DC12B8CF7685A569E693CCAEF159BB030BE4D3D10F5A54C41C
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,be000200000000000000000000000000,c0200400020006000000000000000000,0c000000000000000000000000000000,0c000000000002000000000000000000,02000000000002000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "5F40ED0229F769DC12B8CF7685A569E693CCAEF159BB030BE4D3D10F5A54C41C"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "4"
              -
                type: ":Number.int64"
                value: "5"
              -
                type: ":Map"
                entries:
                  -
                    key: "a"
                    value:
                      type: ":Number.int64"
                      value: "6"
                  -
                    key: "b"
                    value:
                      type: ":Number.int64"
                      value: "7"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "4"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Nothing"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "5"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Nothing"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "6"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "7"
```

---

## Test: cartesian-three

This case checks cartesian-three through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: cartesian-three
binaryFixture:
  id: execute-cartesian-three
  resourceId: gesb-v1.execute-cartesian-three
  relativePath: GesbV1/execute-cartesian-three.gesb
  sha256: FDCBA2BCE9D098168A1EC02BBE6D1297A8EBC340450BFB6E60723937470D7468
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,bea00200050000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "FDCBA2BCE9D098168A1EC02BBE6D1297A8EBC340450BFB6E60723937470D7468"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "1"
                -
                  type: ":Number.int64"
                  value: "3"
                -
                  type: ":Number.int64"
                  value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "1"
                -
                  type: ":Number.int64"
                  value: "3"
                -
                  type: ":Number.int64"
                  value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "2"
                -
                  type: ":Number.int64"
                  value: "3"
                -
                  type: ":Number.int64"
                  value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "2"
                -
                  type: ":Number.int64"
                  value: "3"
                -
                  type: ":Number.int64"
                  value: "2"
```

---

## Test: lockstep-three

This case checks lockstep-three through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: lockstep-three
binaryFixture:
  id: execute-lockstep-three
  resourceId: gesb-v1.execute-lockstep-three
  relativePath: GesbV1/execute-lockstep-three.gesb
  sha256: DAE0D7CBC1A0C4B89E52465B2BE5301064166A155ED78378D9DF66FC93F563B1
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be800200050000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "DAE0D7CBC1A0C4B89E52465B2BE5301064166A155ED78378D9DF66FC93F563B1"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":Number.int64"
                  value: "1"
                -
                  type: ":Number.int64"
                  value: "3"
                -
                  type: ":Number.int64"
                  value: "1"
```

---

## Test: union-three

This case checks union-three through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: union-three
binaryFixture:
  id: execute-union-three
  resourceId: gesb-v1.execute-union-three
  relativePath: GesbV1/execute-union-three.gesb
  sha256: 6B3A9BD36F7C39B3C3D5A81E18CDB45C8F0D57E556000D1AC66DEAD8A0FCFA1A
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be200200050000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "6B3A9BD36F7C39B3C3D5A81E18CDB45C8F0D57E556000D1AC66DEAD8A0FCFA1A"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: search-limit

This case checks search-limit through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: search-limit
runtimeLimits: { maxLoopIterations: 5 }
binaryFixture:
  id: execute-search-limit
  resourceId: gesb-v1.execute-search-limit
  relativePath: GesbV1/execute-search-limit.gesb
  sha256: 27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be400200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "9"
    local: []
    runtimeLimits:
      include:
        -
          name: "MaxLoopIterations"
          limit: 5
```

---

## Test: search-exact

This case checks search-exact through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: search-exact
runtimeLimits: { maxLoopIterations: 6 }
binaryFixture:
  id: execute-search-exact
  resourceId: gesb-v1.execute-search-exact
  relativePath: GesbV1/execute-search-exact.gesb
  sha256: 27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be400200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "27AD49A5895944BBF4AF90A960AEF49DFC1689C50422E40B20643258862C6908"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
                  -
                    type: ":Number.int64"
                    value: "3"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "9"
    local: []
    runtimeLimits:
      exclude:
        -
          name: "MaxLoopIterations"
          limit: 6
```

---

## Test: components-limit

This case checks components-limit through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: components-limit
runtimeLimits: { maxGeneratedCollectionItems: 1 }
binaryFixture:
  id: execute-components-limit
  resourceId: gesb-v1.execute-components-limit
  relativePath: GesbV1/execute-components-limit.gesb
  sha256: 4717F86A56832E2645089933E4EAC25D27E8C24B0B20E87E6770F36DA1E8ACB0
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,bea00200030000000000000000000000,c0200400020008000000000000000000,0c000000000000000000000000000000,0c000000000002000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "4717F86A56832E2645089933E4EAC25D27E8C24B0B20E87E6770F36DA1E8ACB0"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "2"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
    runtimeLimits:
      exclude:
        -
          name: "MaxGeneratedCollectionItems"
          limit: 1
```

---

## Test: row-limit

This case checks row-limit through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: row-limit
runtimeLimits: { maxGeneratedCollectionItems: 1 }
binaryFixture:
  id: execute-row-limit
  resourceId: gesb-v1.execute-row-limit
  relativePath: GesbV1/execute-row-limit.gesb
  sha256: 3E38D7D008C0B0011A5E8A1D0F48127D8A3FC0EF0B4BB1ADF6DF231C63855A72
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,bea00200030000000000000000000000,c0000000020007000000000000000000,0c000000000000000000000000000000,02000000000004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "3E38D7D008C0B0011A5E8A1D0F48127D8A3FC0EF0B4BB1ADF6DF231C63855A72"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "2"
    local: []
    runtimeLimits:
      include:
        -
          name: "MaxGeneratedCollectionItems"
          limit: 1
```

---

## Test: binary-limit

This case checks binary-limit through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: binary-limit
runtimeLimits: { maxGeneratedCollectionItems: 1 }
binaryFixture:
  id: execute-binary-limit
  resourceId: gesb-v1.execute-binary-limit
  relativePath: GesbV1/execute-binary-limit.gesb
  sha256: 8848C5E1276E311BFDBDD85E5592FF3A28DB0A44E8AAA5C17BDB2EFE24A684DD
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,e0000000000001000000000000000000,0c000000000000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "8848C5E1276E311BFDBDD85E5592FF3A28DB0A44E8AAA5C17BDB2EFE24A684DD"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
                  -
                    type: ":Number.int64"
                    value: "4"
    local: []
    runtimeLimits:
      include:
        -
          name: "MaxGeneratedCollectionItems"
          limit: 1
```

---

## Test: binary-cartesian

This case checks binary-cartesian through native instruction execution and message observations.

### Case description

```yaml
gesBlock: case
id: binary-cartesian
binaryFixture:
  id: execute-binary-cartesian
  resourceId: gesb-v1.execute-binary-cartesian
  relativePath: GesbV1/execute-binary-cartesian.gesb
  sha256: 8848C5E1276E311BFDBDD85E5592FF3A28DB0A44E8AAA5C17BDB2EFE24A684DD
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,e0000000000001000000000000000000,0c000000000000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists with 06000000010000000000000000000100000001000200000000000100020000000000010003000000000001000000; update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "8848C5E1276E311BFDBDD85E5592FF3A28DB0A44E8AAA5C17BDB2EFE24A684DD"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
                  -
                    type: ":Number.int64"
                    value: "2"
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "3"
                  -
                    type: ":Number.int64"
                    value: "4"
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":List"
              items:
                -
                  type: ":List"
                  items:
                    -
                      type: ":Number.int64"
                      value: "1"
                    -
                      type: ":Number.int64"
                      value: "3"
                -
                  type: ":List"
                  items:
                    -
                      type: ":Number.int64"
                      value: "1"
                    -
                      type: ":Number.int64"
                      value: "4"
                -
                  type: ":List"
                  items:
                    -
                      type: ":Number.int64"
                      value: "2"
                    -
                      type: ":Number.int64"
                      value: "3"
                -
                  type: ":List"
                  items:
                    -
                      type: ":Number.int64"
                      value: "2"
                    -
                      type: ":Number.int64"
                      value: "4"
```

---

## Test: many-sources-union

This case checks that twenty thousand source references execute and close without recursive iterator nesting.

### Case description

```yaml
gesBlock: case
id: many-sources-union
binaryFixture:
  id: execute-many-sources-union
  resourceId: gesb-v1.execute-many-sources-union
  relativePath: GesbV1/execute-many-sources-union.gesb
  sha256: DAF021BEF930C68492475F27A5735D21527F5084A4468490243979195A627591
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be200200030000000000000000000000,c0000000020006000000000000000000,0c000000000000000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000; UInt16IndexLists are [0], [], [1], and 20000 source registers (0 followed by 19999 copies of 1); update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "DAF021BEF930C68492475F27A5735D21527F5084A4468490243979195A627591"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
              -
                type: ":List"
                items: []
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
```

---

## Test: many-sources-intersect

This case checks that twenty thousand source references execute and close without recursive iterator nesting.

### Case description

```yaml
gesBlock: case
id: many-sources-intersect
binaryFixture:
  id: execute-many-sources-intersect
  resourceId: gesb-v1.execute-many-sources-intersect
  relativePath: GesbV1/execute-many-sources-intersect.gesb
  sha256: 760ED599BC140BE95E69DE1345CEC99F2348611AC3A6A27B166E07E7CEBA7348
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be400200030000000000000000000000,c0000000020006000000000000000000,0c000000000000000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000; UInt16IndexLists are [0], [], [1], and 20000 source registers (all 0); update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "760ED599BC140BE95E69DE1345CEC99F2348611AC3A6A27B166E07E7CEBA7348"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
              -
                type: ":List"
                items: []
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
```

---

## Test: many-sources-difference

This case checks that twenty thousand source references execute and close without recursive iterator nesting.

### Case description

```yaml
gesBlock: case
id: many-sources-difference
binaryFixture:
  id: execute-many-sources-difference
  resourceId: gesb-v1.execute-many-sources-difference
  relativePath: GesbV1/execute-many-sources-difference.gesb
  sha256: A3B224C8DC4BF759106FFD438A8E71A957A06D014AD55234C4163CE2EDE2C2A8
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,20000100020000000000000000000000,20000000010000000000000000000000,be600200030000000000000000000000,c0000000020006000000000000000000,0c000000000000000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000; UInt16IndexLists are [0], [], [1], and 20000 source registers (0 followed by 19999 copies of 1); update counts, lengths and total size."
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: "expect"
binary:
  outcome: "valid"
  rewriteByteExact: true
  rewriteSha256: "A3B224C8DC4BF759106FFD438A8E71A957A06D014AD55234C4163CE2EDE2C2A8"
steps:
  run:
    input:
      args:
        -
          name: "value"
          value:
            type: ":List"
            items:
              -
                type: ":List"
                items:
                  -
                    type: ":Number.int64"
                    value: "1"
              -
                type: ":List"
                items: []
    local:
      -
        name: "Done"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
```
