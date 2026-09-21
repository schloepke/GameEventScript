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
module binaryshape

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
//  Module: binaryshape
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "binaryshape"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: compile.program-dumps.compact-bindings.ges"

.segment source "compile.program-dumps.compact-bindings.ges"

module binaryshape

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
T_binaryshape:		.text "binaryshape"

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
module binarymessagenamedump

on Ping as message {
  emit Done(value: message.name)
}
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: binarymessagenamedump
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "binarymessagenamedump"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: compile.program-dumps.message-name-and-source.ges"

.segment source "compile.program-dumps.message-name-and-source.ges"

module binarymessagenamedump

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
T_binarymessagenamedump:	.text "binarymessagenamedump"

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

---

## Test: parse-literal

This snapshot checks the dedicated literal-reading instruction and its register operands without optional source metadata.

### Case description

```yaml
gesBlock: case
id: parse-literal
compile:
  debugInfo: []
```

### Source code under test

```ges
module parsefixture
on Start(value) { emit Done(value: parse value) }
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: parsefixture
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "parsefixture"
.program-version 0

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_value:			.text "value"
T_Start:			.text "Start"
T_Done:				.text "Done"
T_parsefixture:		.text "parsefixture"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
Args_2:				.registers [r1]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[T_value] entry=Start // "Start(value)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_value] // "Done(value)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

// compiler-generated
Start:				 // handler Start(value)
					RegisterLocals #1
					ParseLiteral r1, r0
					EmitMessage Outbound_Done, Args_2 // "Done(value)"
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: floating immediates use portable exponent spelling

This snapshot covers positive and negative scientific exponents in Number operands.

### Case description

```yaml
gesBlock: case
id: float-exponents
kind: programBinary
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-gesa-float-exponents
  resourceId: gesb-v1.gesa-float-exponents
  relativePath: GesbV1/gesa-float-exponents.gesb
  sha256: 5CDD1E4C6AFFA0FF76CCF17AC09B715368673F1D846F6F09F1921CED0164A57A
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
```

### Source code under test

```ges
module gesadump.floatexponents

on Start {
  emit Done(small: 0.00001, large: 100000000000000000000.0, negativeSmall: '-0.00001' as :Number, negativeLarge: '-1e20' as :Number)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 5CDD1E4C6AFFA0FF76CCF17AC09B715368673F1D846F6F09F1921CED0164A57A
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: gesadump.floatexponents
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "gesadump.floatexponents"
.program-version 0

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_small:			.text "small"
T_large:			.text "large"
T_negativeSmall:	.text "negativeSmall"
T_negativeLarge:	.text "negativeLarge"
T_Done:				.text "Done"
T_gesadump_floatexponents:	.text "gesadump.floatexponents"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2, 3, 4]
Args_2:				.registers [r0, r1, r2, r3]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_small, T_large, T_negativeSmall, T_negativeLarge] // "Done(small, large, negativeSmall, negativeLarge)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

// compiler-generated
Start:				 // handler Start()
					RegisterLocals #4
					LoadFloat r0, #1e-5
					LoadFloat r1, #1e20
					LoadFloat r2, #-1e-5
					LoadFloat r3, #-1e20
					EmitMessage Outbound_Done, Args_2 // "Done(small, large, negativeSmall, negativeLarge)"
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: floating immediates share decimal and scientific boundaries

This snapshot uses Percentage ratios to retain Binary64 operands even for integral values and zero.

### Case description

```yaml
gesBlock: case
id: float-notation-boundaries
kind: programBinary
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-gesa-float-notation-boundaries
  resourceId: gesb-v1.gesa-float-notation-boundaries
  relativePath: GesbV1/gesa-float-notation-boundaries.gesb
  sha256: 412BB3C67B01BFB20F20C5CB38CE1BD1CAFEC9F898E905A2A9AE73794D45F2CA
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
```

### Source code under test

```ges
module gesadump.floatnotationboundaries

on Start {
  emit Done(belowLower: 0.009999999999999999%, lower: 0.01%, belowUpper: 999999999999999800%, upper: 1000000000000000000%, aboveUpper: 1000000000000000200%, negativeUpper: -1000000000000000000%, zero: 0%, negativeZero: -0%, fraction: 12.5%)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: 412BB3C67B01BFB20F20C5CB38CE1BD1CAFEC9F898E905A2A9AE73794D45F2CA
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: gesadump.floatnotationboundaries
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "gesadump.floatnotationboundaries"
.program-version 0

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_belowLower:		.text "belowLower"
T_lower:			.text "lower"
T_belowUpper:		.text "belowUpper"
T_upper:			.text "upper"
T_aboveUpper:		.text "aboveUpper"
T_negativeUpper:	.text "negativeUpper"
T_zero:				.text "zero"
T_negativeZero:		.text "negativeZero"
T_fraction:			.text "fraction"
T_Done:				.text "Done"
T_gesadump_floatnotationboundaries:	.text "gesadump.floatnotationboundaries"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2, 3, 4, 5, 6, 7, 8, 9]
Args_2:				.registers [r0, r1, r2, r3, r4, r6, r5, r8, r7]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_belowLower, T_lower, T_belowUpper, T_upper, T_aboveUpper, T_negativeUpper, T_zero, T_negativeZero, T_fraction] // "Done(belowLower, lower, belowUpper, upper, aboveUpper, negativeUpper, zero, negativeZero, fraction)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

// compiler-generated
Start:				 // handler Start()
					RegisterLocals #9
					LoadPercentage r0, #9.999999999999999e-5
					LoadPercentage r1, #0.0001
					LoadPercentage r2, #9999999999999998
					LoadPercentage r3, #1e16
					LoadPercentage r4, #1.0000000000000002e16
					LoadPercentage r5, #1e16
					Negate r6, r5
					LoadPercentage r5, #0
					LoadPercentage r7, #0
					Negate r8, r7
					LoadPercentage r7, #0.125
					EmitMessage Outbound_Done, Args_2 // "Done(belowLower, lower, belowUpper, upper, aboveUpper, negativeUpper, zero, negativeZero, fraction)"
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: floating immediates preserve subnormal normal and infinite values

This snapshot covers the smallest subnormal, the normal/subnormal boundary, the largest finite value and positive infinity.

### Case description

```yaml
gesBlock: case
id: float-extremes
kind: programBinary
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-gesa-float-extremes
  resourceId: gesb-v1.gesa-float-extremes
  relativePath: GesbV1/gesa-float-extremes.gesb
  sha256: FFC414931C843F37A821798E4A9A4FBF92517E9A2B6A445B9CA1D1B3C5D1E107
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
```

### Source code under test

```ges
module gesadump.floatextremes

on Start {
  emit Done(least: 0.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005, subnormal: 0.00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002225073858507201, normal: 0.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000022250738585072014, greatest: 179769313486231570000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000.0, positiveInfinity: infinity)
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: FFC414931C843F37A821798E4A9A4FBF92517E9A2B6A445B9CA1D1B3C5D1E107
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: gesadump.floatextremes
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "gesadump.floatextremes"
.program-version 0

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_least:			.text "least"
T_subnormal:		.text "subnormal"
T_normal:			.text "normal"
T_greatest:			.text "greatest"
T_positiveInfinity:	.text "positiveInfinity"
T_Done:				.text "Done"
T_gesadump_floatextremes:	.text "gesadump.floatextremes"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2, 3, 4, 5]
Args_2:				.registers [r0, r1, r2, r3, r4]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_least, T_subnormal, T_normal, T_greatest, T_positiveInfinity] // "Done(least, subnormal, normal, greatest, positiveInfinity)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

// compiler-generated
Start:				 // handler Start()
					RegisterLocals #5
					LoadFloat r0, #5e-324
					LoadFloat r1, #2.225073858507201e-308
					LoadFloat r2, #2.2250738585072014e-308
					LoadFloat r3, #1.7976931348623157e308
					LoadFloat r4, #Infinity
					EmitMessage Outbound_Done, Args_2 // "Done(least, subnormal, normal, greatest, positiveInfinity)"
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: staged floating immediates follow the same canonical spelling

This snapshot checks float and Percentage operands staged for a function call.

### Case description

```yaml
gesBlock: case
id: float-staging
kind: programBinary
compile:
  debugInfo: []
binaryFixture:
  id: gesb-v1-gesa-float-staging
  resourceId: gesb-v1.gesa-float-staging
  relativePath: GesbV1/gesa-float-staging.gesb
  sha256: B139194C7246C3EB790F587FAE6ADAAF638C4C8A34BB786B0EE9FED32D06DDD0
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 0
```

### Source code under test

```ges
module gesadump.floatstaging

function identity(_ value) be value

on Start {
  emit Done(small: identity(0.00001), large: identity(100000000000000000000.0), percentage: identity(1000000000000000000%))
}
```

### Expectation

```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  rewriteSha256: B139194C7246C3EB790F587FAE6ADAAF638C4C8A34BB786B0EE9FED32D06DDD0
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: gesadump.floatstaging
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "gesadump.floatstaging"
.program-version 0

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_1:				.text "_"
T_identity:			.text "identity"
T_small:			.text "small"
T_large:			.text "large"
T_percentage:		.text "percentage"
T_Done:				.text "Done"
T_gesadump_floatstaging:	.text "gesadump.floatstaging"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1]
U16_2:				.u16 [3, 4, 5]
Args_3:				.registers [r0, r1, r2]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Function_identity:	.bind Function id=0 name=T_identity args=[T_1] entry=function_identity // "identity(_)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_small, T_large, T_percentage] // "Done(small, large, percentage)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

// compiler-generated
Start:				 // handler Start()
					RegisterLocals #3
					StageFloat #1e-5
					Call r0, function_identity // function identity(_)
					StageFloat #1e20
					Call r1, function_identity // function identity(_)
					StagePercentage #1e16
					Call r2, function_identity // function identity(_)
					EmitMessage Outbound_Done, Args_3 // "Done(small, large, percentage)"
					ReturnVoid

function_identity:	 // function identity(_)
					RegisterLocals #0
					ReturnValue r0

.region-end "Code"
// -------------------------------------------------------------------------------

```

---

## Test: native compilation uses canonical floating immediates

This snapshot verifies the source compiler and dumper together for small and large Number literals; the fixture-backed cases above isolate all remaining operand forms from compiler optimization choices.

### Case description

```yaml
gesBlock: case
id: float-source
compile:
  debugInfo: []
  binaryRoundTrip: true
```

### Source code under test

```ges
module gesadump.source

on Start {
  emit Done(small: 0.00001, large: 100000000000000000000.0)
}
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: gesadump.source
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "gesadump.source"
.program-version 0

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_Start:			.text "Start"
T_small:			.text "small"
T_large:			.text "large"
T_Done:				.text "Done"
T_gesadump_source:	.text "gesadump.source"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 []
U16_1:				.u16 [1, 2]
Args_2:				.registers [r0, r1]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[] entry=Start // "Start()"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_small, T_large] // "Done(small, large)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

// compiler-generated
Start:				 // handler Start()
					RegisterLocals #2
					LoadFloat r0, #1e-5
					LoadFloat r1, #1e20
					EmitMessage Outbound_Done, Args_2 // "Done(small, large)"
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```
