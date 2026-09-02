---
formatVersion: 1
suiteId: migration-pilot
title: Representative JSON to Markdown migration
level: atomic
categories: [conformance, migration-pilot]
tags: [markdown-v1]
compile:
  debugInfo: [debugSymbols, sourceMap, sourceArchive]
  binaryRoundTrip: false
---

# Representative vertical migration

Each case below mirrors an existing JSON conformance case. The JSON source is
kept temporarily so the pilot can prove model and execution parity before the
deterministic bulk migration.

## Test: handler binding creates message values and publishable calls

```yaml
gesBlock: case
id: script-api
kind: scriptApi
level: scenario
compile:
  binaryRoundTrip: true
sources:
  - name: handler binding creates message values and publishable calls.ges
    program: main
```

```ges
module MessageBinding
on Start(unit, target) {
  let shoot as :handler be Shoot(unit, target)
  let msg as :message be shoot(unit: unit, target: target)
  let isHandler be shoot is :handler
  let isMessage be msg is :message
  emit msg
  emit shoot(unit: unit, target: target)
  emit Shoot(unit: unit, target: target)
  emit Done(isHandler: isHandler, isMessage: isMessage)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: unit
          value: { type: ":text", value: u_1 }
        - name: target
          value: { type: ":text", value: t_1 }
    local:
      - name: Shoot
        args:
          - name: unit
            value: { type: ":text", value: u_1 }
          - name: target
            value: { type: ":text", value: t_1 }
      - name: Shoot
        args:
          - name: unit
            value: { type: ":text", value: u_1 }
          - name: target
            value: { type: ":text", value: t_1 }
      - name: Shoot
        args:
          - name: unit
            value: { type: ":text", value: u_1 }
          - name: target
            value: { type: ":text", value: t_1 }
      - name: Done
        args:
          - name: isHandler
            value: { type: ":boolean", value: true }
          - name: isMessage
            value: { type: ":boolean", value: true }
```

## Test: module build errors preserve parser source range

```yaml
gesBlock: case
id: compile-error
kind: compileError
level: scenario
sources:
  - name: location.ges
    program: main
```

```ges
module Location

on Start {
  let value be 1
  let value be 2
}
```

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.duplicateVariable
  symbol: value
  symbolKind: variable
  sourceName: location.ges
  line: 5
  column: 3
  programName: Location
```

## Test: host rejects program whose call depth exceeds its limit

```yaml
gesBlock: case
id: load-error
kind: loadError
level: scenario
runtimeLimits:
  maxCallDepth: 1
sources:
  - name: host rejects program whose call depth exceeds its limit.ges
    program: main
```

```ges
module BinaryCompilerCallDepthLimit

function leaf(value) be value + 1

function middle(value) be leaf(value: value)

on Start(value) {
  let result be middle(value: value)
  emit Done(result: result)
}

```

```yaml
gesBlock: expect
error:
  phase: link
  code: link.requiredCallStackDepthExceeded
```

## Test: message signatures are ordered

```yaml
gesBlock: case
id: message-api
kind: messageApi
level: scenario
messageApi:
  signature:
    name: Start
    parameters: [a, b]
  message:
    name: Start
    args:
      - name: b
        value: { type: ":integer", value: "2" }
      - name: a
        value: { type: ":integer", value: "1" }
```

```yaml
gesBlock: expect
message:
  name: Start
  signatureId: "Start(a,b)"
  messageSignatureId: "Start(b,a)"
  matches: false
  argumentCount: 2
```

## Test: handler and program resource requirements include nested calls

```yaml
gesBlock: case
id: compile-metadata
kind: compileMetadata
level: scenario
sources:
  - name: handler and program resource requirements include nested calls.ges
    program: main
```

```ges
module BinaryCompilerResources

function leaf(value) be value + 1

function middle(value) be leaf(value: value)

on Start(value) {
  let result be middle(value: value)
  emit Done(result: result)
}

on Ping(value) {
  emit Pong(value: value)
}

```

```yaml
gesBlock: expect
metadata:
  programResources:
    requiredRegisterCount: 7
    requiredCallStackDepth: 2
  handlerResources:
    - name: Start
      requiredRegisterCount: 7
      requiredCallStackDepth: 2
    - name: Ping
      requiredRegisterCount: 1
      requiredCallStackDepth: 0
```

## Test: emit arguments read existing registers without temporary moves

```yaml
gesBlock: case
id: bytecode
kind: bytecode
sources:
  - name: emit arguments read existing registers without temporary moves.ges
    program: main
```

```ges
module EmitArgumentReads

on Start(value) {
  let doubled be value * 2
  emit Done(original: value, result: doubled)
}

```

```yaml
gesBlock: expect
opcodes:
  contains: [Multiply, EmitMessage]
  excludes: [Move]
```

## Test: math integer hot path

This pilot executes the original correctness workload. Its deterministic test
provider returns the existing C# reference values so the profile, metric,
tolerance, report, and received-update paths are exercised without making the
ordinary conformance suite timing-sensitive.

```yaml
gesBlock: case
id: performance
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: math integer hot path.ges
    program: main
```

```ges
module PerformanceMath

on Start(value) {
  let a be value + 5
  let b be a * 3
  let c be b - 7
  let d be c div 2
  let squaredValue be d * d
  let f be squaredValue mod 97
  let g be f + 10
  let h be g * 2
  let i be h - 15
  let j be i div 5
  emit Done(result: j)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "10" }
    local:
      - name: Done
        args:
          - name: result
            value: { type: ":integer", value: "29" }
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.per-invoke-elapsed:
          reference: 0.002669
          toleranceRelative: 0.15
          toleranceAbsolute: 0.001
          unit: ms
        run.per-invoke-allocated:
          reference: 0.172
          maximum: 0.172
          unit: KiB
```

## Test: math integer hot path bytecode snapshot

```yaml
gesBlock: case
id: bytecode-snapshot
kind: bytecodeSnapshot
level: scenario
sources:
  - name: math integer hot path.ges
    program: main
```

```ges
module PerformanceMath

on Start(value) {
  let a be value + 5
  let b be a * 3
  let c be b - 7
  let d be c div 2
  let squaredValue be d * d
  let f be squaredValue mod 97
  let g be f + 10
  let h be g * 2
  let i be h - 15
  let j be i div 5
  emit Done(result: j)
}

```

```gesa
// -------------------------------------------------------------------------------
//  Module: PerformanceMath
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "PerformanceMath"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: math integer hot path.ges"

.segment source "math integer hot path.ges"

module PerformanceMath

on Start(value) {
  let a be value + 5
  let b be a * 3
  let c be b - 7
  let d be c div 2
  let squaredValue be d * d
  let f be squaredValue mod 97
  let g be f + 10
  let h be g * 2
  let i be h - 15
  let j be i div 5
  emit Done(result: j)
}

.region-end "Source: math integer hot path.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_value:			.text "value"
T_Start:			.text "Start"
T_result:			.text "result"
T_Done:				.text "Done"
T_PerformanceMath:	.text "PerformanceMath"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
U16_2:				.u16 [2]
Args_3:				.registers [r10]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[T_value] entry=Start // "Start(value)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_result] // "Done(result)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "math integer hot path.ges" 3 | on Start(value) {
Start:				 // handler Start(value)
					RegisterLocals #11

.source-line "math integer hot path.ges" 4 |   let a be value + 5
					LoadInteger r11, #5
					Add r1(a), r0(value), r11

.source-line "math integer hot path.ges" 5 |   let b be a * 3
					LoadInteger r11, #3
					Multiply r2(b), r1(a), r11

.source-line "math integer hot path.ges" 6 |   let c be b - 7
					LoadInteger r11, #7
					Subtract r3(c), r2(b), r11

.source-line "math integer hot path.ges" 7 |   let d be c div 2
					LoadInteger r11, #2
					IntegerDivide r4(d), r3(c), r11

.source-line "math integer hot path.ges" 8 |   let squaredValue be d * d
					Multiply r5(squaredValue), r4(d), r4(d)

.source-line "math integer hot path.ges" 9 |   let f be squaredValue mod 97
					LoadInteger r11, #97
					Modulo r6(f), r5(squaredValue), r11

.source-line "math integer hot path.ges" 10 |   let g be f + 10
					LoadInteger r11, #10
					Add r7(g), r6(f), r11

.source-line "math integer hot path.ges" 11 |   let h be g * 2
					LoadInteger r11, #2
					Multiply r8(h), r7(g), r11

.source-line "math integer hot path.ges" 12 |   let i be h - 15
					LoadInteger r11, #15
					Subtract r9(i), r8(h), r11

.source-line "math integer hot path.ges" 13 |   let j be i div 5
					LoadInteger r11, #5
					IntegerDivide r10(j), r9(i), r11

.source-line "math integer hot path.ges" 14 |   emit Done(result: j)
					EmitMessage Outbound_Done, Args_3 // "Done(result)"

.source-line "math integer hot path.ges" 3 | on Start(value) {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```
