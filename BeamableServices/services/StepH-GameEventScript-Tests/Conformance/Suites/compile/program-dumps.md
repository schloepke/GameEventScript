---
formatVersion: 1
suiteId: "compile.program-dumps"
title: "Portable Program Dumps"
kind: bytecodeSnapshot
level: scenario
categories: [conformance]
tags: [portable-api, assembler]
---

# Portable Program Dumps

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the canonical, source-aware Game Event Script Assembler representation produced for compiled programs.

---

## Test: functions predicates handlers and outbound bindings have a compact portable dump

This snapshot checks the generated Game Event Script Assembler representation for “functions predicates handlers and outbound bindings have a compact portable dump”.

### Case description

```yaml
gesBlock: case
id: compact-bindings
```

### Source code under test

```ges
module BinaryShape

function score(value) be value + 1
predicate high(value) be value > 3

on Start(value) {
  let rounded be floor value
  emit Done(score: score(value: value), high: value is high, rounded: rounded)
}
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: BinaryShape
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "BinaryShape"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: compile.program-dumps.compact-bindings.ges"

.segment source "compile.program-dumps.compact-bindings.ges"

module BinaryShape

function score(value) be value + 1
predicate high(value) be value > 3

on Start(value) {
  let rounded be floor value
  emit Done(score: score(value: value), high: value is high, rounded: rounded)
}

.region-end "Source: compile.program-dumps.compact-bindings.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_value:			.text "value"
T_Start:			.text "Start"
T_high:				.text "high"
T_score:			.text "score"
T_rounded:			.text "rounded"
T_Done:				.text "Done"
T_BinaryShape:		.text "BinaryShape"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
U16_2:				.u16 [3, 2, 4]
Args_3:				.registers [r2, r3, r1]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[T_value] entry=Start // "Start(value)"
Predicate_high:		.bind Predicate id=0 name=T_high args=[T_value] entry=predicate_high // "high(value)"
Function_score:		.bind Function id=0 name=T_score args=[T_value] entry=function_score // "score(value)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_score, T_high, T_rounded] // "Done(score, high, rounded)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "compile.program-dumps.compact-bindings.ges" 6 | on Start(value) {
Start:				 // handler Start(value)
					RegisterLocals #3

.source-line "compile.program-dumps.compact-bindings.ges" 7 |   let rounded be floor value
					Floor r1(rounded), r0(value)

.source-line "compile.program-dumps.compact-bindings.ges" 8 |   emit Done(score: score(value: value), high: value is high, rounded: rounded)
					StageRegister r0(value)
					Call r2, function_score // function score(value)
					StageRegister r0(value)
					Call r3, predicate_high flags=NormalizeResultAsPredicate // predicate high(value)
					EmitMessage Outbound_Done, Args_3 // "Done(score, high, rounded)"

.source-line "compile.program-dumps.compact-bindings.ges" 6 | on Start(value) {
					ReturnVoid

.source-line "compile.program-dumps.compact-bindings.ges" 4 | predicate high(value) be value > 3
predicate_high:		 // predicate high(value)
					RegisterLocals #2
					LoadInteger r1, #3
					Greater r2, r0(value), r1
					ReturnValue r2

.source-line "compile.program-dumps.compact-bindings.ges" 3 | function score(value) be value + 1
function_score:		 // function score(value)
					RegisterLocals #2
					LoadInteger r1, #1
					Add r2, r0(value), r1
					ReturnValue r2

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: message-name handlers and embedded source use the portable dump syntax

This snapshot checks the generated Game Event Script Assembler representation for “message-name handlers and embedded source use the portable dump syntax”.

### Case description

```yaml
gesBlock: case
id: message-name-and-source
```

### Source code under test

```ges
module BinaryMessageNameDump

on Ping as message {
  emit Done(value: message.name)
}
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: BinaryMessageNameDump
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "BinaryMessageNameDump"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: compile.program-dumps.message-name-and-source.ges"

.segment source "compile.program-dumps.message-name-and-source.ges"

module BinaryMessageNameDump

on Ping as message {
  emit Done(value: message.name)
}

.region-end "Source: compile.program-dumps.message-name-and-source.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_message:			.text "message"
T_Ping:				.text "Ping"
T_value:			.text "value"
T_Done:				.text "Done"
T_name:				.text "name"
T_BinaryMessageNameDump:	.text "BinaryMessageNameDump"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
U16_2:				.u16 [2]
Args_3:				.registers [r1]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Ping:		.bind MessageNameHandler id=0 name=T_Ping args=[T_message] entry=Ping // "Ping as message"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_value] // "Done(value)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "compile.program-dumps.message-name-and-source.ges" 3 | on Ping as message {
Ping:				 // handler Ping as message
					RegisterLocals #1
					Cast r0(message), r0(message), Message

.source-line "compile.program-dumps.message-name-and-source.ges" 4 |   emit Done(value: message.name)
					MemberAccess r1, T_name, r0(message) // "name"
					EmitMessage Outbound_Done, Args_3 // "Done(value)"

.source-line "compile.program-dumps.message-name-and-source.ges" 3 | on Ping as message {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```
