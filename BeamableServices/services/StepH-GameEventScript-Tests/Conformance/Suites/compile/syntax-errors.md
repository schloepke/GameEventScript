---
formatVersion: 1
suiteId: "compile.syntax-errors"
title: "CompileSyntaxErrors"
categories: [conformance]
---

# CompileSyntaxErrors

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies portable syntax diagnostics and recovery for malformed or deliberately unsupported source forms.

---

## Test: missing expression in let statement fails syntax analysis

This negative compiler case exercises “missing expression in let statement fails syntax analysis” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: compileError
level: scenario
sources:
  - name: "missing expression in let statement fails syntax analysis.ges"
    program: main
```

### Source code under test

```ges
on Broken {
  let x be
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

## Test: malformed predicate parameter fails syntax analysis

This negative compiler case exercises “malformed predicate parameter fails syntax analysis” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: compileError
level: scenario
sources:
  - name: "malformed predicate parameter fails syntax analysis.ges"
    program: main
```

### Source code under test

```ges
module BrokenCombat
predicate unitIsDead(un%it) be unit[hp] <= 0
on FireAtUnit(unit) {
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

## Test: illegal token inside expression fails through compile interface

This negative compiler case exercises “illegal token inside expression fails through compile interface” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: compileError
level: scenario
sources:
  - name: "illegal token inside expression fails through compile interface.ges"
    program: main
```

### Source code under test

```ges
on Broken {
  let value be hello%&some
  let other be @
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

## Test: legacy percent modulo operator is rejected

This negative compiler case exercises “legacy percent modulo operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: compileError
level: scenario
sources:
  - name: "legacy percent modulo operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Broken {
  let value be 10 % 3
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

## Test: type tags are case sensitive

This negative compiler case exercises “type tags are case sensitive” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: compileError
level: scenario
sources:
  - name: "type tags are case sensitive.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let numberValue as :Float be value
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

## Test: legacy external handler syntax is rejected

This negative compiler case exercises “legacy external handler syntax is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: compileError
level: scenario
sources:
  - name: "legacy external handler syntax is rejected.ges"
    program: main
```

### Source code under test

```ges
external on Notify(playerId, points)
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: sort selector requires explicit direction

This negative compiler case exercises “sort selector requires explicit direction” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: compileError
level: scenario
sources:
  - name: "sort selector requires explicit direction.ges"
    program: main
```

### Source code under test

```ges
on Sorted(items) {
  let values be items[:sort]
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

## Test: legacy untagged type declaration is rejected

This negative compiler case exercises “legacy untagged type declaration is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: compileError
level: scenario
sources:
  - name: "legacy untagged type declaration is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let score as double be value
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

## Test: legacy untagged unary form is rejected

This negative compiler case exercises “legacy untagged unary form is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: compileError
level: scenario
sources:
  - name: "legacy untagged unary form is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(values) {
  let size be len values
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

## Test: legacy random tag operator is rejected

This negative compiler case exercises “legacy random tag operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: compileError
level: scenario
sources:
  - name: "legacy random tag operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be :random from 1 to 6
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

## Test: legacy clamp tag operator is rejected

This negative compiler case exercises “legacy clamp tag operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: compileError
level: scenario
sources:
  - name: "legacy clamp tag operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be :clamp 5 between 1 and 10
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

## Test: legacy min tag operator is rejected

This negative compiler case exercises “legacy min tag operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: compileError
level: scenario
sources:
  - name: "legacy min tag operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be :min of 1 and 2
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

## Test: legacy max tag operator is rejected

This negative compiler case exercises “legacy max tag operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: compileError
level: scenario
sources:
  - name: "legacy max tag operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be :max of 1 and 2
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

## Test: legacy zip tag operator is rejected

This negative compiler case exercises “legacy zip tag operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: compileError
level: scenario
sources:
  - name: "legacy zip tag operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be [1] :zip [2]
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

## Test: legacy dice tag roll is rejected

This negative compiler case exercises “legacy dice tag roll is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: compileError
level: scenario
sources:
  - name: "legacy dice tag roll is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be :dice 4d6
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

## Test: legacy default tag operator is rejected

This negative compiler case exercises “legacy default tag operator is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: compileError
level: scenario
sources:
  - name: "legacy default tag operator is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  let result be value :default 10
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

## Test: legacy emit tag literal form is rejected

This negative compiler case exercises “legacy emit tag literal form is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0017
kind: compileError
level: scenario
sources:
  - name: "legacy emit tag literal form is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done with :radio
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

## Test: legacy handler matching tag literal form is rejected

This negative compiler case exercises “legacy handler matching tag literal form is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0018
kind: compileError
level: scenario
sources:
  - name: "legacy handler matching tag literal form is rejected.ges"
    program: main
```

### Source code under test

```ges
on Ping matching :radio {
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

## Test: legacy handler without tag literal form is rejected

This negative compiler case exercises “legacy handler without tag literal form is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0019
kind: compileError
level: scenario
sources:
  - name: "legacy handler without tag literal form is rejected.ges"
    program: main
```

### Source code under test

```ges
on Ping without :blocked {
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

## Test: legacy floor prefix builtin is rejected

This negative compiler case exercises “legacy floor prefix builtin is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0020
kind: compileError
level: scenario
sources:
  - name: "legacy floor prefix builtin is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be #floor 10.4
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

## Test: legacy wrapDegree prefix builtin is rejected

This negative compiler case exercises “legacy wrapDegree prefix builtin is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0021
kind: compileError
level: scenario
sources:
  - name: "legacy wrapDegree prefix builtin is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let value be #wrapDegree 370°
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

## Test: direct range is rejected after in for loops

This negative compiler case exercises “direct range is rejected after in for loops” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0022
kind: compileError
level: scenario
sources:
  - name: "direct range is rejected after in for loops.ges"
    program: main
```

### Source code under test

```ges
on Start {
  for item in from 1 to 10 emit Seen(value: item)
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

## Test: direct range is rejected after in generated collections

This negative compiler case exercises “direct range is rejected after in generated collections” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0023
kind: compileError
level: scenario
sources:
  - name: "direct range is rejected after in generated collections.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let values be :list[:select item in from 1 to 5 => item]
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

## Test: seeded random blocks cannot be value expressions

This negative compiler case exercises “seeded random blocks cannot be value expressions” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0024
kind: compileError
level: scenario
sources:
  - name: "seeded random blocks cannot be value expressions.ges"
    program: main
```

### Source code under test

```ges
on Start(seed) {
  let values be random with seed {
    emit Done
  }
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

## Test: drop selector requires a supported scope

This negative compiler case exercises “drop selector requires a supported scope” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0025
kind: compileError
level: scenario
sources:
  - name: "drop selector requires a supported scope.ges"
    program: main
```

### Source code under test

```ges
on Broken {
  let x be roll dice 4d6[:drop middle 1]
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

## Test: dice counts must be positive integers

This negative compiler case exercises “dice counts must be positive integers” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0026
kind: compileError
level: scenario
sources:
  - name: "dice counts must be positive integers.ges"
    program: main
```

### Source code under test

```ges
on Broken {
  let x be roll dice 0d6
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

## Test: handler headers reject handler tag prefix

This negative compiler case exercises “handler headers reject handler tag prefix” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0027
kind: compileError
level: scenario
sources:
  - name: "handler headers reject handler tag prefix.ges"
    program: main
```

### Source code under test

```ges
on :handler Shoot(unit, target) {
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

## Test: emit rejects incomplete labeled message arguments

This negative compiler case exercises “emit rejects incomplete labeled message arguments” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0028
kind: compileError
level: scenario
sources:
  - name: "emit rejects incomplete labeled message arguments.ges"
    program: main
```

### Source code under test

```ges
on Start(unit, target) {
  emit Shoot(unit:)
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

## Test: prefix message and handler literals are rejected

This negative compiler case exercises “prefix message and handler literals are rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0029
kind: compileError
level: scenario
sources:
  - name: "prefix message and handler literals are rejected.ges"
    program: main
```

### Source code under test

```ges
on Start(unit, target) {
  let h as :handler be :handler Shoot(unit, target)
  let m as :message be :message Shoot(unit: unit, target: target)
  emit :message Shoot(unit: unit, target: target)
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

## Test: uppercase positional invocation requires identifier parameters

This negative compiler case exercises “uppercase positional invocation requires identifier parameters” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0030
kind: compileError
level: scenario
sources:
  - name: "uppercase positional invocation requires identifier parameters.ges"
    program: main
```

### Source code under test

```ges
on Start(unit) {
  let invalid be Shoot(unit + 1)
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

## Test: quantity rejects percentage unit syntax

This negative compiler case exercises “quantity rejects percentage unit syntax” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0031
kind: compileError
level: scenario
sources:
  - name: "quantity rejects percentage unit syntax.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be 100 as :quantity(%)
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

## Test: set literal syntax is rejected

This negative compiler case exercises “set literal syntax is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0032
kind: compileError
level: scenario
sources:
  - name: "set literal syntax is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be #set[1, 2]
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

## Test: generated set syntax is rejected

This negative compiler case exercises “generated set syntax is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0033
kind: compileError
level: scenario
sources:
  - name: "generated set syntax is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let x be #set[:select item from 1 to 3 => item]
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

## Test: predicate function and handler identifiers enforce casing

This negative compiler case exercises “predicate function and handler identifiers enforce casing” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0034
kind: compileError
level: scenario
sources:
  - name: "predicate function and handler identifiers enforce casing.ges"
    program: main
```

### Source code under test

```ges
predicate Wounded(unit) be unit.hp < unit.maxHp
function filter(Units) be Units
on Start(Target) {
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

## Test: take selector count must be positive

This negative compiler case exercises “take selector count must be positive” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0035
kind: compileError
level: scenario
sources:
  - name: "take selector count must be positive.ges"
    program: main
```

### Source code under test

```ges
on Start(values) {
  let result be values[:take first 0]
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

## Test: drop selector count must be positive

This negative compiler case exercises “drop selector count must be positive” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0036
kind: compileError
level: scenario
sources:
  - name: "drop selector count must be positive.ges"
    program: main
```

### Source code under test

```ges
on Start(values) {
  let result be values[:drop first 0]
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

## Test: unicode letters are rejected in identifiers

This negative compiler case exercises “unicode letters are rejected in identifiers” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0037
kind: compileError
level: scenario
sources:
  - name: "unicode letters are rejected in identifiers.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let naïve be 1
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

## Test: unicode whitespace is not portable trivia

This negative compiler case exercises “unicode whitespace is not portable trivia” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0038
kind: compileError
level: scenario
sources:
  - name: "unicode whitespace is not portable trivia.ges"
    program: main
```

### Source code under test

```ges
on Start {
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

## Test: choose selector count must be positive

This negative compiler case exercises “choose selector count must be positive” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0039
kind: compileError
level: scenario
sources:
  - name: "choose selector count must be positive.ges"
    program: main
```

### Source code under test

```ges
on Start(values) {
  let result be values[:choose 0 at random]
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

## Test: record type names reject numeric suffixes

This negative compiler case verifies that declared custom type names use the
portable type-name grammar rather than the wider identifier and tag grammar.

### Case description

```yaml
gesBlock: case
id: case-0040
kind: compileError
level: atomic
sources:
  - name: "record type names reject numeric suffixes.ges"
    program: main
```

### Source code under test

```ges
record :unit_2 as {
  value: :number
}

on Start {
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

## Test: bare of list syntax is rejected

This negative compiler case verifies that list literals use the single
canonical bracket syntax and that `of … and …` is not accepted as an expression.

### Case description

```yaml
gesBlock: case
id: case-0041
kind: compileError
level: atomic
sources:
  - name: "bare of list syntax is rejected.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let values be of 1 and 2
  emit Done(values: values)
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

## Test: colon-prefixed names are not map keys

This negative compiler case verifies that `:` remains reserved for structured
selectors, type names, and extension namespaces. Map keys use member syntax,
text, or `#` tags instead of the removed legacy `:name` spelling.

### Case description

```yaml
gesBlock: case
id: case-0042
kind: compileError
level: atomic
sources:
  - name: "colon-prefixed names are not map keys.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let unit be [name: 'Ada']
  emit Done(name: unit[:name])
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```
