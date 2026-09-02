---
formatVersion: 1
suiteId: "compile.build-errors"
title: "CompileBuildErrors"
categories: [conformance]
tags: [migrated-json-v1]
---

# CompileBuildErrors

Mechanically migrated from the former JSON conformance corpus.

## Test: direct recursive function call is rejected

```yaml
gesBlock: case
id: case-0001
kind: compileError
level: scenario
sources:
  - name: "direct recursive function call is rejected.ges"
    program: main
```

```ges
function repeat(value) be repeat(value: value)

on Start {
  let result be repeat(value: 1)
}

```

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.cyclicCallGraph"
```

## Test: missing predicate call fails module build

```yaml
gesBlock: case
id: case-0002
kind: compileError
level: scenario
sources:
  - name: "missing predicate call fails module build.ges"
    program: main
```

```ges
on Start(unit) {
  let x be missingRule(unit)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.missingCallable"
  symbol: "missingRule"
```

## Test: missing function call fails module build

```yaml
gesBlock: case
id: case-0003
kind: compileError
level: scenario
sources:
  - name: "missing function call fails module build.ges"
    program: main
```

```ges
on Start(units) {
  let x be missingSelect(units)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.missingCallable"
  symbol: "missingSelect"
```

## Test: unknown identifier fails compilation

```yaml
gesBlock: case
id: case-0004
kind: compileError
level: scenario
sources:
  - name: "unknown identifier fails compilation.ges"
    program: main
```

```ges
on Start {
  let x be missing
}
```

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unresolvedSymbol"
```

## Test: block scopes do not leak variables

```yaml
gesBlock: case
id: case-0005
kind: compileError
level: scenario
sources:
  - name: "block scopes do not leak variables.ges"
    program: main
```

```ges
on Start(items, seed as :number) {
  if true {
    let fromIf be 10
  }

  for item in items {
    let fromLoop be item
  }

  random with seed {
    let fromRandom be random from 1 to 6
    emit Inner(valuePresent: fromRandom has value)
  }

  if fromIf has value emit IfLeak
  if fromLoop has value emit LoopLeak
  if fromRandom has value emit RandomLeak
}
```

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unresolvedSymbol"
```

## Test: quantity rejects unknown unit names

```yaml
gesBlock: case
id: case-0006
kind: compileError
level: scenario
sources:
  - name: "quantity rejects unknown unit names.ges"
    program: main
```

```ges
on Start {
  let x be 100 as :quantity(foo)
}
```

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unsupportedConstruct"
```

## Test: guarded choice rejects tag otherwise

```yaml
gesBlock: case
id: case-0007
kind: compileError
level: scenario
sources:
  - name: "guarded choice rejects tag otherwise.ges"
    program: main
```

```ges
on Start {
  let x be 1 when true, #otherwise 0
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: invalid predicate predicate requires unary predicate

```yaml
gesBlock: case
id: case-0008
kind: compileError
level: scenario
sources:
  - name: "invalid predicate predicate requires unary predicate.ges"
    program: main
```

```ges
function wounded(_ unit) be unit.hp < unit.maxHp

on Start(unit) {
  let x be unit is wounded
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidPredicate"
  symbol: "wounded"
  symbolKind: "predicate"
```

## Test: predicate result rejects implicit numeric coercion

```yaml
gesBlock: case
id: case-0009
kind: compileError
level: scenario
sources:
  - name: "predicate result rejects implicit numeric coercion.ges"
    program: main
```

```ges
predicate numericResult(_ value) be value * 2

on Start {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidPredicate"
  symbol: "numericResult"
  symbolKind: "predicate"
```

## Test: predicate result rejects unknown member truthiness

```yaml
gesBlock: case
id: case-0010
kind: compileError
level: scenario
sources:
  - name: "predicate result rejects unknown member truthiness.ges"
    program: main
```

```ges
predicate alive(_ unit) be unit.alive

on Start {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidPredicate"
  symbol: "alive"
  symbolKind: "predicate"
```

## Test: wrong predicate arity fails module build

```yaml
gesBlock: case
id: case-0011
kind: compileError
level: scenario
sources:
  - name: "wrong predicate arity fails module build.ges"
    program: main
```

```ges
predicate wounded(_ unit) be unit.hp < unit.maxHp

on Start(unit) {
  let x be wounded(unit, unit)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.wrongPredicateArity"
  symbol: "wounded"
  symbolKind: "predicate"
```

## Test: wrong function arity fails module build

```yaml
gesBlock: case
id: case-0012
kind: compileError
level: scenario
sources:
  - name: "wrong function arity fails module build.ges"
    program: main
```

```ges
function woundedUnits(_ units) be units[:filter unit where unit.hp < unit.maxHp]

on Start(units) {
  let x be woundedUnits()
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.wrongFunctionArity"
  symbol: "woundedUnits"
  symbolKind: "function"
```

## Test: duplicate local variable fails module build

```yaml
gesBlock: case
id: case-0013
kind: compileError
level: scenario
sources:
  - name: "duplicate local variable fails module build.ges"
    program: main
```

```ges
module DuplicateVariables
on Start {
  let x be 10
  let x be 20
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateVariable"
  symbol: "x"
  symbolKind: "variable"
  programName: "DuplicateVariables"
```

## Test: module build errors preserve parser source range

```yaml
gesBlock: case
id: case-0014
kind: compileError
level: scenario
sources:
  - name: "location.ges"
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
  phase: "validate"
  code: "validate.duplicateVariable"
  symbol: "value"
  symbolKind: "variable"
  programName: "Location"
  sourceName: "location.ges"
  line: 5
  column: 3
```

## Test: handler parameter cannot be redeclared as local variable

```yaml
gesBlock: case
id: case-0015
kind: compileError
level: scenario
sources:
  - name: "handler parameter cannot be redeclared as local variable.ges"
    program: main
```

```ges
module DuplicateParameterVariable
on Start(value) {
  let value be 10
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateVariable"
  symbol: "value"
  symbolKind: "variable"
  programName: "DuplicateParameterVariable"
```

## Test: duplicate variable inside block fails module build

```yaml
gesBlock: case
id: case-0016
kind: compileError
level: scenario
sources:
  - name: "duplicate variable inside block fails module build.ges"
    program: main
```

```ges
module DuplicateBlockVariables
on Start {
  if true {
    let x be 10
    let x be 20
  }
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateVariable"
  symbol: "x"
  symbolKind: "variable"
  programName: "DuplicateBlockVariables"
```

## Test: duplicate predicate parameters fail module build

```yaml
gesBlock: case
id: case-0017
kind: compileError
level: scenario
sources:
  - name: "duplicate predicate parameters fail module build.ges"
    program: main
```

```ges
module DuplicatePredicateParameters
predicate wounded(unit, unit) be unit.hp < unit.maxHp
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateDefinitionParameter"
  symbol: "wounded"
  symbolKind: "predicate"
  programName: "DuplicatePredicateParameters"
```

## Test: duplicate function parameters fail module build

```yaml
gesBlock: case
id: case-0018
kind: compileError
level: scenario
sources:
  - name: "duplicate function parameters fail module build.ges"
    program: main
```

```ges
module DuplicateFunctionParameters
function wounded(units, units) be units[:filter unit where unit.hp < unit.maxHp]
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateDefinitionParameter"
  symbol: "wounded"
  symbolKind: "function"
  programName: "DuplicateFunctionParameters"
```

## Test: duplicate handler bind arguments fail module build

```yaml
gesBlock: case
id: case-0019
kind: compileError
level: scenario
sources:
  - name: "duplicate handler bind arguments fail module build.ges"
    program: main
```

```ges
module DuplicateHandlerBindArgs
on Start(unit, target, myHandler) {
  emit myHandler(unit: unit, unit: target)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicatePublishArgument"
  symbol: "myHandler"
  programName: "DuplicateHandlerBindArgs"
```

## Test: duplicate handler literal parameters fail module build

```yaml
gesBlock: case
id: case-0020
kind: compileError
level: scenario
sources:
  - name: "duplicate handler literal parameters fail module build.ges"
    program: main
```

```ges
module DuplicateHandlerLiteralParams
on Start(unit, target) {
  let shoot as :handler be Shoot(unit, unit)
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateHandlerParameter"
  symbol: "Shoot"
  symbolKind: "handler"
  programName: "DuplicateHandlerLiteralParams"
```

## Test: predicate and function definitions cannot share a name

```yaml
gesBlock: case
id: case-0021
kind: compileError
level: scenario
sources:
  - name: "a.ges"
    program: main
  - name: "b.ges"
    program: main
```

```ges
module A
predicate wounded(_ unit) be unit.hp < unit.maxHp
```

```ges
module B
function wounded(_ unit) be unit.hp < unit.maxHp
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.predicateFunctionConflict"
  symbol: "wounded"
```

## Test: vector constructor requires positional label order

```yaml
gesBlock: case
id: case-0022
kind: compileError
level: scenario
sources:
  - name: "vector constructor requires positional label order.ges"
    program: main
```

```ges
on Start {
  let position be :vector(y: 2, x: 1)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "vector"
  symbolKind: "type"
```

## Test: vector constructor rejects too many components

```yaml
gesBlock: case
id: case-0023
kind: compileError
level: scenario
sources:
  - name: "vector constructor rejects too many components.ges"
    program: main
```

```ges
on Start {
  let position be :vector(1, 2, 3, 4)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "vector"
  symbolKind: "type"
```

## Test: vector constructor rejects component labels out of order

```yaml
gesBlock: case
id: case-0024
kind: compileError
level: scenario
sources:
  - name: "vector constructor rejects component labels out of order.ges"
    program: main
```

```ges
on Start {
  let point be :vector(z: 1, y: 2)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "vector"
  symbolKind: "type"
```

## Test: ref type has been removed

```yaml
gesBlock: case
id: case-0025
kind: compileError
level: scenario
sources:
  - name: "ref type has been removed.ges"
    program: main
```

```ges
on Start {
  let unitRef be :ref(id: 'unit-42')
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "ref"
  symbolKind: "type"
```

## Test: point constructor requires positional label order

```yaml
gesBlock: case
id: case-0026
kind: compileError
level: scenario
sources:
  - name: "point constructor requires positional label order.ges"
    program: main
```

```ges
on Start {
  let position be :point(y: 2, x: 1)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "point"
  symbolKind: "type"
```

## Test: identifier rejects attached numeric suffix

```yaml
gesBlock: case
id: case-0027
kind: compileError
level: scenario
sources:
  - name: "identifier rejects attached numeric suffix.ges"
    program: main
```

```ges
on Start(x) {
  let result be x2
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: identifier rejects embedded number

```yaml
gesBlock: case
id: case-0028
kind: compileError
level: scenario
sources:
  - name: "identifier rejects embedded number.ges"
    program: main
```

```ges
on Start(x, y) {
  let result be x2y
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: identifier rejects numeric text without suffix marker

```yaml
gesBlock: case
id: case-0029
kind: compileError
level: scenario
sources:
  - name: "identifier rejects numeric text without suffix marker.ges"
    program: main
```

```ges
on Start {
  let player22 be 1
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: identifier suffix requires digits

```yaml
gesBlock: case
id: case-0030
kind: compileError
level: scenario
sources:
  - name: "identifier suffix requires digits.ges"
    program: main
```

```ges
on Start {
  let player_ be 1
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: identifier suffix rejects leading zero

```yaml
gesBlock: case
id: case-0031
kind: compileError
level: scenario
sources:
  - name: "identifier suffix rejects leading zero.ges"
    program: main
```

```ges
on Start {
  let player_01 be 1
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: identifier suffix must end after digits

```yaml
gesBlock: case
id: case-0032
kind: compileError
level: scenario
sources:
  - name: "identifier suffix must end after digits.ges"
    program: main
```

```ges
on Start {
  let player_1a be 1
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: message rejects numeric suffix

```yaml
gesBlock: case
id: case-0033
kind: compileError
level: scenario
sources:
  - name: "message rejects numeric suffix.ges"
    program: main
```

```ges
on Fire_1 {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: tag rejects unseparated numeric suffix

```yaml
gesBlock: case
id: case-0034
kind: compileError
level: scenario
sources:
  - name: "tag rejects unseparated numeric suffix.ges"
    program: main
```

```ges
on Start {
  emit Done with #tag1
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: vector dimension tag is not valid syntax

```yaml
gesBlock: case
id: case-0035
kind: compileError
level: scenario
sources:
  - name: "vector dimension tag is not valid syntax.ges"
    program: main
```

```ges
on Start {
  let value be :vector2(1, 2)
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: custom type constructor requires labels

```yaml
gesBlock: case
id: case-0036
kind: compileError
level: scenario
sources:
  - name: "custom type constructor requires labels.ges"
    program: main
```

```ges
record :gauge as {
  current: :number,
  maximum: :number
}

on Start {
  let hp be :gauge(10, 20)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "gauge"
  symbolKind: "type"
```

## Test: custom type constructor rejects unknown fields

```yaml
gesBlock: case
id: case-0037
kind: compileError
level: scenario
sources:
  - name: "custom type constructor rejects unknown fields.ges"
    program: main
```

```ges
record :gauge as {
  current: :number,
  maximum: :number
}

on Start {
  let hp be :gauge(current: 10, unknown: 20)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "gauge"
  symbolKind: "type"
```

## Test: seeded random unit seed fails module build

```yaml
gesBlock: case
id: case-0038
kind: compileError
level: scenario
sources:
  - name: "seeded random unit seed fails module build.ges"
    program: main
```

```ges
on Start {
  let value be random with 5m 1
  emit Done(value: value)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
```

## Test: seeded random unknown seed requires explicit integer cast

```yaml
gesBlock: case
id: case-0039
kind: compileError
level: scenario
sources:
  - name: "seeded random unknown seed requires explicit integer cast.ges"
    program: main
```

```ges
on Start(seed) {
  let value be random with seed 1
  emit Done(value: value)
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
```

## Test: optional cast is rejected

```yaml
gesBlock: case
id: case-0040
kind: compileError
level: scenario
sources:
  - name: "optional cast is rejected.ges"
    program: main
```

```ges
on Start(value) {
  let invalid be value as :optional
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "optional"
  symbolKind: "type"
```

## Test: optional type check is rejected

```yaml
gesBlock: case
id: case-0041
kind: compileError
level: scenario
sources:
  - name: "optional type check is rejected.ges"
    program: main
```

```ges
on Start(value) {
  let invalid be value is :optional
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "optional"
  symbolKind: "type"
```

## Test: set cast is rejected

```yaml
gesBlock: case
id: case-0042
kind: compileError
level: scenario
sources:
  - name: "set cast is rejected.ges"
    program: main
```

```ges
on Start(value) {
  let invalid be value as :set
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "set"
  symbolKind: "type"
```

## Test: set type check is rejected

```yaml
gesBlock: case
id: case-0043
kind: compileError
level: scenario
sources:
  - name: "set type check is rejected.ges"
    program: main
```

```ges
on Start(value) {
  let invalid be value is :set
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "set"
  symbolKind: "type"
```

## Test: undeliverable endpoint rejects normal parameter syntax

```yaml
gesBlock: case
id: case-0044
kind: compileError
level: scenario
sources:
  - name: "undeliverable endpoint rejects normal parameter syntax.ges"
    program: main
```

```ges
on undeliverable(message as :message) {
}
```

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidMessageCase"
  symbol: "undeliverable"
  symbolKind: "handler"
```

## Test: legacy prefix tag math operator is rejected

```yaml
gesBlock: case
id: case-0045
kind: compileError
level: scenario
sources:
  - name: "legacy prefix tag math operator is rejected.ges"
    program: main
```

```ges
on Start {
  let x be :abs -5
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy numeric tag constant is rejected

```yaml
gesBlock: case
id: case-0046
kind: compileError
level: scenario
sources:
  - name: "legacy numeric tag constant is rejected.ges"
    program: main
```

```ges
on Start {
  let x be :pi
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy integer standard extension is rejected

```yaml
gesBlock: case
id: case-0047
kind: compileError
level: scenario
sources:
  - name: "legacy integer standard extension is rejected.ges"
    program: main
```

```ges
on Start(value) {
  let x be :integer.floor value
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: illegal token recovery continues at next handler

```yaml
gesBlock: case
id: case-0048
kind: compileError
level: scenario
sources:
  - name: "illegal token recovery continues at next handler.ges"
    program: main
```

```ges
on Start {
  let first be x2
}

on Done {
  let second be y2
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```
