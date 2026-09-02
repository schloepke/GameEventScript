---
formatVersion: 1
suiteId: "compile.syntax-errors"
title: "CompileSyntaxErrors"
categories: [conformance]
tags: [migrated-json-v1]
---

# CompileSyntaxErrors

Mechanically migrated from the former JSON conformance corpus.

## Test: missing expression in let statement fails syntax analysis

```yaml
gesBlock: case
id: case-0001
kind: compileError
level: scenario
sources:
  - name: "missing expression in let statement fails syntax analysis.ges"
    program: main
```

```ges
on Broken {
  let x be
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: malformed predicate parameter fails syntax analysis

```yaml
gesBlock: case
id: case-0002
kind: compileError
level: scenario
sources:
  - name: "malformed predicate parameter fails syntax analysis.ges"
    program: main
```

```ges
module BrokenCombat
predicate unitIsDead(un%it) be unit[hp] <= 0
on FireAtUnit(unit) {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: illegal token inside expression fails through compile interface

```yaml
gesBlock: case
id: case-0003
kind: compileError
level: scenario
sources:
  - name: "illegal token inside expression fails through compile interface.ges"
    program: main
```

```ges
on Broken {
  let value be hello%&some
  let other be @
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy percent modulo operator is rejected

```yaml
gesBlock: case
id: case-0004
kind: compileError
level: scenario
sources:
  - name: "legacy percent modulo operator is rejected.ges"
    program: main
```

```ges
on Broken {
  let value be 10 % 3
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: type tags are case sensitive

```yaml
gesBlock: case
id: case-0005
kind: compileError
level: scenario
sources:
  - name: "type tags are case sensitive.ges"
    program: main
```

```ges
on Start(value) {
  let numberValue as :Float be value
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy external handler syntax is rejected

```yaml
gesBlock: case
id: case-0006
kind: compileError
level: scenario
sources:
  - name: "legacy external handler syntax is rejected.ges"
    program: main
```

```ges
external on Notify(playerId, points)
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: sort selector requires explicit direction

```yaml
gesBlock: case
id: case-0007
kind: compileError
level: scenario
sources:
  - name: "sort selector requires explicit direction.ges"
    program: main
```

```ges
on Sorted(items) {
  let values be items[:sort]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy untagged type declaration is rejected

```yaml
gesBlock: case
id: case-0008
kind: compileError
level: scenario
sources:
  - name: "legacy untagged type declaration is rejected.ges"
    program: main
```

```ges
on Start(value) {
  let score as double be value
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy untagged unary form is rejected

```yaml
gesBlock: case
id: case-0009
kind: compileError
level: scenario
sources:
  - name: "legacy untagged unary form is rejected.ges"
    program: main
```

```ges
on Start(values) {
  let size be len values
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy random tag operator is rejected

```yaml
gesBlock: case
id: case-0010
kind: compileError
level: scenario
sources:
  - name: "legacy random tag operator is rejected.ges"
    program: main
```

```ges
on Start {
  let value be :random from 1 to 6
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy clamp tag operator is rejected

```yaml
gesBlock: case
id: case-0011
kind: compileError
level: scenario
sources:
  - name: "legacy clamp tag operator is rejected.ges"
    program: main
```

```ges
on Start {
  let value be :clamp 5 between 1 and 10
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy min tag operator is rejected

```yaml
gesBlock: case
id: case-0012
kind: compileError
level: scenario
sources:
  - name: "legacy min tag operator is rejected.ges"
    program: main
```

```ges
on Start {
  let value be :min of 1 and 2
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy max tag operator is rejected

```yaml
gesBlock: case
id: case-0013
kind: compileError
level: scenario
sources:
  - name: "legacy max tag operator is rejected.ges"
    program: main
```

```ges
on Start {
  let value be :max of 1 and 2
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy zip tag operator is rejected

```yaml
gesBlock: case
id: case-0014
kind: compileError
level: scenario
sources:
  - name: "legacy zip tag operator is rejected.ges"
    program: main
```

```ges
on Start {
  let value be [1] :zip [2]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy dice tag roll is rejected

```yaml
gesBlock: case
id: case-0015
kind: compileError
level: scenario
sources:
  - name: "legacy dice tag roll is rejected.ges"
    program: main
```

```ges
on Start {
  let value be :dice 4d6
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy default tag operator is rejected

```yaml
gesBlock: case
id: case-0016
kind: compileError
level: scenario
sources:
  - name: "legacy default tag operator is rejected.ges"
    program: main
```

```ges
on Start(value) {
  let result be value :default 10
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy emit tag literal form is rejected

```yaml
gesBlock: case
id: case-0017
kind: compileError
level: scenario
sources:
  - name: "legacy emit tag literal form is rejected.ges"
    program: main
```

```ges
on Start {
  emit Done with :radio
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy handler matching tag literal form is rejected

```yaml
gesBlock: case
id: case-0018
kind: compileError
level: scenario
sources:
  - name: "legacy handler matching tag literal form is rejected.ges"
    program: main
```

```ges
on Ping matching :radio {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy handler without tag literal form is rejected

```yaml
gesBlock: case
id: case-0019
kind: compileError
level: scenario
sources:
  - name: "legacy handler without tag literal form is rejected.ges"
    program: main
```

```ges
on Ping without :blocked {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy floor prefix builtin is rejected

```yaml
gesBlock: case
id: case-0020
kind: compileError
level: scenario
sources:
  - name: "legacy floor prefix builtin is rejected.ges"
    program: main
```

```ges
on Start {
  let value be #floor 10.4
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: legacy wrapDegree prefix builtin is rejected

```yaml
gesBlock: case
id: case-0021
kind: compileError
level: scenario
sources:
  - name: "legacy wrapDegree prefix builtin is rejected.ges"
    program: main
```

```ges
on Start {
  let value be #wrapDegree 370°
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: direct range is rejected after in for loops

```yaml
gesBlock: case
id: case-0022
kind: compileError
level: scenario
sources:
  - name: "direct range is rejected after in for loops.ges"
    program: main
```

```ges
on Start {
  for item in from 1 to 10 emit Seen(value: item)
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: direct range is rejected after in generated collections

```yaml
gesBlock: case
id: case-0023
kind: compileError
level: scenario
sources:
  - name: "direct range is rejected after in generated collections.ges"
    program: main
```

```ges
on Start {
  let values be :list[:select item in from 1 to 5 => item]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: seeded random blocks cannot be value expressions

```yaml
gesBlock: case
id: case-0024
kind: compileError
level: scenario
sources:
  - name: "seeded random blocks cannot be value expressions.ges"
    program: main
```

```ges
on Start(seed) {
  let values be random with seed {
    emit Done
  }
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: drop selector requires a supported scope

```yaml
gesBlock: case
id: case-0025
kind: compileError
level: scenario
sources:
  - name: "drop selector requires a supported scope.ges"
    program: main
```

```ges
on Broken {
  let x be roll dice 4d6[:drop middle 1]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: dice counts must be positive integers

```yaml
gesBlock: case
id: case-0026
kind: compileError
level: scenario
sources:
  - name: "dice counts must be positive integers.ges"
    program: main
```

```ges
on Broken {
  let x be roll dice 0d6
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: handler headers reject handler tag prefix

```yaml
gesBlock: case
id: case-0027
kind: compileError
level: scenario
sources:
  - name: "handler headers reject handler tag prefix.ges"
    program: main
```

```ges
on :handler Shoot(unit, target) {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: emit rejects incomplete labeled message arguments

```yaml
gesBlock: case
id: case-0028
kind: compileError
level: scenario
sources:
  - name: "emit rejects incomplete labeled message arguments.ges"
    program: main
```

```ges
on Start(unit, target) {
  emit Shoot(unit:)
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: prefix message and handler literals are rejected

```yaml
gesBlock: case
id: case-0029
kind: compileError
level: scenario
sources:
  - name: "prefix message and handler literals are rejected.ges"
    program: main
```

```ges
on Start(unit, target) {
  let h as :handler be :handler Shoot(unit, target)
  let m as :message be :message Shoot(unit: unit, target: target)
  emit :message Shoot(unit: unit, target: target)
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: uppercase positional invocation requires identifier parameters

```yaml
gesBlock: case
id: case-0030
kind: compileError
level: scenario
sources:
  - name: "uppercase positional invocation requires identifier parameters.ges"
    program: main
```

```ges
on Start(unit) {
  let invalid be Shoot(unit + 1)
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: quantity rejects percentage unit syntax

```yaml
gesBlock: case
id: case-0031
kind: compileError
level: scenario
sources:
  - name: "quantity rejects percentage unit syntax.ges"
    program: main
```

```ges
on Start {
  let x be 100 as :quantity(%)
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: set literal syntax is rejected

```yaml
gesBlock: case
id: case-0032
kind: compileError
level: scenario
sources:
  - name: "set literal syntax is rejected.ges"
    program: main
```

```ges
on Start {
  let x be #set[1, 2]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: generated set syntax is rejected

```yaml
gesBlock: case
id: case-0033
kind: compileError
level: scenario
sources:
  - name: "generated set syntax is rejected.ges"
    program: main
```

```ges
on Start {
  let x be #set[:select item from 1 to 3 => item]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: predicate function and handler identifiers enforce casing

```yaml
gesBlock: case
id: case-0034
kind: compileError
level: scenario
sources:
  - name: "predicate function and handler identifiers enforce casing.ges"
    program: main
```

```ges
predicate Wounded(unit) be unit.hp < unit.maxHp
function filter(Units) be Units
on Start(Target) {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: take selector count must be positive

```yaml
gesBlock: case
id: case-0035
kind: compileError
level: scenario
sources:
  - name: "take selector count must be positive.ges"
    program: main
```

```ges
on Start(values) {
  let result be values[:take first 0]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: drop selector count must be positive

```yaml
gesBlock: case
id: case-0036
kind: compileError
level: scenario
sources:
  - name: "drop selector count must be positive.ges"
    program: main
```

```ges
on Start(values) {
  let result be values[:drop first 0]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: unicode letters are rejected in identifiers

```yaml
gesBlock: case
id: case-0037
kind: compileError
level: scenario
sources:
  - name: "unicode letters are rejected in identifiers.ges"
    program: main
```

```ges
on Start {
  let naïve be 1
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: unicode whitespace is not portable trivia

```yaml
gesBlock: case
id: case-0038
kind: compileError
level: scenario
sources:
  - name: "unicode whitespace is not portable trivia.ges"
    program: main
```

```ges
on Start {
  emit Done
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

## Test: choose selector count must be positive

```yaml
gesBlock: case
id: case-0039
kind: compileError
level: scenario
sources:
  - name: "choose selector count must be positive.ges"
    program: main
```

```ges
on Start(values) {
  let result be values[:choose 0 at random]
}
```

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```
