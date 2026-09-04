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
on Start(items, seed as :Number) {
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
  let x be 100 as :Quantity(foo)
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
module duplicatevariables
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
  programName: "duplicatevariables"
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
module location

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
  programName: "location"
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
module duplicateparametervariable
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
  programName: "duplicateparametervariable"
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
module duplicateblockvariables
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
  programName: "duplicateblockvariables"
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
module duplicatepredicateparameters
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
  programName: "duplicatepredicateparameters"
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
module duplicatefunctionparameters
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
  programName: "duplicatefunctionparameters"
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
module duplicatehandlerbindargs
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
  programName: "duplicatehandlerbindargs"
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
module duplicatehandlerliteralparams
on Start(unit, target) {
  let shoot be (Shoot(unit, unit)) as :Handler
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
  programName: "duplicatehandlerliteralparams"
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
module a
predicate wounded(_ unit) be unit.hp < unit.maxHp
```

```ges
module b
function wounded(unit, amount) be amount
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
  let position be :Vector(y: 2, x: 1)
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
  let position be :Vector(1, 2, 3, 4)
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
  let point be :Vector(z: 1, y: 2)
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
  let unitRef be :Ref(id: 'unit-42')
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "Ref"
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
  let position be :Point(y: 2, x: 1)
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

## Test: callable names reject variable subscript suffixes

This negative compiler case verifies that the `_number` suffix is reserved for variable bindings and cannot name a callable.

### Case description

```yaml
gesBlock: case
id: case-0027
kind: compileError
level: scenario
sources:
  - name: "callable names reject variable subscript suffixes.ges"
    program: main
```

### Source code under test

```ges
function compute_2(value) be value
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidIdentifierCase"
  symbol: "compute_2"
```

---

## Test: duplicate callable signatures ignore local names and types

This negative compiler case verifies that callable overload identity consists only of the callable name and ordered external labels; local parameter names and declared types cannot create a distinct overload.

### Case description

```yaml
gesBlock: case
id: case-0049
kind: compileError
level: atomic
sources:
  - name: "duplicate callable signatures ignore local names and types.ges"
    program: main
```

### Source code under test

```ges
function convert(_ numberValue as :Number) be numberValue
function convert(_ textValue as :Text) be textValue
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.duplicateFunction"
  symbol: "convert"
```

---

## Test: predicate shorthand rejects ambiguous unary overloads

This negative compiler case verifies that `value is predicateName` cannot choose between multiple unary predicate signatures because the shorthand supplies no external argument label.

### Case description

```yaml
gesBlock: case
id: case-0050
kind: compileError
level: atomic
sources:
  - name: "predicate shorthand rejects ambiguous unary overloads.ges"
    program: main
```

### Source code under test

```ges
predicate acceptable(value) be value > 0
predicate acceptable(amount) be amount < 100

on Start(value) {
  let result be value is acceptable
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidPredicate"
  symbol: "acceptable"
```

---

## Test: nested local bindings cannot shadow enclosing locals

This negative compiler case verifies that a local declared in a nested block cannot reuse the name of a still-visible local from an enclosing lexical scope.

### Case description

```yaml
gesBlock: case
id: case-0051
kind: compileError
level: atomic
sources:
  - name: "nested local bindings cannot shadow enclosing locals.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let result be 10
  if true {
    let result be 20
  }
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.shadowedVariable"
  symbol: "result"
```

---

## Test: loop variables cannot shadow routine parameters

This negative compiler case verifies that loop binders cannot hide a parameter that remains visible in the enclosing routine scope.

### Case description

```yaml
gesBlock: case
id: case-0052
kind: compileError
level: atomic
sources:
  - name: "loop variables cannot shadow routine parameters.ges"
    program: main
```

### Source code under test

```ges
on Start(item, items) {
  for item in items {
    emit Seen(value: item)
  }
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.shadowedVariable"
  symbol: "item"
```

---

## Test: collection binders cannot shadow visible locals

This negative compiler case verifies that identifiers introduced by collection selectors follow the same no-shadowing rule as statement-level bindings.

### Case description

```yaml
gesBlock: case
id: case-0053
kind: compileError
level: atomic
sources:
  - name: "collection binders cannot shadow visible locals.ges"
    program: main
```

### Source code under test

```ges
on Start(values) {
  let item be 10
  let selected be values[:filter item where item > 0]
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.shadowedVariable"
  symbol: "item"
```

---

## Test: record fields reject variable subscript suffixes

This negative compiler case verifies that record field names cannot use the variable-only `_number` suffix.

### Case description

```yaml
gesBlock: case
id: case-0028
kind: compileError
level: scenario
sources:
  - name: "record fields reject variable subscript suffixes.ges"
    program: main
```

### Source code under test

```ges
record :Unit as {
  health_2: :Number
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidIdentifierCase"
  symbol: "health_2"
```

---

## Test: message labels reject variable subscript suffixes

This negative compiler case verifies that message argument labels cannot use the variable-only `_number` suffix.

### Case description

```yaml
gesBlock: case
id: case-0029
kind: compileError
level: scenario
sources:
  - name: "message labels reject variable subscript suffixes.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value_2: 1)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidIdentifierCase"
  symbol: "Done"
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

## Test: message rejects variable subscript suffix

This negative compiler case verifies that message names may contain digits but never the variable-only `_number` suffix.

### Case description

```yaml
gesBlock: case
id: case-0033
kind: compileError
level: scenario
sources:
  - name: "message rejects variable subscript suffix.ges"
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

## Test: tags reject variable subscript suffixes

This negative compiler case verifies that tags allow digits but do not accept the variable-only `_number` suffix.

### Case description

```yaml
gesBlock: case
id: case-0034
kind: compileError
level: scenario
sources:
  - name: "tags reject variable subscript suffixes.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done with #tag_1
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
record :Gauge as {
  current: :Number,
  maximum: :Number
}

on Start {
  let hp be :Gauge(10, 20)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "Gauge"
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
record :Gauge as {
  current: :Number,
  maximum: :Number
}

on Start {
  let hp be :Gauge(current: 10, unknown: 20)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  symbol: "Gauge"
  symbolKind: "type"
```

---

## Test: seeded random rejects a unit-bearing seed

This compiler case verifies that a unit-bearing seed is rejected unless the script explicitly removes or converts its unit before using it as a random seed.

### Case description

```yaml
gesBlock: case
id: case-0038
kind: compileError
level: scenario
sources:
  - name: "seeded random rejects a unit-bearing seed.ges"
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
  symbol: "Number"
  symbolKind: "type"
```

---

## Test: seeded random requires an explicit conversion for an unknown seed

This compiler case verifies that an externally supplied or otherwise statically unknown seed must explicitly state the intended numeric conversion.

### Case description

```yaml
gesBlock: case
id: case-0039
kind: compileError
level: scenario
sources:
  - name: "seeded random requires an explicit conversion for an unknown seed.ges"
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
  symbol: "Number"
  symbolKind: "type"
```

---

## Test: typed let declarations are rejected

This negative compiler case verifies that a type conversion belongs to the value expression and that `let name as :Type be value` is not part of the language.

### Case description

```yaml
gesBlock: case
id: case-0040
kind: compileError
level: scenario
sources:
  - name: "typed let declarations are rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let invalid as :Number be value
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

## Test: lowercase cast types are rejected

This negative compiler case verifies that every source-level type reference starts with an uppercase ASCII letter.

### Case description

```yaml
gesBlock: case
id: case-0041
kind: compileError
level: scenario
sources:
  - name: "lowercase cast types are rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let invalid be value as :number
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

## Test: lowercase checked types are rejected

This negative compiler case verifies that type checks use PascalCase type references.

### Case description

```yaml
gesBlock: case
id: case-0042
kind: compileError
level: scenario
sources:
  - name: "lowercase checked types are rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let invalid be value is :text
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

## Test: module identifiers reject uppercase letters

This negative compiler case verifies that module path components must begin with lowercase ASCII letters.

### Case description

```yaml
gesBlock: case
id: case-0043
kind: compileError
level: scenario
sources:
  - name: "module identifiers reject uppercase letters.ges"
    program: main
```

### Source code under test

```ges
module Invalid
on Start { }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
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
on undeliverable(message as :Message) {
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
  let first be player_01
}

on Done {
  let second be value_01
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```
