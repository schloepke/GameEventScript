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
  derivation: "append optional private section 0x8001 version 7"
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
  derivation: "reverse complete section frames without changing payloads"
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
  derivation: "replace first GESB magic byte with X"
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
  derivation: "remove final payload byte and update FileSize"
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
  derivation: "remove required CodeSegment"
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
  derivation: "append duplicate StringConstantSegment"
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
  derivation: "replace first BindingSegment name index with 0xFFFE"
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
  derivation: "replace first module-name UTF-8 byte with 0xFF"
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
  derivation: "append private section 0x8000 with Required flag"
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
  derivation: "set reserved compression codec 1 on ProgramMetadataSegment"
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
  derivation: "replace helper ReturnValue with Call to Start entry address"
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
  derivation: "patch RequiredCallStackDepth in ProgramMetadata and handler binding 0 to 0 at file byte offsets 32 and 181 (u16 Little Endian); retain all code and other bytes"
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
  derivation: "patch RequiredRegisterCount in ProgramMetadata and handler binding 0 to 3 at file byte offsets 30 and 179 (u16 Little Endian); retain all code and other bytes"
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
  derivation: "patch RequiredCallStackDepth in ProgramMetadata and handler binding 0 to 1 at file byte offsets 32 and 190 (u16 Little Endian); retain all code and other bytes"
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
  derivation: "patch RequiredRegisterCount in ProgramMetadata and handler binding 0 to 4 at file byte offsets 30 and 188 (u16 Little Endian); retain all code and other bytes"
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

## Test: valid-resource-staging

This case validates the resource contract independently of the stored declarations. Canonical compiler output; code is retained unchanged by its underdeclaration variants.

### Case description

```yaml
gesBlock: case
id: valid-resource-staging
binaryFixture:
  id: gesb-v1-valid-resource-staging
  resourceId: gesb-v1.valid-resource-staging
  relativePath: GesbV1/valid-resource-staging.gesb
  sha256: 9ADB846D67388DD07ECFD27E5E1B5B6404FA73DB02BFAA24A35491ADDA9E0A2B
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Canonical compiler output; code is retained unchanged by its underdeclaration variants."
```

### Source code under test

```ges
module resourceproof
on Start { let values be [1, 2, 3]
 emit Done(value: values) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 9ADB846D67388DD07ECFD27E5E1B5B6404FA73DB02BFAA24A35491ADDA9E0A2B
  moduleName: resourceproof
  requiredRegisterCount: 4
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
```

---

## Test: underdeclared-resource-staging-registers

This case validates the resource contract independently of the stored declarations. Reduce only the selected handler register requirement by one and set the program requirement to the same value; retain every instruction.

### Case description

```yaml
gesBlock: case
id: underdeclared-resource-staging-registers
binaryFixture:
  id: gesb-v1-underdeclared-resource-staging-registers
  resourceId: gesb-v1.underdeclared-resource-staging-registers
  relativePath: GesbV1/underdeclared-resource-staging-registers.gesb
  sha256: 644BE694E2EB1D05AF248D2DC0739D2093318949852CA514CC5B6E89A803787B
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Reduce only the selected handler register requirement by one and set the program requirement to the same value; retain every instruction."
```

### Source code under test

```ges
module resourceproof
on Start { let values be [1, 2, 3]
 emit Done(value: values) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: invalid-resource-interrupted-stage

This case validates the resource contract independently of the stored declarations. Replace the middle StageInteger of the three-value sequence with Nop; keep all resource declarations.

### Case description

```yaml
gesBlock: case
id: invalid-resource-interrupted-stage
binaryFixture:
  id: gesb-v1-invalid-resource-interrupted-stage
  resourceId: gesb-v1.invalid-resource-interrupted-stage
  relativePath: GesbV1/invalid-resource-interrupted-stage.gesb
  sha256: F80EEE903232D7B16DFBEB5967C2F42A6834333931C7430DF29C91B2C7FE0DB4
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the middle StageInteger of the three-value sequence with Nop; keep all resource declarations."
```

### Source code under test

```ges
module resourceproof
on Start { let values be [1, 2, 3]
 emit Done(value: values) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: invalid-resource-growing-loop

This case validates the resource contract independently of the stored declarations. Replace the final ReturnVoid with Jump to the entry prolog. Each iteration would allocate the frame again.

### Case description

```yaml
gesBlock: case
id: invalid-resource-growing-loop
binaryFixture:
  id: gesb-v1-invalid-resource-growing-loop
  resourceId: gesb-v1.invalid-resource-growing-loop
  relativePath: GesbV1/invalid-resource-growing-loop.gesb
  sha256: B1E0C0E33CCA5EB56047BBDE6BA74B5907FB707D027040EDB02122BCE701E516
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Replace the final ReturnVoid with Jump to the entry prolog. Each iteration would allocate the frame again."
```

### Source code under test

```ges
module resourceproof
on Start { let values be [1, 2, 3]
 emit Done(value: values) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: valid-resource-balanced-loop

This case validates the resource contract independently of the stored declarations. Direct V1 control-flow fixture: reserve one root local, reserve/release one iteration local, then return to the unchanged one-local loop header. Peak is two registers, depth zero. This validation fixture is not executed.

### Case description

```yaml
gesBlock: case
id: valid-resource-balanced-loop
binaryFixture:
  id: gesb-v1-valid-resource-balanced-loop
  resourceId: gesb-v1.valid-resource-balanced-loop
  relativePath: GesbV1/valid-resource-balanced-loop.gesb
  sha256: 81CCBEF01CFB24565EC06A4CD9CCF2489A058CB49B3C6C46BA794EFB75F5EC0A
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Direct V1 control-flow fixture: reserve one root local, reserve/release one iteration local, then return to the unchanged one-local loop header. Peak is two registers, depth zero. This validation fixture is not executed."
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 81CCBEF01CFB24565EC06A4CD9CCF2489A058CB49B3C6C46BA794EFB75F5EC0A
  moduleName: resourceproof
  requiredRegisterCount: 2
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
```

---

## Test: valid-resource-record-create

This case validates the resource contract independently of the stored declarations. Canonical compiler output; code is retained unchanged by its underdeclaration variants.

### Case description

```yaml
gesBlock: case
id: valid-resource-record-create
binaryFixture:
  id: gesb-v1-valid-resource-record-create
  resourceId: gesb-v1.valid-resource-record-create
  relativePath: GesbV1/valid-resource-record-create.gesb
  sha256: D29690D35FEC9C984EE468DA493F1B68D94130BA26F7F98FFDA91CA2F84DC23C
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Canonical compiler output; code is retained unchanged by its underdeclaration variants."
```

### Source code under test

```ges
module resourceproof
record :Item as { value: :Number }
on Start(value) { let item be :Item(value: value)
 emit Done(value: item.value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: D29690D35FEC9C984EE468DA493F1B68D94130BA26F7F98FFDA91CA2F84DC23C
  moduleName: resourceproof
  requiredRegisterCount: 7
  requiredCallStackDepth: 1
  opaqueSectionCount: 0
```

---

## Test: underdeclared-resource-record-create-registers

This case validates the resource contract independently of the stored declarations. Reduce only the selected handler register requirement by one and set the program requirement to the same value; retain every instruction.

### Case description

```yaml
gesBlock: case
id: underdeclared-resource-record-create-registers
binaryFixture:
  id: gesb-v1-underdeclared-resource-record-create-registers
  resourceId: gesb-v1.underdeclared-resource-record-create-registers
  relativePath: GesbV1/underdeclared-resource-record-create-registers.gesb
  sha256: 357605C8341EF8B68D11F851BB1A193EA422233536EAA6B0444CF6C7104BC9AA
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Reduce only the selected handler register requirement by one and set the program requirement to the same value; retain every instruction."
```

### Source code under test

```ges
module resourceproof
record :Item as { value: :Number }
on Start(value) { let item be :Item(value: value)
 emit Done(value: item.value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: underdeclared-resource-record-create-depth

This case validates the resource contract independently of the stored declarations. Set handler and program call depth to zero; retain the constructor call and all other bytes.

### Case description

```yaml
gesBlock: case
id: underdeclared-resource-record-create-depth
binaryFixture:
  id: gesb-v1-underdeclared-resource-record-create-depth
  resourceId: gesb-v1.underdeclared-resource-record-create-depth
  relativePath: GesbV1/underdeclared-resource-record-create-depth.gesb
  sha256: 79BE8A60FB1E262A716A95B6F9384C94D2E595FD0DD0D656A2489BB877F35A51
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Set handler and program call depth to zero; retain the constructor call and all other bytes."
```

### Source code under test

```ges
module resourceproof
record :Item as { value: :Number }
on Start(value) { let item be :Item(value: value)
 emit Done(value: item.value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: valid-resource-record-cast

This case validates the resource contract independently of the stored declarations. Canonical compiler output; code is retained unchanged by its underdeclaration variants.

### Case description

```yaml
gesBlock: case
id: valid-resource-record-cast
binaryFixture:
  id: gesb-v1-valid-resource-record-cast
  resourceId: gesb-v1.valid-resource-record-cast
  relativePath: GesbV1/valid-resource-record-cast.gesb
  sha256: 1278A351FC903AE22163091F2EC53BB7EB59AE8156A2C21CD4A0F9874EBB1EC0
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Canonical compiler output; code is retained unchanged by its underdeclaration variants."
```

### Source code under test

```ges
module resourceproof
record :Item as { value: :Number }
on Start(value) { let item be ([value: value]) as :Item
 emit Done(value: item.value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 1278A351FC903AE22163091F2EC53BB7EB59AE8156A2C21CD4A0F9874EBB1EC0
  moduleName: resourceproof
  requiredRegisterCount: 7
  requiredCallStackDepth: 1
  opaqueSectionCount: 0
```

---

## Test: underdeclared-resource-record-cast-registers

This case validates the resource contract independently of the stored declarations. Reduce only the selected handler register requirement by one and set the program requirement to the same value; retain every instruction.

### Case description

```yaml
gesBlock: case
id: underdeclared-resource-record-cast-registers
binaryFixture:
  id: gesb-v1-underdeclared-resource-record-cast-registers
  resourceId: gesb-v1.underdeclared-resource-record-cast-registers
  relativePath: GesbV1/underdeclared-resource-record-cast-registers.gesb
  sha256: F0F47B7BFCC2173AA682884B574EFE4015D5643778F739B72D16FA6590BA28C2
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Reduce only the selected handler register requirement by one and set the program requirement to the same value; retain every instruction."
```

### Source code under test

```ges
module resourceproof
record :Item as { value: :Number }
on Start(value) { let item be ([value: value]) as :Item
 emit Done(value: item.value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: underdeclared-resource-record-cast-depth

This case validates the resource contract independently of the stored declarations. Set handler and program call depth to zero; retain the constructor call and all other bytes.

### Case description

```yaml
gesBlock: case
id: underdeclared-resource-record-cast-depth
binaryFixture:
  id: gesb-v1-underdeclared-resource-record-cast-depth
  resourceId: gesb-v1.underdeclared-resource-record-cast-depth
  relativePath: GesbV1/underdeclared-resource-record-cast-depth.gesb
  sha256: 38D00E426972285BCBF82654970E043A82FDFFB3B7CC99FAF98132BB0DFB9A1A
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Set handler and program call depth to zero; retain the constructor call and all other bytes."
```

### Source code under test

```ges
module resourceproof
record :Item as { value: :Number }
on Start(value) { let item be ([value: value]) as :Item
 emit Done(value: item.value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: valid-resource-per-handler

This case validates the resource contract independently of the stored declarations. Canonical compiler output; code is retained unchanged by its underdeclaration variants.

### Case description

```yaml
gesBlock: case
id: valid-resource-per-handler
binaryFixture:
  id: gesb-v1-valid-resource-per-handler
  resourceId: gesb-v1.valid-resource-per-handler
  relativePath: GesbV1/valid-resource-per-handler.gesb
  sha256: 9FB5EC245800D7AC681BA6E9229D0D951CC978125D85D30C3C2F8584D380FFE6
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Canonical compiler output; code is retained unchanged by its underdeclaration variants."
```

### Source code under test

```ges
module resourceproof
function add(_ value) be value + 1
on First(value) {
 emit Done(value: add(value)) }
on Second(value) {
 emit Done(value: add(add(value))) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 9FB5EC245800D7AC681BA6E9229D0D951CC978125D85D30C3C2F8584D380FFE6
  moduleName: resourceproof
  requiredRegisterCount: 5
  requiredCallStackDepth: 1
  opaqueSectionCount: 0
```

---

## Test: underdeclared-resource-per-handler-registers

This case validates the resource contract independently of the stored declarations. Reduce only the selected handler register requirement by one; retain the larger program maximum supplied by the other handler.

### Case description

```yaml
gesBlock: case
id: underdeclared-resource-per-handler-registers
binaryFixture:
  id: gesb-v1-underdeclared-resource-per-handler-registers
  resourceId: gesb-v1.underdeclared-resource-per-handler-registers
  relativePath: GesbV1/underdeclared-resource-per-handler-registers.gesb
  sha256: DFF5E5A0B803AC9D40621241E10448B21FEC7E91710CA70FBEB9ED4B4F70FEC5
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
  compareCompiledRuntime: false
  derivation: "Reduce only the selected handler register requirement by one; retain the larger program maximum supplied by the other handler."
```

### Source code under test

```ges
module resourceproof
function add(_ value) be value + 1
on First(value) {
 emit Done(value: add(value)) }
on Second(value) {
 emit Done(value: add(add(value))) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidResourceMetadata
```

---

## Test: valid-parse-literal

This case checks the ParseLiteral binary contract. Canonical ParseLiteral with a separate input and output register.

### Case description

```yaml
gesBlock: case
id: valid-parse-literal
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-valid-parse-literal
  resourceId: gesb-v1.valid-parse-literal
  relativePath: GesbV1/valid-parse-literal.gesb
  sha256: 7ED102B0D8264E5A7E1D2BF1E80D4711D8E514D9348CEFC9D2BBB97BFCE11D07
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Canonical ParseLiteral with a separate input and output register."
  compareCompiledRuntime: true
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
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
  rewriteSha256: 7ED102B0D8264E5A7E1D2BF1E80D4711D8E514D9348CEFC9D2BBB97BFCE11D07
  moduleName: parsefixture
  requiredRegisterCount: 2
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
steps:
  execute:
    input:
      args:
        - name: value
          value: { type: ":Text", value: "[1, \"1\"]" }
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "1" }
                - { type: ":Text", value: "1" }
```

---

## Test: invalid-parse-source

This case checks the ParseLiteral binary contract. Replace byte(s) at ParseLiteral instruction 1, operand offset 4, with ffff.

### Case description

```yaml
gesBlock: case
id: invalid-parse-source
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-parse-source
  resourceId: gesb-v1.invalid-parse-source
  relativePath: GesbV1/invalid-parse-source.gesb
  sha256: 8B6E8431FEF718314A38F9F335F5B0ED8FC9A19FE009998AE943555A1FC4E66E
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Replace byte(s) at ParseLiteral instruction 1, operand offset 4, with ffff."
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
  sectionType: 16
  entryIndex: 1
```

---

## Test: invalid-parse-target

This case checks the ParseLiteral binary contract. Replace byte(s) at ParseLiteral instruction 1, operand offset 2, with ffff.

### Case description

```yaml
gesBlock: case
id: invalid-parse-target
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-parse-target
  resourceId: gesb-v1.invalid-parse-target
  relativePath: GesbV1/invalid-parse-target.gesb
  sha256: FAC1069083B1EE1D43D2D0C49133F55F8371DC81207DF64AE50BDC2D32568093
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Replace byte(s) at ParseLiteral instruction 1, operand offset 2, with ffff."
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
  sectionType: 16
  entryIndex: 1
```

---

## Test: invalid-parse-flags

This case checks the ParseLiteral binary contract. Replace byte(s) at ParseLiteral instruction 1, operand offset 1, with 01.

### Case description

```yaml
gesBlock: case
id: invalid-parse-flags
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-parse-flags
  resourceId: gesb-v1.invalid-parse-flags
  relativePath: GesbV1/invalid-parse-flags.gesb
  sha256: 03A8AC49A85A700B043BA0B7B81E8642FD658A61831A6EE6052F3033E4ED36BA
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Replace byte(s) at ParseLiteral instruction 1, operand offset 1, with 01."
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
  sectionType: 16
  entryIndex: 1
```

---

## Test: invalid-parse-unused-word

This case checks the ParseLiteral binary contract. Replace byte(s) at ParseLiteral instruction 1, operand offset 6, with 01.

### Case description

```yaml
gesBlock: case
id: invalid-parse-unused-word
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-parse-unused-word
  resourceId: gesb-v1.invalid-parse-unused-word
  relativePath: GesbV1/invalid-parse-unused-word.gesb
  sha256: 40A09D9F516880F94C6E79877C018752076F8EDEBC2CF0105D49557F3B622194
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Replace byte(s) at ParseLiteral instruction 1, operand offset 6, with 01."
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
  sectionType: 16
  entryIndex: 1
```

---

## Test: invalid-parse-payload

This case checks the ParseLiteral binary contract. Replace byte(s) at ParseLiteral instruction 1, operand offset 8, with 01.

### Case description

```yaml
gesBlock: case
id: invalid-parse-payload
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-parse-payload
  resourceId: gesb-v1.invalid-parse-payload
  relativePath: GesbV1/invalid-parse-payload.gesb
  sha256: BC42434037383F709C20400B3381784FF4E89E94D986A329A1AD2FD0C4B5015B
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Replace byte(s) at ParseLiteral instruction 1, operand offset 8, with 01."
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
  sectionType: 16
  entryIndex: 1
```

---

## Test: invalid-reserved-opcode-da

This case checks the ParseLiteral binary contract. Replace byte(s) at ParseLiteral instruction 1, operand offset 0, with da.

### Case description

```yaml
gesBlock: case
id: invalid-reserved-opcode-da
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-invalid-reserved-opcode-da
  resourceId: gesb-v1.invalid-reserved-opcode-da
  relativePath: GesbV1/invalid-reserved-opcode-da.gesb
  sha256: 2D8F30057069590D1D38A5A50B4B3BCE72B514B324333ECA4AD518021373BA87
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Replace byte(s) at ParseLiteral instruction 1, operand offset 0, with da."
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOpcode
  sectionType: 16
  entryIndex: 1
```

---

## Test: valid-unused-routine

This case retains a fully instrumented unused function whose frame exceeds the handler resource maximum.

### Case description

```yaml
gesBlock: case
id: valid-unused-routine
binaryFixture:
  id: gesb-v1-valid-unused-routine
  resourceId: gesb-v1.valid-unused-routine
  relativePath: GesbV1/valid-unused-routine.gesb
  sha256: 70A5C7CB18ADCF365FCDF34F0A37AB577D4676263CEB1E1D2987E0BF099D6C56
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Canonical compiler output with all debug sections."
  compareCompiledRuntime: true
```

### Source code under test

```ges
module unusedfixture
function unused(first, second) be first + second
on Start(value) { emit Done(value: value) }
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
  rewriteSha256: 70A5C7CB18ADCF365FCDF34F0A37AB577D4676263CEB1E1D2987E0BF099D6C56
  moduleName: unusedfixture
  requiredRegisterCount: 1
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
steps:
  execute:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "42" }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":Number.int64", value: "42" }
```

---

## Test: valid-unused-routine-no-handler

This case retains a fully instrumented unused function whose frame exceeds the handler resource maximum.

### Case description

```yaml
gesBlock: case
id: valid-unused-routine-no-handler
binaryFixture:
  id: gesb-v1-valid-unused-routine-no-handler
  resourceId: gesb-v1.valid-unused-routine-no-handler
  relativePath: GesbV1/valid-unused-routine-no-handler.gesb
  sha256: 6B70F8FEF7312D2255C302AADF8107479062B7489953D8CF90E8A0277C7D05EE
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "Canonical compiler output with all debug sections."
  compareCompiledRuntime: true
```

### Source code under test

```ges
module unusedfixture
function unused(first, second) be first + second
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 6B70F8FEF7312D2255C302AADF8107479062B7489953D8CF90E8A0277C7D05EE
  moduleName: unusedfixture
  requiredRegisterCount: 0
  requiredCallStackDepth: 0
  opaqueSectionCount: 0
```

---

## Test: invalid-unused-routine-register

This case rejects register 100 in the unused function, whose frame has only three registers.

### Case description

```yaml
gesBlock: case
id: invalid-unused-routine-register
binaryFixture:
  id: gesb-v1-invalid-unused-routine-register
  resourceId: gesb-v1.invalid-unused-routine-register
  relativePath: GesbV1/invalid-unused-routine-register.gesb
  sha256: 66577A4197605BE0D59D4AF35C0E27988E87A338B27E4F5631BED5297B86770B
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "From valid-unused-routine.gesb, set the Add instruction X register at code index 4 to 100."
```

### Source code under test

```ges
module unusedfixture
function unused(first, second) be first + second
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidOperand
  sectionType: 16
  entryIndex: 4
```

---

## Test: invalid-unused-routine-debug-register

This case rejects debug register 100 in an unused function, independent of handler reachability.

### Case description

```yaml
gesBlock: case
id: invalid-unused-routine-debug-register
binaryFixture:
  id: gesb-v1-invalid-unused-routine-debug-register
  resourceId: gesb-v1.invalid-unused-routine-debug-register
  relativePath: GesbV1/invalid-unused-routine-debug-register.gesb
  sha256: 363815E791997490234EFB298E78D55940B6549AFB404B48B6C627CCF0FE62A1
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
  derivation: "From valid-unused-routine.gesb, set the first DebugSymbols record RegisterId to 100."
```

### Source code under test

```ges
module unusedfixture
function unused(first, second) be first + second
on Start(value) { emit Done(value: value) }
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: InvalidDebugSymbol
  sectionType: 32
  entryIndex: 0
```
