---
formatVersion: 1
suiteId: program.internal-values
title: Internal iterator and builder boundaries
kind: programBinary
level: atomic
categories: [conformance, program-binary]
---

# Internal iterator and builder boundaries

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

Internal mutable work values must not escape through ordinary data operands.
These fixtures exercise the reader and canonical writer without executing
untrusted internal values. Valid controls preserve ordinary iterator/builder use.

---

## Test: iterator-escape

This fixture verifies iterator-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: iterator-escape
binaryFixture:
  id: internal-iterator-escape
  resourceId: gesb-v1.internal-iterator-escape
  relativePath: GesbV1/internal-iterator-escape.gesb
  sha256: C662B69CD93FB5035B83AFCE59F934243E09672E8B05C88A9CE4ADE2767256F9
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,3f000100010002000100000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: list-builder-escape

This fixture verifies list-builder-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: list-builder-escape
binaryFixture:
  id: internal-list-builder-escape
  resourceId: gesb-v1.internal-list-builder-escape
  relativePath: GesbV1/internal-list-builder-escape.gesb
  sha256: 38064E888E2307B8E6B6EC875A4EE19F2379A32D2984343C8A45CE84489795BA
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000100000000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: map-builder-escape

This fixture verifies map-builder-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: map-builder-escape
binaryFixture:
  id: internal-map-builder-escape
  resourceId: gesb-v1.internal-map-builder-escape
  relativePath: GesbV1/internal-map-builder-escape.gesb
  sha256: BAD2D39482A808C52371FB98BC640E55EE21CFC93CB40BF92AEECEAFF0570B27
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,ca000100000000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: distinct-builder-escape

This fixture verifies distinct-builder-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: distinct-builder-escape
binaryFixture:
  id: internal-distinct-builder-escape
  resourceId: gesb-v1.internal-distinct-builder-escape
  relativePath: GesbV1/internal-distinct-builder-escape.gesb
  sha256: FB82226189AAAC9F26BC827F9E62D932094F2DAD7018C37070149223E7FD3AFA
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,cd000100000000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: group-builder-escape

This fixture verifies group-builder-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: group-builder-escape
binaryFixture:
  id: internal-group-builder-escape
  resourceId: gesb-v1.internal-group-builder-escape
  relativePath: GesbV1/internal-group-builder-escape.gesb
  sha256: A5AF28CF9951CD5BD882C0F4C65C909AC00E0F095B3EA0E7D3D020619A92C9F5
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,d0000100000000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: order-builder-escape

This fixture verifies order-builder-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: order-builder-escape
binaryFixture:
  id: internal-order-builder-escape
  resourceId: gesb-v1.internal-order-builder-escape
  relativePath: GesbV1/internal-order-builder-escape.gesb
  sha256: DFF186647DFEBDB10695742DD8FD4EBF04CDE4AFD488DAE266340FF55A5DCA46
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,d3000100000000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: builder-copy

This fixture verifies builder-copy at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-copy
binaryFixture:
  id: internal-builder-copy
  resourceId: gesb-v1.internal-builder-copy
  relativePath: GesbV1/internal-builder-copy.gesb
  sha256: B8D58354E394EE3482943BC2F65CAEB1D750412DF94AB0639254891C7102F3C5
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000200000000000000000000000000,1e000100020000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: builder-staging

This fixture verifies builder-staging at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-staging
binaryFixture:
  id: internal-builder-staging
  resourceId: gesb-v1.internal-builder-staging
  relativePath: GesbV1/internal-builder-staging.gesb
  sha256: CDE843D98CFD036A06D5B392F30D87DC7758C59034C94B388FC07524D9FC4FD0
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000200000000000000000000000000,2d000000020000000000000000000000,39000100000000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: builder-branch-escape

This fixture verifies builder-branch-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-branch-escape
binaryFixture:
  id: internal-builder-branch-escape
  resourceId: gesb-v1.internal-builder-branch-escape
  relativePath: GesbV1/internal-builder-branch-escape.gesb
  sha256: 1D0F87F53AD60DE155D1516AED1751FA5612B0C1C4E9E1B468C10AD4E7BD27C9
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000100000000000000000000000000,03000000000004000000000000000000,23000100000000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: builder-wrong-family

This fixture verifies builder-wrong-family at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-wrong-family
binaryFixture:
  id: internal-builder-wrong-family
  resourceId: gesb-v1.internal-builder-wrong-family
  relativePath: GesbV1/internal-builder-wrong-family.gesb
  sha256: AC3866EDB922B7FF77A153CD6AB29DC63D54ED74B96E2AA952E02BE76140200A
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000200000000000000000000000000,cb000000020000000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: builder-overwritten

This fixture verifies builder-overwritten at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-overwritten
binaryFixture:
  id: internal-builder-overwritten
  resourceId: gesb-v1.internal-builder-overwritten
  relativePath: GesbV1/internal-builder-overwritten.gesb
  sha256: 9596E837EC1EE57A913A787B8E5830D465CF84836EC471BFE2FB8C4DF7B5CD2D
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000100000000000000000000000000,26000100000000000700000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 9596E837EC1EE57A913A787B8E5830D465CF84836EC471BFE2FB8C4DF7B5CD2D
```

---

## Test: builder-finished

This fixture verifies builder-finished at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-finished
binaryFixture:
  id: internal-builder-finished
  resourceId: gesb-v1.internal-builder-finished
  relativePath: GesbV1/internal-builder-finished.gesb
  sha256: 6AC3985CA45AE9C99E07322E58253191E877AA3324B9C578B3F8277BFBA22D76
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000200000000000000000000000000,c8000000020000000000000000000000,c9000100020000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 6AC3985CA45AE9C99E07322E58253191E877AA3324B9C578B3F8277BFBA22D76
```

---

## Test: iterator-counted

This fixture verifies iterator-counted at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: iterator-counted
binaryFixture:
  id: internal-iterator-counted
  resourceId: gesb-v1.internal-iterator-counted
  relativePath: GesbV1/internal-iterator-counted.gesb
  sha256: 0A30C532AF77FAA54342381025033625DE7ACA0506F31D552D3E0160C3FDF95F
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,3f000200010002000100000000000000,ac000100020000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 0A30C532AF77FAA54342381025033625DE7ACA0506F31D552D3E0160C3FDF95F
```

---

## Test: builder-loop-escape

This fixture verifies builder-loop-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-loop-escape
binaryFixture:
  id: internal-builder-loop-escape
  resourceId: gesb-v1.internal-builder-loop-escape
  relativePath: GesbV1/internal-builder-loop-escape.gesb
  sha256: 477178F7C8751FAD2D37D25A541E7E9BC5962DD6565EFDEBC6926552B5162BC1
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000100000000000000000000000000,03000000000004000000000000000000,02000000000002000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: builder-self-insertion

This fixture verifies builder-self-insertion at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-self-insertion
binaryFixture:
  id: internal-builder-self-insertion
  resourceId: gesb-v1.internal-builder-self-insertion
  relativePath: GesbV1/internal-builder-self-insertion.gesb
  sha256: E1F01700D03995A62FDD3369EE733D3C486E1E71748A961D29D4C6D488E14596
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000200000000000000000000000000,c8000000020002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: builder-return

This fixture verifies builder-return at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: builder-return
binaryFixture:
  id: internal-builder-return
  resourceId: gesb-v1.internal-builder-return
  relativePath: GesbV1/internal-builder-return.gesb
  sha256: E30D9531728F673B1793D11B757EAC49DD4E95DDD6847BB72BEC6771EC10D36D
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000100000000000000000000000000,0b000000010000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```

---

## Test: released-register

This fixture verifies released-register at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: released-register
binaryFixture:
  id: internal-released-register
  resourceId: gesb-v1.internal-released-register
  relativePath: GesbV1/internal-released-register.gesb
  sha256: 4C5A4E2B451CBBCC8EFA53B876F21C4F4985EDBF0482FFF22B9487D469BDCD08
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,c7000200000000000000000000000000,01000000ffff00000000000000000000,01000000010000000000000000000000,1e000100020000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 4C5A4E2B451CBBCC8EFA53B876F21C4F4985EDBF0482FFF22B9487D469BDCD08
```

---

## Test: closed-iterator

This fixture verifies closed-iterator at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: closed-iterator
binaryFixture:
  id: internal-closed-iterator
  resourceId: gesb-v1.internal-closed-iterator
  relativePath: GesbV1/internal-closed-iterator.gesb
  sha256: AAE93E448B6161A7B229158A9C8797D7127F2897DED340FA2BCAB9A9F673E7D8
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,3f000100010002000100000000000000,c1000000010000000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: AAE93E448B6161A7B229158A9C8797D7127F2897DED340FA2BCAB9A9F673E7D8
```

---

## Test: iterator-branch-escape

This fixture verifies iterator-branch-escape at the binary validation boundary.

### Case description

```yaml
gesBlock: case
id: iterator-branch-escape
binaryFixture:
  id: internal-iterator-branch-escape
  resourceId: gesb-v1.internal-iterator-branch-escape
  relativePath: GesbV1/internal-iterator-branch-escape.gesb
  sha256: AA77F771A7948EA3E8FA28671D77BD7118F35071974A2A227D196E276BFD82ED
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the Code section of valid-runtime.gesb with words 01000000020000000000000000000000,bf000100000003000000000000000000,02000000000003000000000000000000,0c000000000002000000000000000000,0a000000000000000000000000000000; update instruction count, payload length and file size."
```

### Source code under test

```ges
module binaryfixture
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
```
