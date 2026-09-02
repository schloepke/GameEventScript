---
formatVersion: 1
suiteId: "compile.build-errors"
title: "CompileBuildErrors"
categories: [conformance]
---

# CompileBuildErrors

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies semantic build diagnostics for programs that parse successfully but violate compiler rules.

---

## Test: direct recursive function call is rejected

This negative compiler case exercises “direct recursive function call is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: compileError
level: scenario
sources:
  - name: "direct recursive function call is rejected.ges"
    program: main
```

### Source code under test

```ges
function repeat(value) be repeat(value: value)

on Start {
  let result be repeat(value: 1)
}

```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.cyclicCallGraph"
```

---

## Test: missing predicate call fails module build

This negative compiler case exercises “missing predicate call fails module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: compileError
level: scenario
sources:
  - name: "missing predicate call fails module build.ges"
    program: main
```

### Source code under test

```ges
on Start(unit) {
  let x be missingRule(unit)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.missingCallable"
  symbol: "missingRule"
```

---

## Test: missing function call fails module build

This negative compiler case exercises “missing function call fails module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: compileError
level: scenario
sources:
  - name: "missing function call fails module build.ges"
    program: main
```

### Source code under test

```ges
on Start(units) {
  let x be missingSelect(units)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.missingCallable"
  symbol: "missingSelect"
```

---

## Test: unknown identifier fails compilation

This negative compiler case exercises “unknown identifier fails compilation” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: compileError
level: scenario
sources:
  - name: "unknown identifier fails compilation.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be missing
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unresolvedSymbol"
```

---

## Test: block scopes do not leak variables

This negative compiler case exercises “block scopes do not leak variables” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: compileError
level: scenario
sources:
  - name: "block scopes do not leak variables.ges"
    program: main
```

### Source code under test

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

### Expectation

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unresolvedSymbol"
```

---

## Test: quantity rejects unknown unit names

This negative compiler case exercises “quantity rejects unknown unit names” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: compileError
level: scenario
sources:
  - name: "quantity rejects unknown unit names.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be 100 as :quantity(foo)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unsupportedConstruct"
```

---

## Test: guarded choice rejects tag otherwise

This negative compiler case exercises “guarded choice rejects tag otherwise” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: compileError
level: scenario
sources:
  - name: "guarded choice rejects tag otherwise.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be 1 when true, #otherwise 0
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: invalid predicate predicate requires unary predicate

This negative compiler case exercises “invalid predicate predicate requires unary predicate” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: compileError
level: scenario
sources:
  - name: "invalid predicate predicate requires unary predicate.ges"
    program: main
```

### Source code under test

```ges
function wounded(_ unit) be unit.hp < unit.maxHp

on Start(unit) {
  let x be unit is wounded
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidPredicate"
  symbol: "wounded"
  symbolKind: "predicate"
```

---

## Test: predicate result rejects implicit numeric coercion

This negative compiler case exercises “predicate result rejects implicit numeric coercion” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: compileError
level: scenario
sources:
  - name: "predicate result rejects implicit numeric coercion.ges"
    program: main
```

### Source code under test

```ges
predicate numericResult(_ value) be value * 2

on Start {
  emit Done
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidPredicate"
  symbol: "numericResult"
  symbolKind: "predicate"
```

---

## Test: predicate result rejects unknown member truthiness

This negative compiler case exercises “predicate result rejects unknown member truthiness” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: compileError
level: scenario
sources:
  - name: "predicate result rejects unknown member truthiness.ges"
    program: main
```

### Source code under test

```ges
predicate alive(_ unit) be unit.alive

on Start {
  emit Done
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidPredicate"
  symbol: "alive"
  symbolKind: "predicate"
```

---

## Test: wrong predicate arity fails module build

This negative compiler case exercises “wrong predicate arity fails module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: compileError
level: scenario
sources:
  - name: "wrong predicate arity fails module build.ges"
    program: main
```

### Source code under test

```ges
predicate wounded(_ unit) be unit.hp < unit.maxHp

on Start(unit) {
  let x be wounded(unit, unit)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.wrongPredicateArity"
  symbol: "wounded"
  symbolKind: "predicate"
```

---

## Test: wrong function arity fails module build

This negative compiler case exercises “wrong function arity fails module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: compileError
level: scenario
sources:
  - name: "wrong function arity fails module build.ges"
    program: main
```

### Source code under test

```ges
function woundedUnits(_ units) be units[:filter unit where unit.hp < unit.maxHp]

on Start(units) {
  let x be woundedUnits()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.wrongFunctionArity"
  symbol: "woundedUnits"
  symbolKind: "function"
```

---

## Test: duplicate local variable fails module build

This negative compiler case exercises “duplicate local variable fails module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: compileError
level: scenario
sources:
  - name: "duplicate local variable fails module build.ges"
    program: main
```

### Source code under test

```ges
module DuplicateVariables
on Start {
  let x be 10
  let x be 20
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateVariable"
  symbol: "x"
  symbolKind: "variable"
  programName: "DuplicateVariables"
```

---

## Test: module build errors preserve parser source range

This negative compiler case exercises “module build errors preserve parser source range” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: compileError
level: scenario
sources:
  - name: "location.ges"
    program: main
```

### Source code under test

```ges
module Location

on Start {
  let value be 1
  let value be 2
}
```

### Expectation

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

---

## Test: handler parameter cannot be redeclared as local variable

This negative compiler case exercises “handler parameter cannot be redeclared as local variable” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: compileError
level: scenario
sources:
  - name: "handler parameter cannot be redeclared as local variable.ges"
    program: main
```

### Source code under test

```ges
module DuplicateParameterVariable
on Start(value) {
  let value be 10
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateVariable"
  symbol: "value"
  symbolKind: "variable"
  programName: "DuplicateParameterVariable"
```

---

## Test: duplicate variable inside block fails module build

This negative compiler case exercises “duplicate variable inside block fails module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: compileError
level: scenario
sources:
  - name: "duplicate variable inside block fails module build.ges"
    program: main
```

### Source code under test

```ges
module DuplicateBlockVariables
on Start {
  if true {
    let x be 10
    let x be 20
  }
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateVariable"
  symbol: "x"
  symbolKind: "variable"
  programName: "DuplicateBlockVariables"
```

---

## Test: duplicate predicate parameters fail module build

This negative compiler case exercises “duplicate predicate parameters fail module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0017
kind: compileError
level: scenario
sources:
  - name: "duplicate predicate parameters fail module build.ges"
    program: main
```

### Source code under test

```ges
module DuplicatePredicateParameters
predicate wounded(unit, unit) be unit.hp < unit.maxHp
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateDefinitionParameter"
  symbol: "wounded"
  symbolKind: "predicate"
  programName: "DuplicatePredicateParameters"
```

---

## Test: duplicate function parameters fail module build

This negative compiler case exercises “duplicate function parameters fail module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0018
kind: compileError
level: scenario
sources:
  - name: "duplicate function parameters fail module build.ges"
    program: main
```

### Source code under test

```ges
module DuplicateFunctionParameters
function wounded(units, units) be units[:filter unit where unit.hp < unit.maxHp]
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateDefinitionParameter"
  symbol: "wounded"
  symbolKind: "function"
  programName: "DuplicateFunctionParameters"
```

---

## Test: duplicate handler bind arguments fail module build

This negative compiler case exercises “duplicate handler bind arguments fail module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0019
kind: compileError
level: scenario
sources:
  - name: "duplicate handler bind arguments fail module build.ges"
    program: main
```

### Source code under test

```ges
module DuplicateHandlerBindArgs
on Start(unit, target, myHandler) {
  emit myHandler(unit: unit, unit: target)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicatePublishArgument"
  symbol: "myHandler"
  programName: "DuplicateHandlerBindArgs"
```

---

## Test: duplicate handler literal parameters fail module build

This negative compiler case exercises “duplicate handler literal parameters fail module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0020
kind: compileError
level: scenario
sources:
  - name: "duplicate handler literal parameters fail module build.ges"
    program: main
```

### Source code under test

```ges
module DuplicateHandlerLiteralParams
on Start(unit, target) {
  let shoot as :handler be Shoot(unit, unit)
  emit Done
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateHandlerParameter"
  symbol: "Shoot"
  symbolKind: "handler"
  programName: "DuplicateHandlerLiteralParams"
```

---

## Test: predicate and function definitions cannot share a name

This negative compiler case exercises “predicate and function definitions cannot share a name” and verifies the required portable diagnostic.

### Case description

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

### Source code under test

```ges
module A
predicate wounded(_ unit) be unit.hp < unit.maxHp
```

```ges
module B
function wounded(_ unit) be unit.hp < unit.maxHp
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.predicateFunctionConflict"
  symbol: "wounded"
```

---

## Test: vector constructor requires positional label order

This negative compiler case exercises “vector constructor requires positional label order” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0022
kind: compileError
level: scenario
sources:
  - name: "vector constructor requires positional label order.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let position be :vector(y: 2, x: 1)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "vector"
  symbolKind: "type"
```

---

## Test: vector constructor rejects too many components

This negative compiler case exercises “vector constructor rejects too many components” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0023
kind: compileError
level: scenario
sources:
  - name: "vector constructor rejects too many components.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let position be :vector(1, 2, 3, 4)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "vector"
  symbolKind: "type"
```

---

## Test: vector constructor rejects component labels out of order

This negative compiler case exercises “vector constructor rejects component labels out of order” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0024
kind: compileError
level: scenario
sources:
  - name: "vector constructor rejects component labels out of order.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let point be :vector(z: 1, y: 2)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "vector"
  symbolKind: "type"
```

---

## Test: ref type has been removed

This negative compiler case exercises “ref type has been removed” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0025
kind: compileError
level: scenario
sources:
  - name: "ref type has been removed.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let unitRef be :ref(id: 'unit-42')
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "ref"
  symbolKind: "type"
```

---

## Test: point constructor requires positional label order

This negative compiler case exercises “point constructor requires positional label order” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0026
kind: compileError
level: scenario
sources:
  - name: "point constructor requires positional label order.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let position be :point(y: 2, x: 1)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "point"
  symbolKind: "type"
```

---

## Test: identifier rejects attached numeric suffix

This negative compiler case exercises “identifier rejects attached numeric suffix” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0027
kind: compileError
level: scenario
sources:
  - name: "identifier rejects attached numeric suffix.ges"
    program: main
```

### Source code under test

```ges
on Start(x) {
  let result be x2
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: identifier rejects embedded number

This negative compiler case exercises “identifier rejects embedded number” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0028
kind: compileError
level: scenario
sources:
  - name: "identifier rejects embedded number.ges"
    program: main
```

### Source code under test

```ges
on Start(x, y) {
  let result be x2y
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: identifier rejects numeric text without suffix marker

This negative compiler case exercises “identifier rejects numeric text without suffix marker” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0029
kind: compileError
level: scenario
sources:
  - name: "identifier rejects numeric text without suffix marker.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let player22 be 1
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: identifier suffix requires digits

This negative compiler case exercises “identifier suffix requires digits” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0030
kind: compileError
level: scenario
sources:
  - name: "identifier suffix requires digits.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let player_ be 1
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: identifier suffix rejects leading zero

This negative compiler case exercises “identifier suffix rejects leading zero” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0031
kind: compileError
level: scenario
sources:
  - name: "identifier suffix rejects leading zero.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let player_01 be 1
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: identifier suffix must end after digits

This negative compiler case exercises “identifier suffix must end after digits” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0032
kind: compileError
level: scenario
sources:
  - name: "identifier suffix must end after digits.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let player_1a be 1
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: message rejects numeric suffix

This negative compiler case exercises “message rejects numeric suffix” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0033
kind: compileError
level: scenario
sources:
  - name: "message rejects numeric suffix.ges"
    program: main
```

### Source code under test

```ges
on Fire_1 {
  emit Done
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: tag rejects unseparated numeric suffix

This negative compiler case exercises “tag rejects unseparated numeric suffix” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0034
kind: compileError
level: scenario
sources:
  - name: "tag rejects unseparated numeric suffix.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done with #tag1
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: vector dimension tag is not valid syntax

This negative compiler case exercises “vector dimension tag is not valid syntax” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0035
kind: compileError
level: scenario
sources:
  - name: "vector dimension tag is not valid syntax.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be :vector2(1, 2)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: custom type constructor requires labels

This negative compiler case exercises “custom type constructor requires labels” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0036
kind: compileError
level: scenario
sources:
  - name: "custom type constructor requires labels.ges"
    program: main
```

### Source code under test

```ges
record :gauge as {
  current: :number,
  maximum: :number
}

on Start {
  let hp be :gauge(10, 20)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "gauge"
  symbolKind: "type"
```

---

## Test: custom type constructor rejects unknown fields

This negative compiler case exercises “custom type constructor rejects unknown fields” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0037
kind: compileError
level: scenario
sources:
  - name: "custom type constructor rejects unknown fields.ges"
    program: main
```

### Source code under test

```ges
record :gauge as {
  current: :number,
  maximum: :number
}

on Start {
  let hp be :gauge(current: 10, unknown: 20)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "gauge"
  symbolKind: "type"
```

---

## Test: seeded random unit seed fails module build

This negative compiler case exercises “seeded random unit seed fails module build” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0038
kind: compileError
level: scenario
sources:
  - name: "seeded random unit seed fails module build.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be random with 5m 1
  emit Done(value: value)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
```

---

## Test: seeded random unknown seed requires explicit integer cast

This negative compiler case exercises “seeded random unknown seed requires explicit integer cast” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0039
kind: compileError
level: scenario
sources:
  - name: "seeded random unknown seed requires explicit integer cast.ges"
    program: main
```

### Source code under test

```ges
on Start(seed) {
  let value be random with seed 1
  emit Done(value: value)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
```

---

## Test: optional cast is rejected

This negative compiler case exercises “optional cast is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0040
kind: compileError
level: scenario
sources:
  - name: "optional cast is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let invalid be value as :optional
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "optional"
  symbolKind: "type"
```

---

## Test: optional type check is rejected

This negative compiler case exercises “optional type check is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0041
kind: compileError
level: scenario
sources:
  - name: "optional type check is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let invalid be value is :optional
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "optional"
  symbolKind: "type"
```

---

## Test: set cast is rejected

This negative compiler case exercises “set cast is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0042
kind: compileError
level: scenario
sources:
  - name: "set cast is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let invalid be value as :set
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "set"
  symbolKind: "type"
```

---

## Test: set type check is rejected

This negative compiler case exercises “set type check is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0043
kind: compileError
level: scenario
sources:
  - name: "set type check is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let invalid be value is :set
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "set"
  symbolKind: "type"
```

---

## Test: undeliverable endpoint rejects normal parameter syntax

This negative compiler case exercises “undeliverable endpoint rejects normal parameter syntax” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0044
kind: compileError
level: scenario
sources:
  - name: "undeliverable endpoint rejects normal parameter syntax.ges"
    program: main
```

### Source code under test

```ges
on undeliverable(message as :message) {
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidMessageCase"
  symbol: "undeliverable"
  symbolKind: "handler"
```

---

## Test: legacy prefix tag math operator is rejected

This negative compiler case exercises “legacy prefix tag math operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0045
kind: compileError
level: scenario
sources:
  - name: "legacy prefix tag math operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be :abs -5
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: legacy numeric tag constant is rejected

This negative compiler case exercises “legacy numeric tag constant is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0046
kind: compileError
level: scenario
sources:
  - name: "legacy numeric tag constant is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be :pi
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: legacy integer standard extension is rejected

This negative compiler case exercises “legacy integer standard extension is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0047
kind: compileError
level: scenario
sources:
  - name: "legacy integer standard extension is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let x be :integer.floor value
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: illegal token recovery continues at next handler

This negative compiler case exercises “illegal token recovery continues at next handler” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0048
kind: compileError
level: scenario
sources:
  - name: "illegal token recovery continues at next handler.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let first be x2
}

on Done {
  let second be y2
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```
