---
formatVersion: 1
suiteId: "compile.loop-invariants"
title: "Compile Loop Invariants"
categories: [conformance]
---

# Compile Loop Invariants

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies that compilers move pure, total and deterministic loop-invariant instructions in front of their
loop. Constant loads and arithmetic whose inputs are final before the loop run once per loop entry. Random draws,
message operations and loop-dependent values stay in the loop, and observable results are unchanged.

---

## Test: invariant constants and arithmetic move in front of a fold

This snapshot checks that the constant operands and the products of loop-invariant parameters leave the fold body,
while the accumulator recurrence stays inside it.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: bytecodeSnapshot
level: atomic
sources:
  - name: "invariant constants and arithmetic move in front of a fold.ges"
    program: main
```

### Source code under test

```ges
module foldinvariants

function score(_ item as :Number, _ batch as :Number) be
  :Range(1, 4)[:fold acc be item + batch, step => (acc * 3 + item * 5 + step) mod 101]

on Start(value) {
  emit Done(result: score(value, value))
}

```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: foldinvariants
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "foldinvariants"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: invariant constants and arithmetic move in front of a fold.ges"

.segment source "invariant constants and arithmetic move in front of a fold.ges"

module foldinvariants

function score(_ item as :Number, _ batch as :Number) be
  :Range(1, 4)[:fold acc be item + batch, step => (acc * 3 + item * 5 + step) mod 101]

on Start(value) {
  emit Done(result: score(value, value))
}

.region-end "Source: invariant constants and arithmetic move in front of a fold.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_value:			.text "value"
T_Start:			.text "Start"
T_2:				.text "_"
T_score:			.text "score"
T_result:			.text "result"
T_Done:				.text "Done"
T_range:			.text "range"
T_foldinvariants:	.text "foldinvariants"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
Names_2:			.texts [T_2, T_2] // "_", "_"
U16_3:				.u16 [4]
Args_4:				.registers [r1]
Args_5:				.registers [r2, r3]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[T_value] entry=Start // "Start(value)"
Function_score:		.bind Function id=0 name=T_score args=[T_2, T_2] entry=function_score // "score(_, _)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_result] // "Done(result)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "invariant constants and arithmetic move in front of a fold.ges" 6 | on Start(value) {
Start:				 // handler Start(value)
					RegisterLocals #1

.source-line "invariant constants and arithmetic move in front of a fold.ges" 7 |   emit Done(result: score(value, value))
					StageRegister r0(value)
					StageRegister r0(value)
					Call r1, function_score // function score(_, _)
					EmitMessage Outbound_Done, Args_4 // "Done(result)"

.source-line "invariant constants and arithmetic move in front of a fold.ges" 6 | on Start(value) {
					ReturnVoid

.source-line "invariant constants and arithmetic move in front of a fold.ges" 3 | function score(_ item as :Number, _ batch as :Number) be
function_score:		 // function score(_, _)
					RegisterLocals #8
					CastNumeric r0(_), r0(_)
					CastNumeric r1(_), r1(_)

.source-line "invariant constants and arithmetic move in front of a fold.ges" 4 |   :Range(1, 4)[:fold acc be item + batch, step => (acc * 3 + item * 5 + step) mod 101]
					LoadInteger r2, #1
					LoadInteger r3, #4
					ConstructData r4, T_range, Args_5, Names_2 // "range", "_", "_"
					Add r2, r0(_), r1(_)
					IteratorCreateOrJump r3, r4, function_score_27
					LoadInteger r4, #3
					LoadInteger r5, #5
					Multiply r6, r0(_), r5
					LoadInteger r5, #101
function_score_18:	IteratorNext r7, r3, function_score_24
					Multiply r8, r2, r4
					Add r9, r8, r6
					Add r8, r9, r7
					Modulo r2, r8, r5
					Jump function_score_18
function_score_24:	IteratorClose r3
					Move r3, r2
					Jump function_score_28
function_score_27:	LoadNothing r3

.source-line "invariant constants and arithmetic move in front of a fold.ges" 3 | function score(_ item as :Number, _ batch as :Number) be
function_score_28:	ReturnValue r3

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: nested loops hoist constants to the outermost loop

This snapshot checks that constants used by an inner pipeline leave both loops, while expressions that read the outer
loop variable stay inside the outer loop.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: bytecodeSnapshot
level: atomic
sources:
  - name: "nested loops hoist constants to the outermost loop.ges"
    program: main
```

### Source code under test

```ges
module nestedinvariants

on Start(values) {
  for item from 1 to 3 {
    let bucket be values[:select value => (value + item) * 4 + 9][:sum value => value]
    emit Bucket(value: bucket)
  }
}

```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: nestedinvariants
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "nestedinvariants"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: nested loops hoist constants to the outermost loop.ges"

.segment source "nested loops hoist constants to the outermost loop.ges"

module nestedinvariants

on Start(values) {
  for item from 1 to 3 {
    let bucket be values[:select value => (value + item) * 4 + 9][:sum value => value]
    emit Bucket(value: bucket)
  }
}

.region-end "Source: nested loops hoist constants to the outermost loop.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_values:			.text "values"
T_Start:			.text "Start"
T_value:			.text "value"
T_Bucket:			.text "Bucket"
T_nestedinvariants:	.text "nestedinvariants"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
Args_2:				.registers [r2]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[T_values] entry=Start // "Start(values)"
Outbound_Bucket:	.bind OutboundMessage id=0 name=T_Bucket args=[T_value] // "Bucket(value)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "nested loops hoist constants to the outermost loop.ges" 3 | on Start(values) {
Start:				 // handler Start(values)
					RegisterLocals #9

.source-line "nested loops hoist constants to the outermost loop.ges" 4 |   for item from 1 to 3 {
					CreateRangeIteratorShort r3, #1, #3, #1

.source-line "nested loops hoist constants to the outermost loop.ges" 5 |     let bucket be values[:select value => (value + item) * 4 + 9][:sum value => value]
					LoadInteger r4, #4
					LoadInteger r5, #9

.source-line "nested loops hoist constants to the outermost loop.ges" 4 |   for item from 1 to 3 {
Start_4:			IteratorNext r1(item), r3, Start_25

.source-line "nested loops hoist constants to the outermost loop.ges" 5 |     let bucket be values[:select value => (value + item) * 4 + 9][:sum value => value]
					IteratorCreateOrJump r6, r0(values), Start_22
					LoadFalse r7
					LoadNothing r2(bucket)
Start_8:			IteratorNext r8, r6, Start_18
					Add r9, r8, r1(item)
					Multiply r8, r9, r4
					Add r9, r8, r5
					JumpIfTrue r7, Start_16
					Move r2(bucket), r9 mode=Numeric
					LoadTrue r7
					Jump Start_8
Start_16:			Add r2(bucket), r2(bucket), r9 mode=Numeric
					Jump Start_8
Start_18:			IteratorClose r6
					JumpIfTrue r7, Start_23
					LoadInteger r2(bucket), #0
					Jump Start_23
Start_22:			LoadNothing r2(bucket)

.source-line "nested loops hoist constants to the outermost loop.ges" 6 |     emit Bucket(value: bucket)
Start_23:			EmitMessage Outbound_Bucket, Args_2 // "Bucket(value)"

.source-line "nested loops hoist constants to the outermost loop.ges" 4 |   for item from 1 to 3 {
					Jump Start_4
Start_25:			IteratorClose r3

.source-line "nested loops hoist constants to the outermost loop.ges" 3 | on Start(values) {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: random draws and messages stay inside loops

This snapshot checks that the bounds of a random draw and a constant factor may leave the loop, while the draw itself,
the message and the loop-dependent product stay inside it.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: bytecodeSnapshot
level: atomic
sources:
  - name: "random draws and messages stay inside loops.ges"
    program: main
```

### Source code under test

```ges
module effectsinloops

on Start {
  for item from 1 to 3 {
    emit Tick(value: random from 1 to 6, scaled: item * 10)
  }
}

```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: effectsinloops
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "effectsinloops"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: random draws and messages stay inside loops.ges"

.segment source "random draws and messages stay inside loops.ges"

module effectsinloops

on Start {
  for item from 1 to 3 {
    emit Tick(value: random from 1 to 6, scaled: item * 10)
  }
}

.region-end "Source: random draws and messages stay inside loops.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_value:			.text "value"
T_scaled:			.text "scaled"
T_Tick:				.text "Tick"
T_effectsinloops:	.text "effectsinloops"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2]
Args_2:				.registers [r5, r6]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Outbound_Tick:		.bind OutboundMessage id=0 name=T_Tick args=[T_value, T_scaled] // "Tick(value, scaled)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "random draws and messages stay inside loops.ges" 3 | on Start {
Start:				 // handler Start()
					RegisterLocals #7

.source-line "random draws and messages stay inside loops.ges" 4 |   for item from 1 to 3 {
					CreateRangeIteratorShort r1, #1, #3, #1

.source-line "random draws and messages stay inside loops.ges" 5 |     emit Tick(value: random from 1 to 6, scaled: item * 10)
					LoadInteger r2, #1
					LoadInteger r3, #6
					LoadInteger r4, #10

.source-line "random draws and messages stay inside loops.ges" 4 |   for item from 1 to 3 {
Start_5:			IteratorNext r0(item), r1, Start_10

.source-line "random draws and messages stay inside loops.ges" 5 |     emit Tick(value: random from 1 to 6, scaled: item * 10)
					RandomTake r5, r2, r3
					Multiply r6, r0(item), r4
					EmitMessage Outbound_Tick, Args_2 // "Tick(value, scaled)"

.source-line "random draws and messages stay inside loops.ges" 4 |   for item from 1 to 3 {
					Jump Start_5
Start_10:			IteratorClose r1

.source-line "random draws and messages stay inside loops.ges" 3 | on Start {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: hoisting preserves empty, nested and seeded random loop results

This runtime case checks that loops which execute zero times, nested pipelines, fold recurrences and seeded random
draws produce the same values with loop-invariant instructions in front of their loops.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
sources:
  - name: "hoisting preserves empty nested and seeded random loop results.ges"
    program: main
```

### Source code under test

```ges
function score(_ item as :Number, _ batch as :Number) be
  :Range(1, 4)[:fold acc be item + batch, step => (acc * 3 + item * 5 + step) mod 101]

on Start(values) {
  let none be values[:filter value where value > 100][:select value => value * 7 + 3][:sum value => value]
  let nested be :Range(1, 3)[:fold total be 0, item => total + values[:select value => (value + item) * 4 + 9][:sum value => value]]
  let drawn be random with 11 (:Range(1, 3)[:select item => item * 10 + random from 1 to 6])
  emit Done(score: score(3, 2), none: none, nested: nested, drawn: drawn)
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
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "3"
              - type: ":Number.int64"
                value: "4"
    local:
      - name: "Done"
        args:
          - name: "score"
            value:
              type: ":Number.int64"
              value: "53"
          - name: "none"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "nested"
            value:
              type: ":Number.int64"
              value: "324"
          - name: "drawn"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "12"
                - type: ":Number.int64"
                  value: "24"
                - type: ":Number.int64"
                  value: "32"
```
