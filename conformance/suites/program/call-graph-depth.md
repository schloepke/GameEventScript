---
formatVersion: 1
suiteId: program.call-graph-depth
title: Bounded deep call graph validation
kind: programBinary
level: scenario
categories: [conformance, program-binary]
---

# Bounded deep call graph validation

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite validates acyclic and cyclic graphs within the V1 instruction, binding,
string, and resource limits. The deep fixtures contain 21,845 routines and exactly
65,535 instructions; shallow fixtures use eight routines. Native adapters isolate
this suite in worker processes so a validator crash cannot end the corpus run.
A crash or timeout fails the case; neither is an expected binary outcome.

The fixtures are encoded directly from the V1 layout, without compiling a large
source or invoking the validator. For N routines the five required sections are
canonical, with no optional sections, no index lists, and ProgramVersion 0.
Strings are `callgraph`, `Start`, followed by `f1` through `f(N-1)` in numeric
order. Each routine starts at instruction `3*i` and contains `RegisterLocals 1`,
`Call r0 @(3*(i+1))`, and `ReturnVoid`. In the acyclic leaf the Call is replaced
by Nop. In the cyclic leaf it calls `@3`, closing a cycle through the functions.
All unused instruction words/payload bits are zero; RegisterLocals stores 1 in
Word1, and Call stores its target in Word2.

Binding zero is `MessageHandler Start`, ID 0, address 0, N required registers,
and N-1 required call frames. The other bindings are `Function fi`, ID i-1,
name-string index i+1, address 3*i, and zero handler-resource fields. Every
argument/tag list is absent (0xFFFF); all flags and reserved fields are zero.
Program resources are also N and N-1. An acyclic run therefore retains exactly
one register per active routine and N-1 frames at its deepest point. Cyclic
fixtures differ only in the leaf instruction; their cycle must be rejected
before execution. No host limits or implementation-specific exit codes enter
the expected results. Compiler identity below records fixture provenance only;
these files have no BuildMetadata section.

---

## Test: valid-shallow-call-chain

This acyclic fixture contains 8 routines and 24 instructions in 716 bytes. It must validate and preserve canonical bytes and declared resources without terminating the runner process.

### Case description

```yaml
gesBlock: case
id: valid-shallow-call-chain
binaryFixture:
  id: gesb-v1-valid-shallow-call-chain
  resourceId: gesb-v1.valid-shallow-call-chain
  relativePath: GesbV1/valid-shallow-call-chain.gesb
  sha256: 151EC70AC866EE53BD65D77750803A45C6FF8220B4F1826ECFCEA9CE936C84B3
  compilerId: ges.conformance.fixture
  compilerVersion: "1"
  programVersion: 0
  compareCompiledRuntime: false
  derivation: "Direct V1 encoding with N=8; construction and resource proof are specified in the suite preamble."
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 151EC70AC866EE53BD65D77750803A45C6FF8220B4F1826ECFCEA9CE936C84B3
  moduleName: callgraph
  requiredRegisterCount: 8
  requiredCallStackDepth: 7
  opaqueSectionCount: 0
```

---

## Test: invalid-shallow-call-cycle

This cyclic fixture contains 8 routines and 24 instructions in 716 bytes. It must report the stable CyclicCallGraph validation error without terminating the runner process.

### Case description

```yaml
gesBlock: case
id: invalid-shallow-call-cycle
binaryFixture:
  id: gesb-v1-invalid-shallow-call-cycle
  resourceId: gesb-v1.invalid-shallow-call-cycle
  relativePath: GesbV1/invalid-shallow-call-cycle.gesb
  sha256: 302948957C9E180ABA7CEF0A251693784BD396DD7878D8EDAA5360445F5058B9
  compilerId: ges.conformance.fixture
  compilerVersion: "1"
  programVersion: 0
  compareCompiledRuntime: false
  derivation: "Direct V1 encoding with N=8; construction and resource proof are specified in the suite preamble."
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: CyclicCallGraph
```

---

## Test: valid-deep-call-chain

This acyclic fixture contains 21,845 routines and 65,535 instructions in 1,692,924 bytes. It must validate and preserve canonical bytes and declared resources without terminating the runner process.

### Case description

```yaml
gesBlock: case
id: valid-deep-call-chain
binaryFixture:
  id: gesb-v1-valid-deep-call-chain
  resourceId: gesb-v1.valid-deep-call-chain
  relativePath: GesbV1/valid-deep-call-chain.gesb
  sha256: 3BFFC737A0E1794B4F917BB1875C101783D5AA1DF585B847D9EBBB5E15D5C997
  compilerId: ges.conformance.fixture
  compilerVersion: "1"
  programVersion: 0
  compareCompiledRuntime: false
  derivation: "Direct V1 encoding with N=21845; construction and resource proof are specified in the suite preamble."
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 3BFFC737A0E1794B4F917BB1875C101783D5AA1DF585B847D9EBBB5E15D5C997
  moduleName: callgraph
  requiredRegisterCount: 21845
  requiredCallStackDepth: 21844
  opaqueSectionCount: 0
```

---

## Test: invalid-deep-call-cycle

This cyclic fixture contains 21,845 routines and 65,535 instructions in 1,692,924 bytes. It must report the stable CyclicCallGraph validation error without terminating the runner process.

### Case description

```yaml
gesBlock: case
id: invalid-deep-call-cycle
binaryFixture:
  id: gesb-v1-invalid-deep-call-cycle
  resourceId: gesb-v1.invalid-deep-call-cycle
  relativePath: GesbV1/invalid-deep-call-cycle.gesb
  sha256: D88F4013DE9B01B0CC1B90FDCD803EB9013E52F4B035DA88618B198280E28093
  compilerId: ges.conformance.fixture
  compilerVersion: "1"
  programVersion: 0
  compareCompiledRuntime: false
  derivation: "Direct V1 encoding with N=21845; construction and resource proof are specified in the suite preamble."
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: validationError
  errorCode: CyclicCallGraph
```
