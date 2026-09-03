---
formatVersion: 1
suiteId: "performance.core"
title: "PerformanceCore"
categories: [conformance]
---

# PerformanceCore

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite defines repeatable core-runtime workloads, expected behavior, portable bytecode snapshots, and implementation-specific performance budgets.

---

## Test: math integer hot path

This benchmark exercises “math integer hot path” and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: performance
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "math integer hot path.ges"
    program: main
```

### Source code under test

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
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":integer"
            value: "10"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":integer"
              value: "29"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.3407
          toleranceAbsolute: 1
          unit: ms
        ast-build.allocated:
          reference: 22.281
          maximum: 22.281
          unit: KiB
        binary-build.elapsed:
          reference: 0.4375
          toleranceAbsolute: 1
          unit: ms
        binary-build.allocated:
          reference: 46.969
          maximum: 46.969
          unit: KiB
        program-load.elapsed:
          reference: 0.2801
          toleranceAbsolute: 1
          unit: ms
        program-load.allocated:
          reference: 13.461
          maximum: 13.461
          unit: KiB
        compile.elapsed:
          reference: 1.0583
          toleranceAbsolute: 1
          unit: ms
        compile.allocated:
          reference: 82.711
          maximum: 82.711
          unit: KiB
        run.elapsed:
          reference: 2.6689
          toleranceAbsolute: 1
          unit: ms
        run.allocated:
          reference: 171.914
          maximum: 171.914
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.002669
          toleranceAbsolute: 1
          unit: ms
        run.per-invoke-allocated:
          reference: 0.172
          maximum: 0.172
          unit: KiB
```

---

## Test: math integer hot path bytecode snapshot

This snapshot checks the generated Game Event Script Assembler representation for “math integer hot path bytecode snapshot”.

### Case description

```yaml
gesBlock: case
id: snapshot-0001
kind: bytecodeSnapshot
level: scenario
sources:
  - name: "math integer hot path.ges"
    program: main
```

### Source code under test

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

### Expected Game Event Script Assembler

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

---

## Test: range select and aggregate

This benchmark exercises “range select and aggregate” and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: performance
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "range select and aggregate.ges"
    program: main
```

### Source code under test

```ges
module PerformanceRanges

on Start {
  let values be :list[:select item from 1 to 100 => item * 2]
  let total be values[:sum value => value]
  let count be values[:count]
  emit Done(total: total, count: count)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "total"
            value:
              type: ":integer"
              value: "10100"
          - name: "count"
            value:
              type: ":integer"
              value: "100"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.1287
          toleranceAbsolute: 1
          unit: ms
        ast-build.allocated:
          reference: 14.594
          maximum: 14.594
          unit: KiB
        binary-build.elapsed:
          reference: 0.2945
          toleranceAbsolute: 1
          unit: ms
        binary-build.allocated:
          reference: 46.695
          maximum: 46.695
          unit: KiB
        program-load.elapsed:
          reference: 0.0746
          toleranceAbsolute: 1
          unit: ms
        program-load.allocated:
          reference: 13.258
          maximum: 13.258
          unit: KiB
        compile.elapsed:
          reference: 0.4978
          toleranceAbsolute: 1
          unit: ms
        compile.allocated:
          reference: 74.547
          maximum: 74.547
          unit: KiB
        run.elapsed:
          reference: 51.0479
          toleranceAbsolute: 7.657185
          unit: ms
        run.allocated:
          reference: 11476.602
          maximum: 11476.602
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.051048
          toleranceAbsolute: 1
          unit: ms
        run.per-invoke-allocated:
          reference: 11.477
          maximum: 11.477
          unit: KiB
```

---

## Test: range select and aggregate bytecode snapshot

This snapshot checks the generated Game Event Script Assembler representation for “range select and aggregate bytecode snapshot”.

### Case description

```yaml
gesBlock: case
id: snapshot-0002
kind: bytecodeSnapshot
level: scenario
sources:
  - name: "range select and aggregate.ges"
    program: main
```

### Source code under test

```ges
module PerformanceRanges

on Start {
  let values be :list[:select item from 1 to 100 => item * 2]
  let total be values[:sum value => value]
  let count be values[:count]
  emit Done(total: total, count: count)
}

```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: PerformanceRanges
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "PerformanceRanges"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: range select and aggregate.ges"

.segment source "range select and aggregate.ges"

module PerformanceRanges

on Start {
  let values be :list[:select item from 1 to 100 => item * 2]
  let total be values[:sum value => value]
  let count be values[:count]
  emit Done(total: total, count: count)
}

.region-end "Source: range select and aggregate.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_total:			.text "total"
T_count:			.text "count"
T_Done:				.text "Done"
T_PerformanceRanges:	.text "PerformanceRanges"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2]
Args_2:				.registers [r2, r3]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_total, T_count] // "Done(total, count)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "range select and aggregate.ges" 3 | on Start {
Start:				 // handler Start()
					RegisterLocals #8

.source-line "range select and aggregate.ges" 4 |   let values be :list[:select item from 1 to 100 => item * 2]
					ListBuilderCreate r4
					CreateRangeIteratorShort r5, #1, #100, #1
Start_3:			IteratorNext r1(item), r5, Start_8
					LoadInteger r6, #2
					Multiply r7, r1(item), r6
					ListBuilderAdd r4, r7
					Jump Start_3
Start_8:			IteratorClose r5
					ListBuilderFinish r0(values), r4

.source-line "range select and aggregate.ges" 5 |   let total be values[:sum value => value]
					IteratorCreateOrJump r4, r0(values), Start_21
					IteratorNext r5, r4, Start_16
					Move r2(total), r5
Start_13:			IteratorNext r5, r4, Start_19
					Add r2(total), r2(total), r5
					Jump Start_13
Start_16:			IteratorClose r4
					LoadInteger r2(total), #0
					Jump Start_22
Start_19:			IteratorClose r4
					Jump Start_22
Start_21:			LoadNothing r2(total)

.source-line "range select and aggregate.ges" 6 |   let count be values[:count]
Start_22:			Count r3(count), r0(values)

.source-line "range select and aggregate.ges" 7 |   emit Done(total: total, count: count)
					EmitMessage Outbound_Done, Args_2 // "Done(total, count)"

.source-line "range select and aggregate.ges" 3 | on Start {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: record creation and casts

This benchmark exercises “record creation and casts” and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: performance
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "record creation and casts.ges"
    program: main
```

### Source code under test

```ges
module PerformanceTypes

record :sample as {
  _ value: :number,
  label: :text,
  doubled: :number computed by value * 2
}

on Start {
  let rec be :sample(21, label: 'ok')
  let textNumber be '42' as :number
  emit Done(doubled: rec.doubled, label: rec.label, textNumber: textNumber, isSample: rec is :sample)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "doubled"
            value:
              type: ":integer"
              value: "42"
          - name: "label"
            value:
              type: ":text"
              value: "ok"
          - name: "textNumber"
            value:
              type: ":integer"
              value: "42"
          - name: "isSample"
            value:
              type: ":boolean"
              value: true
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.1114
          toleranceAbsolute: 1
          unit: ms
        ast-build.allocated:
          reference: 17.703
          maximum: 18.031
          unit: KiB
        binary-build.elapsed:
          reference: 0.3411
          toleranceAbsolute: 1
          unit: ms
        binary-build.allocated:
          reference: 46.336
          maximum: 46.547
          unit: KiB
        program-load.elapsed:
          reference: 0.1289
          toleranceAbsolute: 1
          unit: ms
        program-load.allocated:
          reference: 15.141
          maximum: 15.352
          unit: KiB
        compile.elapsed:
          reference: 0.5814
          toleranceAbsolute: 1
          unit: ms
        compile.allocated:
          reference: 79.18
          maximum: 79.93
          unit: KiB
        run.elapsed:
          reference: 2.8563
          toleranceAbsolute: 1
          unit: ms
        run.allocated:
          reference: 718.789
          maximum: 718.789
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.002856
          toleranceAbsolute: 1
          unit: ms
        run.per-invoke-allocated:
          reference: 0.719
          maximum: 0.719
          unit: KiB
```

---

## Test: record creation and casts bytecode snapshot

This snapshot checks the generated Game Event Script Assembler representation for “record creation and casts bytecode snapshot”.

### Case description

```yaml
gesBlock: case
id: snapshot-0003
kind: bytecodeSnapshot
level: scenario
sources:
  - name: "record creation and casts.ges"
    program: main
```

### Source code under test

```ges
module PerformanceTypes

record :sample as {
  _ value: :number,
  label: :text,
  doubled: :number computed by value * 2
}

on Start {
  let rec be :sample(21, label: 'ok')
  let textNumber be '42' as :number
  emit Done(doubled: rec.doubled, label: rec.label, textNumber: textNumber, isSample: rec is :sample)
}

```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: PerformanceTypes
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "PerformanceTypes"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: record creation and casts.ges"

.segment source "record creation and casts.ges"

module PerformanceTypes

record :sample as {
  _ value: :number,
  label: :text,
  doubled: :number computed by value * 2
}

on Start {
  let rec be :sample(21, label: 'ok')
  let textNumber be '42' as :number
  emit Done(doubled: rec.doubled, label: rec.label, textNumber: textNumber, isSample: rec is :sample)
}

.region-end "Source: record creation and casts.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_1:				.text "_"
T_label:			.text "label"
T_sample:			.text "sample"
T_doubled:			.text "doubled"
T_textNumber:		.text "textNumber"
T_isSample:			.text "isSample"
T_Done:				.text "Done"
T_ok:				.text "ok"
T_value:			.text "value"
T_PerformanceTypes:	.text "PerformanceTypes"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2]
U16_2:				.u16 [4, 2, 5, 6]
Args_3:				.registers [r2, r3, r1, r4]
Keys_4:				.texts [T_value, T_label, T_doubled] // "value", "label", "doubled"

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Record_sample:		.bind Record id=0 name=T_sample args=[T_1, T_label] entry=record_sample // "sample(_, label)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_doubled, T_label, T_textNumber, T_isSample] // "Done(doubled, label, textNumber, isSample)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "record creation and casts.ges" 9 | on Start {
Start:				 // handler Start()
					RegisterLocals #5

.source-line "record creation and casts.ges" 10 |   let rec be :sample(21, label: 'ok')
					StageInteger #21
					StageText T_ok // "ok"
					CreateRecord r0(rec), Record_sample // "sample(_, label)"

.source-line "record creation and casts.ges" 11 |   let textNumber be '42' as :number
					LoadInteger r1(textNumber), #42

.source-line "record creation and casts.ges" 12 |   emit Done(doubled: rec.doubled, label: rec.label, textNumber: textNumber, isSample: rec is :sample)
					MemberAccess r2, T_doubled, r0(rec) // "doubled"
					MemberAccess r3, T_label, r0(rec) // "label"
					CheckCustomType r4, r0(rec), T_sample // "sample"
					EmitMessage Outbound_Done, Args_3 // "Done(doubled, label, textNumber, isSample)"

.source-line "record creation and casts.ges" 9 | on Start {
					ReturnVoid

.source-line "record creation and casts.ges" 3 | record :sample as {
record_sample:		 // record sample(_, label)
					RegisterLocals #3

.source-line "record creation and casts.ges" 4 |   _ value: :number,
					CastNumeric r0(_), r0(_)

.source-line "record creation and casts.ges" 5 |   label: :text,
					Cast r1(label), r1(label), Text

.source-line "record creation and casts.ges" 6 |   doubled: :number computed by value * 2
					LoadInteger r3, #2
					Multiply r4, r0(_), r3
					CastNumeric r2(doubled), r4

.source-line "record creation and casts.ges" 3 | record :sample as {
					StageRegister r0(_)
					StageRegister r1(label)
					StageRegister r2(doubled)
					CreateMap r3, Keys_4 // "value", "label", "doubled"
					CreateRecordValue r4, r3, T_sample // "sample"
					ReturnValue r4

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: pipeline filter select sum and direct list slice

This benchmark exercises “pipeline filter select sum and direct list slice” and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: performance
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "pipeline filter select sum and direct list slice.ges"
    program: main
```

### Source code under test

```ges
module PerformancePipelines

on Start {
  let values be [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]
  let firstTen be values[:take first 10]
  let directTotal be firstTen[:sum value => value]
  let streamTotal be values[:filter value where value mod 2 = 0][:select value => value * 2][:sum value => value]
  let streamCount be values[:filter value where value > 10][:count value where true]
  emit Done(directTotal: directTotal, streamTotal: streamTotal, streamCount: streamCount)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "directTotal"
            value:
              type: ":integer"
              value: "55"
          - name: "streamTotal"
            value:
              type: ":integer"
              value: "220"
          - name: "streamCount"
            value:
              type: ":integer"
              value: "10"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.1729
          toleranceAbsolute: 1
          unit: ms
        ast-build.allocated:
          reference: 29.695
          maximum: 30.977
          unit: KiB
        binary-build.elapsed:
          reference: 0.5369
          toleranceAbsolute: 1
          unit: ms
        binary-build.allocated:
          reference: 105.406
          maximum: 105.617
          unit: KiB
        program-load.elapsed:
          reference: 0.1581
          toleranceAbsolute: 1
          unit: ms
        program-load.allocated:
          reference: 18.32
          maximum: 18.531
          unit: KiB
        compile.elapsed:
          reference: 0.8679
          toleranceAbsolute: 1
          unit: ms
        compile.allocated:
          reference: 153.422
          maximum: 155.125
          unit: KiB
        run.elapsed:
          reference: 19.525
          toleranceAbsolute: 2.92875
          unit: ms
        run.allocated:
          reference: 1312.539
          maximum: 1312.539
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.019525
          toleranceAbsolute: 1
          unit: ms
        run.per-invoke-allocated:
          reference: 1.313
          maximum: 1.313
          unit: KiB
```

---

## Test: pipeline filter select sum and direct list slice bytecode snapshot

This snapshot checks the generated Game Event Script Assembler representation for “pipeline filter select sum and direct list slice bytecode snapshot”.

### Case description

```yaml
gesBlock: case
id: snapshot-0004
kind: bytecodeSnapshot
level: scenario
sources:
  - name: "pipeline filter select sum and direct list slice.ges"
    program: main
```

### Source code under test

```ges
module PerformancePipelines

on Start {
  let values be [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]
  let firstTen be values[:take first 10]
  let directTotal be firstTen[:sum value => value]
  let streamTotal be values[:filter value where value mod 2 = 0][:select value => value * 2][:sum value => value]
  let streamCount be values[:filter value where value > 10][:count value where true]
  emit Done(directTotal: directTotal, streamTotal: streamTotal, streamCount: streamCount)
}

```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: PerformancePipelines
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "PerformancePipelines"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: pipeline filter select sum and direct list slice.ges"

.segment source "pipeline filter select sum and direct list slice.ges"

module PerformancePipelines

on Start {
  let values be [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]
  let firstTen be values[:take first 10]
  let directTotal be firstTen[:sum value => value]
  let streamTotal be values[:filter value where value mod 2 = 0][:select value => value * 2][:sum value => value]
  let streamCount be values[:filter value where value > 10][:count value where true]
  emit Done(directTotal: directTotal, streamTotal: streamTotal, streamCount: streamCount)
}

.region-end "Source: pipeline filter select sum and direct list slice.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_directTotal:		.text "directTotal"
T_streamTotal:		.text "streamTotal"
T_streamCount:		.text "streamCount"
T_Done:				.text "Done"
T_PerformancePipelines:	.text "PerformancePipelines"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2, 3]
Args_2:				.registers [r2, r3, r4]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_directTotal, T_streamTotal, T_streamCount] // "Done(directTotal, streamTotal, streamCount)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "pipeline filter select sum and direct list slice.ges" 3 | on Start {
Start:				 // handler Start()
					RegisterLocals #11
// compiler-generated
					StageInteger #1
					StageInteger #2
					StageInteger #3
					StageInteger #4
					StageInteger #5
					StageInteger #6
					StageInteger #7
					StageInteger #8
					StageInteger #9
					StageInteger #10
					StageInteger #11
					StageInteger #12
					StageInteger #13
					StageInteger #14
					StageInteger #15
					StageInteger #16
					StageInteger #17
					StageInteger #18
					StageInteger #19
					StageInteger #20

.source-line "pipeline filter select sum and direct list slice.ges" 4 |   let values be [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]
					CreateList r0(values)

.source-line "pipeline filter select sum and direct list slice.ges" 5 |   let firstTen be values[:take first 10]
					TakeFirst r1(firstTen), r0(values), #10

.source-line "pipeline filter select sum and direct list slice.ges" 6 |   let directTotal be firstTen[:sum value => value]
					IteratorCreateOrJump r5, r1(firstTen), Start_34
					IteratorNext r6, r5, Start_29
					Move r2(directTotal), r6
Start_26:			IteratorNext r6, r5, Start_32
					Add r2(directTotal), r2(directTotal), r6
					Jump Start_26
Start_29:			IteratorClose r5
					LoadInteger r2(directTotal), #0
					Jump Start_35
Start_32:			IteratorClose r5
					Jump Start_35
Start_34:			LoadNothing r2(directTotal)

.source-line "pipeline filter select sum and direct list slice.ges" 7 |   let streamTotal be values[:filter value where value mod 2 = 0][:select value => value * 2][:sum value => value]
Start_35:			IteratorCreateOrJump r5, r0(values), Start_56
					LoadFalse r6
					LoadNothing r3(streamTotal)
Start_38:			IteratorNext r7, r5, Start_52
					LoadInteger r8, #2
					Modulo r9, r7, r8
					LoadInteger r8, #0
					Equal r10, r9, r8
					JumpIfNotTrue r10, Start_38
					LoadInteger r9, #2
					Multiply r8, r7, r9
					JumpIfTrue r6, Start_50
					Move r3(streamTotal), r8
					LoadTrue r6
					Jump Start_38
Start_50:			Add r3(streamTotal), r3(streamTotal), r8
					Jump Start_38
Start_52:			IteratorClose r5
					JumpIfTrue r6, Start_57
					LoadInteger r3(streamTotal), #0
					Jump Start_57
Start_56:			LoadNothing r3(streamTotal)

.source-line "pipeline filter select sum and direct list slice.ges" 8 |   let streamCount be values[:filter value where value > 10][:count value where true]
Start_57:			IteratorCreateOrJump r5, r0(values), Start_68
					LoadInteger r4(streamCount), #0
					LoadInteger r6, #1
Start_60:			IteratorNext r7, r5, Start_66
					LoadInteger r9, #10
					Greater r8, r7, r9
					JumpIfNotTrue r8, Start_60
					Add r4(streamCount), r4(streamCount), r6
					Jump Start_60
Start_66:			IteratorClose r5
					Jump Start_69
Start_68:			LoadNothing r4(streamCount)

.source-line "pipeline filter select sum and direct list slice.ges" 9 |   emit Done(directTotal: directTotal, streamTotal: streamTotal, streamCount: streamCount)
Start_69:			EmitMessage Outbound_Done, Args_2 // "Done(directTotal, streamTotal, streamCount)"

.source-line "pipeline filter select sum and direct list slice.ges" 3 | on Start {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: engine performance mixed pipelines control flow and messages

This benchmark exercises “engine performance mixed pipelines control flow and messages” and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: performance
level: scenario
runtimeLimits:
  maxProcessedEventsPerRun: 128
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
performance:
  iterations: 1000
  warmupIterations: 50
  compileWarmupIterations: 3
sources:
  - name: "engine performance mixed pipelines control flow and messages.ges"
    program: main
```

### Source code under test

```ges
module EnginePerformance

predicate high(value as :number) be value >= 10

on Start(values) {
  let total be values[:filter value where value is high][:select value => value + 5%][:sum value => floor value]
  let average be values[:filter value where value is high][:select value => value + 5%][:average value => floor value]
  let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
  let firstBoosted be values[:filter value where value is high][:select value => value + 5%][:first]
  let scaled as :quantity(m) be 100m + 5%
  let folded be (15% + 15%) * 2
  let directOddScaled be values[:filter value where value mod 2 = 1][:select value => value * 2][:count value where value > 10]
  if scaled > 100m {
    let success be scaled + folded
    let divis be scaled ÷ folded
    let myHandler be Success(message, value)
    let myMessage be myHandler(message: 'hello', value: success)
    let myMessageDirect be Success(message: 'world', value: scaled)
  }
  for item from 1 to 16 {
    let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value => (value + item) * 2][:sum value => value]
  }
  let workTotal be values[:filter value where value >= 10][:select value => value + 5%][:select value => value * 2][:sum value => value]
  emit Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "values"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "1"
              - type: ":integer"
                value: "2"
              - type: ":integer"
                value: "3"
              - type: ":integer"
                value: "4"
              - type: ":integer"
                value: "5"
              - type: ":integer"
                value: "6"
              - type: ":integer"
                value: "7"
              - type: ":integer"
                value: "8"
              - type: ":integer"
                value: "9"
              - type: ":integer"
                value: "10"
              - type: ":integer"
                value: "11"
              - type: ":integer"
                value: "12"
              - type: ":integer"
                value: "13"
              - type: ":integer"
                value: "14"
              - type: ":integer"
                value: "15"
              - type: ":integer"
                value: "16"
              - type: ":integer"
                value: "17"
              - type: ":integer"
                value: "18"
              - type: ":integer"
                value: "19"
              - type: ":integer"
                value: "20"
              - type: ":integer"
                value: "21"
              - type: ":integer"
                value: "22"
              - type: ":integer"
                value: "23"
              - type: ":integer"
                value: "24"
              - type: ":integer"
                value: "25"
              - type: ":integer"
                value: "26"
              - type: ":integer"
                value: "27"
              - type: ":integer"
                value: "28"
              - type: ":integer"
                value: "29"
              - type: ":integer"
                value: "30"
              - type: ":integer"
                value: "31"
              - type: ":integer"
                value: "32"
              - type: ":integer"
                value: "33"
              - type: ":integer"
                value: "34"
              - type: ":integer"
                value: "35"
              - type: ":integer"
                value: "36"
              - type: ":integer"
                value: "37"
              - type: ":integer"
                value: "38"
              - type: ":integer"
                value: "39"
              - type: ":integer"
                value: "40"
              - type: ":integer"
                value: "41"
              - type: ":integer"
                value: "42"
              - type: ":integer"
                value: "43"
              - type: ":integer"
                value: "44"
              - type: ":integer"
                value: "45"
              - type: ":integer"
                value: "46"
              - type: ":integer"
                value: "47"
              - type: ":integer"
                value: "48"
              - type: ":integer"
                value: "49"
              - type: ":integer"
                value: "50"
    local:
      - name: "Done"
        args:
          - name: "total"
            value:
              type: ":integer"
              value: "1272"
          - name: "average"
            value:
              type: ":float"
              value: "31.024390243902438"
          - name: "oddCount"
            value:
              type: ":integer"
              value: "25"
          - name: "directOddScaled"
            value:
              type: ":integer"
              value: "22"
          - name: "first"
            value:
              type: ":float"
              value: "10.5"
          - name: "scaled"
            value:
              type: ":integer"
              value: "105"
              unit: ":meter"
          - name: "folded"
            value:
              type: ":float"
              value: "0.6"
          - name: "workTotal"
            value:
              type: ":integer"
              value: "2583"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.3699
          toleranceAbsolute: 1
          unit: ms
        ast-build.allocated:
          reference: 75.766
          maximum: 81.945
          unit: KiB
        binary-build.elapsed:
          reference: 1.0498
          toleranceAbsolute: 1
          unit: ms
        binary-build.allocated:
          reference: 252.648
          maximum: 253.445
          unit: KiB
        program-load.elapsed:
          reference: 0.188
          toleranceAbsolute: 1
          unit: ms
        program-load.allocated:
          reference: 32.227
          maximum: 32.867
          unit: KiB
        compile.elapsed:
          reference: 1.6077
          toleranceAbsolute: 1
          unit: ms
        compile.allocated:
          reference: 360.641
          maximum: 368.258
          unit: KiB
        run.elapsed:
          reference: 777.5183
          toleranceAbsolute: 116.627745
          unit: ms
        run.allocated:
          reference: 2265.664
          maximum: 2265.664
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.777518
          toleranceAbsolute: 1
          unit: ms
        run.per-invoke-allocated:
          reference: 2.266
          maximum: 2.266
          unit: KiB
```

---

## Test: engine performance mixed pipelines control flow and messages bytecode snapshot

This snapshot checks the generated Game Event Script Assembler representation for “engine performance mixed pipelines control flow and messages bytecode snapshot”.

### Case description

```yaml
gesBlock: case
id: snapshot-0005
kind: bytecodeSnapshot
level: scenario
sources:
  - name: "engine performance mixed pipelines control flow and messages.ges"
    program: main
```

### Source code under test

```ges
module EnginePerformance

predicate high(value as :number) be value >= 10

on Start(values) {
  let total be values[:filter value where value is high][:select value => value + 5%][:sum value => floor value]
  let average be values[:filter value where value is high][:select value => value + 5%][:average value => floor value]
  let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
  let firstBoosted be values[:filter value where value is high][:select value => value + 5%][:first]
  let scaled as :quantity(m) be 100m + 5%
  let folded be (15% + 15%) * 2
  let directOddScaled be values[:filter value where value mod 2 = 1][:select value => value * 2][:count value where value > 10]
  if scaled > 100m {
    let success be scaled + folded
    let divis be scaled ÷ folded
    let myHandler be Success(message, value)
    let myMessage be myHandler(message: 'hello', value: success)
    let myMessageDirect be Success(message: 'world', value: scaled)
  }
  for item from 1 to 16 {
    let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value => (value + item) * 2][:sum value => value]
  }
  let workTotal be values[:filter value where value >= 10][:select value => value + 5%][:select value => value * 2][:sum value => value]
  emit Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
}

```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: EnginePerformance
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "EnginePerformance"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: engine performance mixed pipelines control flow and messages.ges"

.segment source "engine performance mixed pipelines control flow and messages.ges"

module EnginePerformance

predicate high(value as :number) be value >= 10

on Start(values) {
  let total be values[:filter value where value is high][:select value => value + 5%][:sum value => floor value]
  let average be values[:filter value where value is high][:select value => value + 5%][:average value => floor value]
  let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
  let firstBoosted be values[:filter value where value is high][:select value => value + 5%][:first]
  let scaled as :quantity(m) be 100m + 5%
  let folded be (15% + 15%) * 2
  let directOddScaled be values[:filter value where value mod 2 = 1][:select value => value * 2][:count value where value > 10]
  if scaled > 100m {
    let success be scaled + folded
    let divis be scaled ÷ folded
    let myHandler be Success(message, value)
    let myMessage be myHandler(message: 'hello', value: success)
    let myMessageDirect be Success(message: 'world', value: scaled)
  }
  for item from 1 to 16 {
    let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value => (value + item) * 2][:sum value => value]
  }
  let workTotal be values[:filter value where value >= 10][:select value => value + 5%][:select value => value * 2][:sum value => value]
  emit Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
}

.region-end "Source: engine performance mixed pipelines control flow and messages.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_values:			.text "values"
T_Start:			.text "Start"
T_value:			.text "value"
T_high:				.text "high"
T_total:			.text "total"
T_average:			.text "average"
T_oddCount:			.text "oddCount"
T_directOddScaled:	.text "directOddScaled"
T_first:			.text "first"
T_scaled:			.text "scaled"
T_folded:			.text "folded"
T_workTotal:		.text "workTotal"
T_Done:				.text "Done"
T_Success:			.text "Success"
T_message:			.text "message"
T_hello:			.text "hello"
T_world:			.text "world"
T_EnginePerformance:	.text "EnginePerformance"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
U16_2:				.u16 [2]
U16_3:				.u16 [4, 5, 6, 7, 8, 9, 10, 11]
Shape_4:			.texts [T_Success, T_message, T_value] // "Success", "message", "value"
Args_5:				.registers [r16, r8]
Args_6:				.registers [r16, r5]
Args_7:				.registers [r1, r2, r3, r7, r4, r5, r6, r15]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[T_values] entry=Start // "Start(values)"
Predicate_high:		.bind Predicate id=0 name=T_high args=[T_value] entry=predicate_high // "high(value)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_total, T_average, T_oddCount, T_directOddScaled, T_first, T_scaled, T_folded, T_workTotal] // "Done(total, average, oddCount, directOddScaled, first, scaled, folded, workTotal)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "engine performance mixed pipelines control flow and messages.ges" 5 | on Start(values) {
Start:				 // handler Start(values)
					RegisterLocals #22

.source-line "engine performance mixed pipelines control flow and messages.ges" 6 |   let total be values[:filter value where value is high][:select value => value + 5%][:sum value => floor value]
					IteratorCreateOrJump r16, r0(values), Start_21
					LoadFalse r17
					LoadNothing r1(total)
Start_4:			IteratorNext r18, r16, Start_17
					StageRegister r18
					Call r19, predicate_high flags=NormalizeResultAsPredicate // predicate high(value)
					JumpIfNotTrue r19, Start_4
					LoadPercentage r19, #0.05
					Add r20, r18, r19
					Floor r18, r20
					JumpIfTrue r17, Start_15
					Move r1(total), r18
					LoadTrue r17
					Jump Start_4
Start_15:			Add r1(total), r1(total), r18
					Jump Start_4
Start_17:			IteratorClose r16
					JumpIfTrue r17, Start_22
					LoadInteger r1(total), #0
					Jump Start_22
Start_21:			LoadNothing r1(total)

.source-line "engine performance mixed pipelines control flow and messages.ges" 7 |   let average be values[:filter value where value is high][:select value => value + 5%][:average value => floor value]
Start_22:			IteratorCreateOrJump r16, r0(values), Start_47
					LoadInteger r2(average), #0
					LoadInteger r17, #1
					LoadFalse r20
					LoadNothing r18
Start_27:			IteratorNext r19, r16, Start_41
					StageRegister r19
					Call r21, predicate_high flags=NormalizeResultAsPredicate // predicate high(value)
					JumpIfNotTrue r21, Start_27
					LoadPercentage r21, #0.05
					Add r22, r19, r21
					Floor r19, r22
					Add r2(average), r2(average), r17
					JumpIfTrue r20, Start_39
					Move r18, r19
					LoadTrue r20
					Jump Start_27
Start_39:			Add r18, r18, r19
					Jump Start_27
Start_41:			IteratorClose r16
					JumpIfNotTrue r2(average), Start_45
					Divide r2(average), r18, r2(average)
					Jump Start_48
Start_45:			LoadNothing r2(average)
					Jump Start_48
Start_47:			LoadNothing r2(average)

.source-line "engine performance mixed pipelines control flow and messages.ges" 8 |   let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
Start_48:			IteratorCreateOrJump r16, r0(values), Start_61
					LoadInteger r3(oddCount), #0
					LoadInteger r17, #1
Start_51:			IteratorNext r20, r16, Start_59
					LoadInteger r18, #2
					Modulo r22, r20, r18
					LoadInteger r20, #1
					Equal r18, r22, r20
					JumpIfNotTrue r18, Start_51
					Add r3(oddCount), r3(oddCount), r17
					Jump Start_51
Start_59:			IteratorClose r16
					Jump Start_62
Start_61:			LoadNothing r3(oddCount)

.source-line "engine performance mixed pipelines control flow and messages.ges" 9 |   let firstBoosted be values[:filter value where value is high][:select value => value + 5%][:first]
Start_62:			IteratorCreateOrJump r16, r0(values), Start_74
					LoadNothing r4(firstBoosted)
Start_64:			IteratorNext r17, r16, Start_72
					StageRegister r17
					Call r22, predicate_high flags=NormalizeResultAsPredicate // predicate high(value)
					JumpIfNotTrue r22, Start_64
					LoadPercentage r22, #0.05
					Add r4(firstBoosted), r17, r22
					Jump Start_72
					Jump Start_64
Start_72:			IteratorClose r16
					Jump Start_75
Start_74:			LoadNothing r4(firstBoosted)

.source-line "engine performance mixed pipelines control flow and messages.ges" 10 |   let scaled as :quantity(m) be 100m + 5%
Start_75:			LoadInteger r5(scaled), #105, unit:meter
					CastUnit r5(scaled), r5(scaled), unit:meter

.source-line "engine performance mixed pipelines control flow and messages.ges" 11 |   let folded be (15% + 15%) * 2
					LoadFloat r6(folded), #0.6

.source-line "engine performance mixed pipelines control flow and messages.ges" 12 |   let directOddScaled be values[:filter value where value mod 2 = 1][:select value => value * 2][:count value where value > 10]
					IteratorCreateOrJump r16, r0(values), Start_96
					LoadInteger r7(directOddScaled), #0
					LoadInteger r17, #1
Start_81:			IteratorNext r22, r16, Start_94
					LoadInteger r20, #2
					Modulo r18, r22, r20
					LoadInteger r20, #1
					Equal r19, r18, r20
					JumpIfNotTrue r19, Start_81
					LoadInteger r18, #2
					Multiply r20, r22, r18
					LoadInteger r22, #10
					Greater r18, r20, r22
					JumpIfNotTrue r18, Start_81
					Add r7(directOddScaled), r7(directOddScaled), r17
					Jump Start_81
Start_94:			IteratorClose r16
					Jump Start_97
Start_96:			LoadNothing r7(directOddScaled)

.source-line "engine performance mixed pipelines control flow and messages.ges" 13 |   if scaled > 100m {
Start_97:			LoadInteger r16, #100, unit:meter
					Greater r17, r5(scaled), r16
					JumpIfNotTrue r17, Start_107

.source-line "engine performance mixed pipelines control flow and messages.ges" 14 |     let success be scaled + folded
					Add r8(success), r5(scaled), r6(folded)

.source-line "engine performance mixed pipelines control flow and messages.ges" 15 |     let divis be scaled ÷ folded
					Divide r9(divis), r5(scaled), r6(folded)

.source-line "engine performance mixed pipelines control flow and messages.ges" 16 |     let myHandler be Success(message, value)
					LoadHandler r10(myHandler), Shape_4 // "Success", "message", "value"

.source-line "engine performance mixed pipelines control flow and messages.ges" 17 |     let myMessage be myHandler(message: 'hello', value: success)
					LoadText r16, T_hello // "hello"
					BindHandler r11(myMessage), r10(myHandler), Args_5

.source-line "engine performance mixed pipelines control flow and messages.ges" 18 |     let myMessageDirect be Success(message: 'world', value: scaled)
					LoadText r16, T_world // "world"
					LoadMessage r12(myMessageDirect), Shape_4, Args_6 // "Success", "message", "value"

.source-line "engine performance mixed pipelines control flow and messages.ges" 20 |   for item from 1 to 16 {
Start_107:			CreateRangeIteratorShort r16, #1, #16, #1
Start_108:			IteratorNext r13(item), r16, Start_134

.source-line "engine performance mixed pipelines control flow and messages.ges" 21 |     let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value => (value + item) * 2][:sum value => value]
					IteratorCreateOrJump r17, r0(values), Start_132
					LoadFalse r20
					LoadNothing r14(foldedBucket)
Start_112:			IteratorNext r22, r17, Start_128
					Add r18, r22, r13(item)
					LoadInteger r19, #7
					Modulo r21, r18, r19
					LoadInteger r18, #0
					Greater r19, r21, r18
					JumpIfNotTrue r19, Start_112
					Add r21, r22, r13(item)
					LoadInteger r22, #2
					Multiply r18, r21, r22
					JumpIfTrue r20, Start_126
					Move r14(foldedBucket), r18
					LoadTrue r20
					Jump Start_112
Start_126:			Add r14(foldedBucket), r14(foldedBucket), r18
					Jump Start_112
Start_128:			IteratorClose r17
					JumpIfTrue r20, Start_108
					LoadInteger r14(foldedBucket), #0
					Jump Start_108
Start_132:			LoadNothing r14(foldedBucket)

.source-line "engine performance mixed pipelines control flow and messages.ges" 20 |   for item from 1 to 16 {
					Jump Start_108
Start_134:			IteratorClose r16

.source-line "engine performance mixed pipelines control flow and messages.ges" 23 |   let workTotal be values[:filter value where value >= 10][:select value => value + 5%][:select value => value * 2][:sum value => value]
					IteratorCreateOrJump r16, r0(values), Start_156
					LoadFalse r17
					LoadNothing r15(workTotal)
Start_138:			IteratorNext r20, r16, Start_152
					LoadInteger r21, #10
					GreaterOrEqual r22, r20, r21
					JumpIfNotTrue r22, Start_138
					LoadPercentage r21, #0.05
					Add r22, r20, r21
					LoadInteger r20, #2
					Multiply r21, r22, r20
					JumpIfTrue r17, Start_150
					Move r15(workTotal), r21
					LoadTrue r17
					Jump Start_138
Start_150:			Add r15(workTotal), r15(workTotal), r21
					Jump Start_138
Start_152:			IteratorClose r16
					JumpIfTrue r17, Start_157
					LoadInteger r15(workTotal), #0
					Jump Start_157
Start_156:			LoadNothing r15(workTotal)

.source-line "engine performance mixed pipelines control flow and messages.ges" 24 |   emit Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
Start_157:			EmitMessage Outbound_Done, Args_7 // "Done(total, average, oddCount, directOddScaled, first, scaled, folded, workTotal)"

.source-line "engine performance mixed pipelines control flow and messages.ges" 5 | on Start(values) {
					ReturnVoid

.source-line "engine performance mixed pipelines control flow and messages.ges" 3 | predicate high(value as :number) be value >= 10
predicate_high:		 // predicate high(value)
					RegisterLocals #2
					CastNumeric r0(value), r0(value)
					LoadInteger r1, #10
					GreaterOrEqual r2, r0(value), r1
					ReturnValue r2

.region-end "Code"
// -------------------------------------------------------------------------------

```
