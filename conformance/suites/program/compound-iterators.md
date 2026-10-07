---
formatVersion: 1
suiteId: program.compound-iterators
title: Compound iterator transport
kind: programBinary
level: atomic
categories: [conformance, program-binary]
---

# Compound iterator transport

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

Binary-only validation and canonical rewrite cases. Execution is tested separately.

---

## Test: mode-0

This fixture checks mode-0 at the transport boundary.

### Case description

```yaml
gesBlock: case
id: mode-0
binaryFixture:
  id: compound-mode-0
  resourceId: gesb-v1.compound-mode-0
  relativePath: GesbV1/compound-mode-0.gesb
  sha256: 119934D534C7CF26E523FE057E4C18591DFE6B9FBDB8A89514405D1C640AE2A5
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,be000200000000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: 119934D534C7CF26E523FE057E4C18591DFE6B9FBDB8A89514405D1C640AE2A5
```

---

## Test: mode-1

This fixture checks mode-1 at the transport boundary.

### Case description

```yaml
gesBlock: case
id: mode-1
binaryFixture:
  id: compound-mode-1
  resourceId: gesb-v1.compound-mode-1
  relativePath: GesbV1/compound-mode-1.gesb
  sha256: 6849A3F9B26B55B57DC1F86B4EDEF337410902CAFD55336CE7717460FCD5B574
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,be200200030000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: 6849A3F9B26B55B57DC1F86B4EDEF337410902CAFD55336CE7717460FCD5B574
```

---

## Test: mode-2

This fixture checks mode-2 at the transport boundary.

### Case description

```yaml
gesBlock: case
id: mode-2
binaryFixture:
  id: compound-mode-2
  resourceId: gesb-v1.compound-mode-2
  relativePath: GesbV1/compound-mode-2.gesb
  sha256: E798BAECE55958802BCAC27C7C4E4AF5AA586D5B898CBA16CB18CB0A9CA26E87
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,be400200030000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: E798BAECE55958802BCAC27C7C4E4AF5AA586D5B898CBA16CB18CB0A9CA26E87
```

---

## Test: mode-3

This fixture checks mode-3 at the transport boundary.

### Case description

```yaml
gesBlock: case
id: mode-3
binaryFixture:
  id: compound-mode-3
  resourceId: gesb-v1.compound-mode-3
  relativePath: GesbV1/compound-mode-3.gesb
  sha256: D6C93172972D061C538DA9DE12E83840CEDABD6DDA9174CF623BAB0910B86CC2
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,be600200030000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: D6C93172972D061C538DA9DE12E83840CEDABD6DDA9174CF623BAB0910B86CC2
```

---

## Test: mode-4

This fixture checks mode-4 at the transport boundary.

### Case description

```yaml
gesBlock: case
id: mode-4
binaryFixture:
  id: compound-mode-4
  resourceId: gesb-v1.compound-mode-4
  relativePath: GesbV1/compound-mode-4.gesb
  sha256: A8315DC537B51565BDC5A76149046CFA2644FB1E00605067186E616439F227DE
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,be800200030000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: A8315DC537B51565BDC5A76149046CFA2644FB1E00605067186E616439F227DE
```

---

## Test: mode-5

This fixture checks mode-5 at the transport boundary.

### Case description

```yaml
gesBlock: case
id: mode-5
binaryFixture:
  id: compound-mode-5
  resourceId: gesb-v1.compound-mode-5
  relativePath: GesbV1/compound-mode-5.gesb
  sha256: 7BD2A31CF1873D2D2E415178EEE042B3424F4BA8109B44A10257E5654C68FCFC
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200030000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: 7BD2A31CF1873D2D2E415178EEE042B3424F4BA8109B44A10257E5654C68FCFC
```

---

## Test: mode-6

This fixture checks mode-6 at the transport boundary.

### Case description

```yaml
gesBlock: case
id: mode-6
binaryFixture:
  id: compound-mode-6
  resourceId: gesb-v1.compound-mode-6
  relativePath: GesbV1/compound-mode-6.gesb
  sha256: B41170427BAA772AA9217CC95A49811AC0FF503D12C87012CD32B58C14E687CF
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bec00200000000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: B41170427BAA772AA9217CC95A49811AC0FF503D12C87012CD32B58C14E687CF
```

---

## Test: reserved-mode

This fixture checks reserved-mode at the transport boundary.

### Case description

```yaml
gesBlock: case
id: reserved-mode
binaryFixture:
  id: compound-reserved-mode
  resourceId: gesb-v1.compound-reserved-mode
  relativePath: GesbV1/compound-reserved-mode.gesb
  sha256: 2D906BBE0635877A017DA260B8956F73CCB8B5A543513BA9385BC4414188189C
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bee00200030000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: unit

This fixture checks unit at the transport boundary.

### Case description

```yaml
gesBlock: case
id: unit
binaryFixture:
  id: compound-unit
  resourceId: gesb-v1.compound-unit
  relativePath: GesbV1/compound-unit.gesb
  sha256: C08BE1F2DCE672853E66F9C94448981CEC5223E341C12E9879A031808F0E5788
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea10200030000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: payload

This fixture checks payload at the transport boundary.

### Case description

```yaml
gesBlock: case
id: payload
binaryFixture:
  id: compound-payload
  resourceId: gesb-v1.compound-payload
  relativePath: GesbV1/compound-payload.gesb
  sha256: D0BC0B48F9EA910A0AE6F536217F057996C0FBC17380342134D7B8BBF1074895
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200030000000100000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: missing-sources

This fixture checks missing-sources at the transport boundary.

### Case description

```yaml
gesBlock: case
id: missing-sources
binaryFixture:
  id: compound-missing-sources
  resourceId: gesb-v1.compound-missing-sources
  relativePath: GesbV1/compound-missing-sources.gesb
  sha256: EC117E20FB6CFE35CF9BA64ADE1F4BD5520CD1A55ACA0C0BEA1E24299C8BC1F6
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200630000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  errorCode: InvalidListIndex
```

---

## Test: source-arity

This fixture checks source-arity at the transport boundary.

### Case description

```yaml
gesBlock: case
id: source-arity
binaryFixture:
  id: compound-source-arity
  resourceId: gesb-v1.compound-source-arity
  relativePath: GesbV1/compound-source-arity.gesb
  sha256: C12B31896B23875AAB3ED5F87462C69CF172AD084E20B6A06181BE079112E5E6
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200000000000000000000000000,c0200400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: empty-targets

This fixture checks empty-targets at the transport boundary.

### Case description

```yaml
gesBlock: case
id: empty-targets
binaryFixture:
  id: compound-empty-targets
  resourceId: gesb-v1.compound-empty-targets
  relativePath: GesbV1/compound-empty-targets.gesb
  sha256: 5999BE232F3D780509C83AEA9F5BC5A27D0719AB74E0606C908D2C443B824C56
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200030000000000000000000000,c0200800020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: duplicate-targets

This fixture checks duplicate-targets at the transport boundary.

### Case description

```yaml
gesBlock: case
id: duplicate-targets
binaryFixture:
  id: compound-duplicate-targets
  resourceId: gesb-v1.compound-duplicate-targets
  relativePath: GesbV1/compound-duplicate-targets.gesb
  sha256: BF9202E4695AA455CE7BA72F039DD392006B95BC803209D816D9CAD52F94B7AC
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200030000000000000000000000,c0200500020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: iterator-target

This fixture checks iterator-target at the transport boundary.

### Case description

```yaml
gesBlock: case
id: iterator-target
binaryFixture:
  id: compound-iterator-target
  resourceId: gesb-v1.compound-iterator-target
  relativePath: GesbV1/compound-iterator-target.gesb
  sha256: 13F9E621CD81D5C858DA853037B6502D62B5F981819DA5A1FA723403D3405726
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200030000000000000000000000,c0200600020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: frame-target

This fixture checks frame-target at the transport boundary.

### Case description

```yaml
gesBlock: case
id: frame-target
binaryFixture:
  id: compound-frame-target
  resourceId: gesb-v1.compound-frame-target
  relativePath: GesbV1/compound-frame-target.gesb
  sha256: 2A7688267B74D7FB6C55A25BCDC6F3DFA0B2093CEE917981C45CFCEBC33E89AF
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200030000000000000000000000,c0200700020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: next-reserved-flag

This fixture checks next-reserved-flag at the transport boundary.

### Case description

```yaml
gesBlock: case
id: next-reserved-flag
binaryFixture:
  id: compound-next-reserved-flag
  resourceId: gesb-v1.compound-next-reserved-flag
  relativePath: GesbV1/compound-next-reserved-flag.gesb
  sha256: D3F25EC785347D9F20E7504051EE20C3C0192E63B790F88FA7DF445925418EE2
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,bea00200030000000000000000000000,c0400400020004000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: source-resource

This fixture checks source-resource at the transport boundary.

### Case description

```yaml
gesBlock: case
id: source-resource
binaryFixture:
  id: compound-source-resource
  resourceId: gesb-v1.compound-source-resource
  relativePath: GesbV1/compound-source-resource.gesb
  sha256: 20A5476644358051AFE47453150C543228D65301048D4C599C34A172E0DAE3C3
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,be000000000000000000000000000000,bea00200030000000000000000000000,c1000000020000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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

## Test: cartesian

This fixture checks cartesian at the transport boundary.

### Case description

```yaml
gesBlock: case
id: cartesian
binaryFixture:
  id: compound-cartesian
  resourceId: gesb-v1.compound-cartesian
  relativePath: GesbV1/compound-cartesian.gesb
  sha256: 29E1E39A112B0318529E2014CF93228C00594B60F799B7B6089377F5E5879A5E
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,e0000100000000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
  rewriteSha256: 29E1E39A112B0318529E2014CF93228C00594B60F799B7B6089377F5E5879A5E
```

---

## Test: cartesian-flags

This fixture checks cartesian-flags at the transport boundary.

### Case description

```yaml
gesBlock: case
id: cartesian-flags
binaryFixture:
  id: compound-cartesian-flags
  resourceId: gesb-v1.compound-cartesian-flags
  relativePath: GesbV1/compound-cartesian-flags.gesb
  sha256: 684FD28355BD03747CEA3DD0987BE96CCC90B5FAC7C24DA94359856AE2A082CD
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace valid-runtime.gesb Code with words 01000000020000000000000000000000,e0200100000000000000000000000000,0a000000000000000000000000000000 and UInt16IndexLists payload with 090000000100000000000000000001000000010002000000000000000200000000000100020000000000000001000000020001000000030000000000; update counts, lengths and total size."
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
