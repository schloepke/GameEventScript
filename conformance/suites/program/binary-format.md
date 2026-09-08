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
  sha256: DE88FAF472D5283E908DAD35BEAC07F6243B265F7DD4DA66B0B0D012A78D0FBC
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
```

### Source code under test

```ges
module binaryfixture

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
  rewriteSha256: DE88FAF472D5283E908DAD35BEAC07F6243B265F7DD4DA66B0B0D012A78D0FBC
  moduleName: binaryfixture
  requiredRegisterCount: 3
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
steps:
  execute:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "2" }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":Number.int64", value: "3" }
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
  sha256: 726C63C9A199CDAA4C1F29197AF01A786B3A8DC31400544F7054244969F221A1
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
```

### Source code under test

```ges
module binaryfixture

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
  rewriteSha256: 726C63C9A199CDAA4C1F29197AF01A786B3A8DC31400544F7054244969F221A1
  moduleName: binaryfixture
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
  sha256: C46CAC9FB7FE0E8ED034EF90822B6E22A92CDE30FD7A411A7827581F9CE3E79C
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
  derivation: append optional private section 0x8001 version 7
```

### Source code under test

```ges
module binaryfixture

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
  rewriteSha256: C46CAC9FB7FE0E8ED034EF90822B6E22A92CDE30FD7A411A7827581F9CE3E79C
  moduleName: binaryfixture
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
  sha256: 16E2013841ACA210C8FAAFCFD2275F1EA53DA83DB0575B0B2E5DDD73A11C31B8
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
  derivation: reverse complete section frames without changing payloads
```

### Source code under test

```ges
module binaryfixture

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
  rewriteSha256: DE88FAF472D5283E908DAD35BEAC07F6243B265F7DD4DA66B0B0D012A78D0FBC
  moduleName: binaryfixture
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
  sha256: 9A91C8994B76EB30F9F7A28ECCBB8AECE0743E66CC01BF7DCFB4DD8C32FDE5C3
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: replace first GESB magic byte with X
```

### Source code under test

```ges
module binaryfixture

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
  sha256: C31DCC0F1B9E0C5E9F2C9542FBD4399CA086C87EEC8129FC4DE343217C1007AA
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: remove final payload byte and update FileSize
```

### Source code under test

```ges
module binaryfixture

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
  sha256: 1BC9441D55862BB987FE8FD865B840CAFBC004FF15847A646D8B77FC479AA11B
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: remove required CodeSegment
```

### Source code under test

```ges
module binaryfixture

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
  sha256: BD9CE480E3EE62CA499FB83972FF2F9C4FA2B37128C4E110FE6A9957BE269C7F
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: append duplicate StringConstantSegment
```

### Source code under test

```ges
module binaryfixture

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
  sha256: C94B3058AB01921A1A49A145187A1FAA6EB79DE3CE4B9CF767D650E05CB7CEED
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: replace first BindingSegment name index with 0xFFFE
```

### Source code under test

```ges
module binaryfixture

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
  sha256: 21B63F770343B171B65320BA233981FB15631694B0D0131B37F60AE9ED596C84
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: replace first module-name UTF-8 byte with 0xFF
```

### Source code under test

```ges
module binaryfixture

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
  byteOffset: 90
  sectionType: 2
  entryIndex: 3
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
  sha256: 4B8654D2CAFEE27985526BE93E2DE12DACE08BBB3E209DB83D8A32AE6C338D77
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: append private section 0x8000 with Required flag
```

### Source code under test

```ges
module binaryfixture

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
  sha256: DD6AA897B289707914851EFB609A61B2B5C083F1B109D88FD661C70C64115163
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: set reserved compression codec 1 on ProgramMetadataSegment
```

### Source code under test

```ges
module binaryfixture

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
  sha256: 35521038A0B3F48168AE93FBFBA0FE6F09E388EBC63BA3D8A4BDDD35037BA034
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 7
  derivation: replace helper ReturnValue with Call to Start entry address
```

### Source code under test

```ges
module indirectcyclefixture

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

---

## Test: valid-direct-call

This case preserves the compiled resource requirements of 5 registers and call depth 1, including live caller frames.

### Case description

```yaml
gesBlock: case
id: valid-direct-call
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-valid-direct-call
  resourceId: gesb-v1.valid-direct-call
  relativePath: GesbV1/valid-direct-call.gesb
  sha256: 55A73F1384849C446D560603DD03CCFEEF62329B1FE63CC7E55101E26A833903
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
```

### Source code under test

```ges
module resources
function add(_ value) be value + 1
on Start(value) { emit Done(value: add(value)) }
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
  rewriteSha256: 55A73F1384849C446D560603DD03CCFEEF62329B1FE63CC7E55101E26A833903
  moduleName: resources
  requiredRegisterCount: 5
  requiredCallStackDepth: 1
  opaqueSectionCount: 0
steps:
  execute:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "2" }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":Number.int64", value: "3" }
```

---

## Test: underdeclared-depth-direct-call

This case lowers the declared call depth in both the Program metadata and its handler. The declarations still agree with each other, but contradict the unchanged executable code. The reader must reject this Program before execution.

### Case description

```yaml
gesBlock: case
id: underdeclared-depth-direct-call
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-underdeclared-depth-direct-call
  resourceId: gesb-v1.underdeclared-depth-direct-call
  relativePath: GesbV1/underdeclared-depth-direct-call.gesb
  sha256: 4B8758F404BD571E65F5D99F9A9C46B144E69964CA7C557AE63EAC4B2CD3DE04
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: patch RequiredCallStackDepth in ProgramMetadata and handler binding 0 to 0 at file byte offsets 32 and 181 (u16 Little Endian); retain all code and other bytes
```

### Source code under test

```ges
module resources
function add(_ value) be value + 1
on Start(value) { emit Done(value: add(value)) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: underdeclared-registers-direct-call

This case lowers the declared register requirement in both the Program metadata and its handler. The declarations still agree with each other, but contradict the unchanged executable code. The reader must reject this Program before execution.

### Case description

```yaml
gesBlock: case
id: underdeclared-registers-direct-call
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-underdeclared-registers-direct-call
  resourceId: gesb-v1.underdeclared-registers-direct-call
  relativePath: GesbV1/underdeclared-registers-direct-call.gesb
  sha256: A543CE814AF066B4E991D262C8805807D6CE3FF9FF9D0833A88770DC287B687E
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: patch RequiredRegisterCount in ProgramMetadata and handler binding 0 to 3 at file byte offsets 30 and 179 (u16 Little Endian); retain all code and other bytes
```

### Source code under test

```ges
module resources
function add(_ value) be value + 1
on Start(value) { emit Done(value: add(value)) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: valid-nested-call

This case preserves the compiled resource requirements of 9 registers and call depth 2, including live caller frames.

### Case description

```yaml
gesBlock: case
id: valid-nested-call
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-valid-nested-call
  resourceId: gesb-v1.valid-nested-call
  relativePath: GesbV1/valid-nested-call.gesb
  sha256: 64AC5E356FFFAD78F56A09B173B8B538B830546E6C0FB20CAC85110A336D5A2D
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: true
```

### Source code under test

```ges
module resources
function add(_ value) be value + 1
function twice(_ value) be add(value) + add(value)
on Start(value) { emit Done(value: twice(value)) }
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
  rewriteSha256: 64AC5E356FFFAD78F56A09B173B8B538B830546E6C0FB20CAC85110A336D5A2D
  moduleName: resources
  requiredRegisterCount: 9
  requiredCallStackDepth: 2
  opaqueSectionCount: 0
steps:
  execute:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "2" }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":Number.int64", value: "6" }
```

---

## Test: underdeclared-depth-nested-call

This case lowers the declared call depth in both the Program metadata and its handler. The declarations still agree with each other, but contradict the unchanged executable code. The reader must reject this Program before execution.

### Case description

```yaml
gesBlock: case
id: underdeclared-depth-nested-call
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-underdeclared-depth-nested-call
  resourceId: gesb-v1.underdeclared-depth-nested-call
  relativePath: GesbV1/underdeclared-depth-nested-call.gesb
  sha256: 2E8CAFA6A91AA09A0B241599038D86651097ACCD9679FC167126FE820744F0C5
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: patch RequiredCallStackDepth in ProgramMetadata and handler binding 0 to 1 at file byte offsets 32 and 190 (u16 Little Endian); retain all code and other bytes
```

### Source code under test

```ges
module resources
function add(_ value) be value + 1
function twice(_ value) be add(value) + add(value)
on Start(value) { emit Done(value: twice(value)) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: underdeclared-registers-nested-call

This case lowers the declared register requirement in both the Program metadata and its handler. The declarations still agree with each other, but contradict the unchanged executable code. The reader must reject this Program before execution.

### Case description

```yaml
gesBlock: case
id: underdeclared-registers-nested-call
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-underdeclared-registers-nested-call
  resourceId: gesb-v1.underdeclared-registers-nested-call
  relativePath: GesbV1/underdeclared-registers-nested-call.gesb
  sha256: 5F4645897C5E4158815C5CA20397D048ED460241A352E97D53D7D5D58EED8B3E
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  derivation: patch RequiredRegisterCount in ProgramMetadata and handler binding 0 to 4 at file byte offsets 30 and 188 (u16 Little Endian); retain all code and other bytes
```

### Source code under test

```ges
module resources
function add(_ value) be value + 1
function twice(_ value) be add(value) + add(value)
on Start(value) { emit Done(value: twice(value)) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```
