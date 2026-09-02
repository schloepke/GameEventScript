---
formatVersion: 1
suiteId: program.binary-format
title: Portable .gesb V1 fixture manifest
kind: programBinary
level: atomic
categories: [conformance, program-binary]
---

# Portable `.gesb` V1 fixture manifest

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite is both the portable fixture manifest and its executable contract.
Each case names immutable fixture bytes by resource ID, records their packaged
relative path and SHA-256, and defines the expected read, validation, rewrite,
and—where meaningful—runtime result. Runners receive bytes only from an injected
bounded resolver; paths in this document are packaging metadata, not I/O input.

---

## Test: Canonical runtime program reads, rewrites, and executes

This canonical uncompressed runtime-only fixture must validate, rewrite byte for
byte, load into a Host, and preserve its observable message behavior.

### Case description

```yaml
gesBlock: case
id: valid-runtime
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-valid-runtime
  resourceId: gesb-v1.valid-runtime
  relativePath: GesbV1/valid-runtime.gesb
  sha256: 594808EB171F039AF2182A22360C39183CC4960412EF3D76B97E3632603EECF8
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| execute | Start | completion | |

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 594808EB171F039AF2182A22360C39183CC4960412EF3D76B97E3632603EECF8
  moduleName: BinaryFixture
  requiredRegisterCount: 3
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
steps:
  execute:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "2" }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":integer", value: "3" }
```

---

## Test: Canonical debug program retains all known optional sections

This fully instrumented canonical fixture validates the ordinary writer path
when DebugSymbols, SourceMap, SourceArchive, and BuildMetadata are present.

### Case description

```yaml
gesBlock: case
id: valid-debug
binaryFixture:
  id: gesb-v1-valid-debug
  resourceId: gesb-v1.valid-debug
  relativePath: GesbV1/valid-debug.gesb
  sha256: EAE9416A8C6C3905EEA9C74C6B0DE2CA62F68D2F8165E5EFB1DFA631FEEFC747
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: EAE9416A8C6C3905EEA9C74C6B0DE2CA62F68D2F8165E5EFB1DFA631FEEFC747
  moduleName: BinaryFixture
  requiredRegisterCount: 3
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
```

---

## Test: Unknown optional section survives canonical rewriting

This optional private section is retained as opaque payload under PreserveAll and
does not affect validation of the known runtime sections.

### Case description

```yaml
gesBlock: case
id: valid-opaque
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-valid-opaque
  resourceId: gesb-v1.valid-opaque
  relativePath: GesbV1/valid-opaque.gesb
  sha256: DC5CAB440C3BEB0B64F2F4D1331E30EC8E8464C422339D0CF54577DC048DFA38
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
  derivation: append optional private section 0x8001 version 7
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: DC5CAB440C3BEB0B64F2F4D1331E30EC8E8464C422339D0CF54577DC048DFA38
  moduleName: BinaryFixture
  opaqueSectionCount: 1
```

---

## Test: Noncanonical section order rewrites canonically

This case verifies that the reader accepts a valid reversed section order while the writer restores the
canonical section order rather than preserving known raw section bytes.

### Case description

```yaml
gesBlock: case
id: valid-noncanonical-order
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-valid-noncanonical-order
  resourceId: gesb-v1.valid-noncanonical-order
  relativePath: GesbV1/valid-noncanonical-order.gesb
  sha256: 3CA2DA36927D73959747D3C55278DA9E9A7F8181D8FECC17B4B9AAE5CB887720
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
  derivation: reverse complete section frames without changing payloads
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: false
  rewriteSha256: 594808EB171F039AF2182A22360C39183CC4960412EF3D76B97E3632603EECF8
  moduleName: BinaryFixture
  opaqueSectionCount: 0
```

---

## Test: Invalid magic is a structural read error

This case changes the first magic byte, which must fail before any section is materialized.

### Case description

```yaml
gesBlock: case
id: invalid-magic
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-magic
  resourceId: gesb-v1.invalid-magic
  relativePath: GesbV1/invalid-magic.gesb
  sha256: 5365273EA8403250C9011E8FBDD585EB7432818979327B07D668F9C382AFBFA5
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: replace first GESB magic byte with X
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: readError
  errorCode: InvalidMagic
```

---

## Test: Truncated section payload is a structural read error

This case removes the final payload byte while correcting FileSize and must identify the
section payload boundary rather than escape as a platform exception.

### Case description

```yaml
gesBlock: case
id: invalid-truncated-payload
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-truncated-payload
  resourceId: gesb-v1.invalid-truncated-payload
  relativePath: GesbV1/invalid-truncated-payload.gesb
  sha256: BD3B9D78030AAF4442E5960FB44CB7CFAA4C34D8DFD4061CDF175ECB35476ED6
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: remove final payload byte and update FileSize
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: readError
  errorCode: TruncatedSectionPayload
  byteOffset: 295
  sectionType: 48
```

---

## Test: Missing code section is a structural read error

This Program without its required CodeSegment must be rejected with the stable
required-section error and the missing section ID.

### Case description

```yaml
gesBlock: case
id: invalid-missing-code
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-missing-code
  resourceId: gesb-v1.invalid-missing-code
  relativePath: GesbV1/invalid-missing-code.gesb
  sha256: AB520974D297772E67D9E776E41DFB10998F6421FF7A15EF331F0E7FE8FF8D41
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: remove required CodeSegment
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: readError
  errorCode: MissingRequiredSection
  sectionType: 16
```

---

## Test: Duplicate string section is a structural read error

This case appends a second required StringConstantSegment, which must be rejected at the
duplicate section frame.

### Case description

```yaml
gesBlock: case
id: invalid-duplicate-strings
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-duplicate-strings
  resourceId: gesb-v1.invalid-duplicate-strings
  relativePath: GesbV1/invalid-duplicate-strings.gesb
  sha256: 98862622A6423C46455D3F10CC13771A7C1B113D6AE92EE41489B07E3F7CFFDC
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: append duplicate StringConstantSegment
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: readError
  errorCode: DuplicateSection
  byteOffset: 337
  sectionType: 2
```

---

## Test: Invalid string index is a semantic validation error

This out-of-range BindingSegment string reference must survive framing and fail
the shared semantic Program validator with stable section and entry context.

### Case description

```yaml
gesBlock: case
id: invalid-string-index
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-string-index
  resourceId: gesb-v1.invalid-string-index
  relativePath: GesbV1/invalid-string-index.gesb
  sha256: A76D94C4B131B232E675E3EAE00B0D19037F630E89EEB45AC024C4E925C3930C
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: replace first BindingSegment name index with 0xFFFE
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidStringIndex
  sectionType: 4
  entryIndex: 0
```

---

## Test: Invalid UTF-8 is a structural read error

This invalid byte in a length-delimited string constant must fail strict UTF-8
decoding at a deterministic byte offset.

### Case description

```yaml
gesBlock: case
id: invalid-utf8
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-utf8
  resourceId: gesb-v1.invalid-utf8
  relativePath: GesbV1/invalid-utf8.gesb
  sha256: 3A210F83E68AB4504867D697CFF1783A513C417CF8CD6CC79C87BEE6256B7FD4
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: replace first module-name UTF-8 byte with 0xFF
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: readError
  errorCode: InvalidUtf8
  byteOffset: 64
  sectionType: 2
  entryIndex: 0
```

---

## Test: Unknown required section is a structural read error

This case ensures an implementation never skips an unknown section marked Required, even in
the private section-ID range.

### Case description

```yaml
gesBlock: case
id: invalid-unknown-required
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-unknown-required
  resourceId: gesb-v1.invalid-unknown-required
  relativePath: GesbV1/invalid-unknown-required.gesb
  sha256: FA42491DC0BF41CCFEB4589D77BABA483773979CA3143195630FBA453F72A17B
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: append private section 0x8000 with Required flag
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: readError
  errorCode: UnknownRequiredSection
  byteOffset: 337
  sectionType: 32768
```

---

## Test: Compression on a required V1 section is a structural read error

This V1 case reserves compression codec bits but requires every known runtime section to
remain uncompressed.

### Case description

```yaml
gesBlock: case
id: invalid-required-compression
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-required-compression
  resourceId: gesb-v1.invalid-required-compression
  relativePath: GesbV1/invalid-required-compression.gesb
  sha256: 6C2BB4AD7D4769901AE054FC3DF1200499016156E5ED3C87D19C05957DE46D4D
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: set reserved compression codec 1 on ProgramMetadataSegment
```

### Source code under test

```ges
module BinaryFixture

on Start(value) {
  let result be value + 1
  emit Done(value: result)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: readError
  errorCode: UnsupportedCompression
  byteOffset: 18
  sectionType: 1
```

---

## Test: Indirect cyclic call graph is a semantic validation error

This fixture starts from an acyclic `Start -> helper` Program and patches the
helper return into `helper -> Start`. This graph cannot be emitted from valid
source, but every untrusted `.gesb` reader must reject it before Host linking.

### Case description

```yaml
gesBlock: case
id: invalid-indirect-call-cycle
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-indirect-call-cycle
  resourceId: gesb-v1.invalid-indirect-call-cycle
  relativePath: GesbV1/invalid-indirect-call-cycle.gesb
  sha256: 9C4AC99A2EBA6637F818ED37D4E9ABD8C502A76A050C8D0DBC81E8188C048580
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 7
  derivation: replace helper ReturnValue with Call to Start entry address
```

### Source code under test

```ges
module IndirectCycleFixture

function helper() be 1

on Start {
  let ignored be helper()
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: CyclicCallGraph
```
