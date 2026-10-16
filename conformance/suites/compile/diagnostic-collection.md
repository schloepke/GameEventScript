---
formatVersion: 1
suiteId: "compile.diagnostic-collection"
title: "CompileDiagnosticCollection"
categories: [conformance]
---

# CompileDiagnosticCollection

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies that compilation reports every diagnostic of the failing phase in order. Parsing recovers after a syntax error and reports all syntax errors of the first source that fails; validation reports every semantic diagnostic of the program. Syntax diagnostics are checked with their positions; validation diagnostics are checked by phase and code.

---

## Test: parse recovers at declarations and statements

A syntax error inside a handler resumes at the next statement, and an error at declaration level resumes at the next declaration keyword.

### Case description

```yaml
gesBlock: case
id: parse-recovers-at-declarations-and-statements
kind: compileError
level: scenario
sources:
  - name: "parse-recovers-at-declarations-and-statements.ges"
    program: main
```

### Source code under test

```ges
module m
on Start {
  let a be (
  let b be 1 +
}
on Stop {
  emit X(
}
function f_x() be 1
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "parse"
    code: "parse.syntax"
    line: 4
    column: 3
  - phase: "parse"
    code: "parse.syntax"
    line: 8
    column: 1
  - phase: "parse"
    code: "parse.syntax"
    line: 9
    column: 10
```

---

## Test: parse recovers inside nested blocks

An error in a nested block resumes inside that block; a later error in the enclosing block is still reported.

### Case description

```yaml
gesBlock: case
id: parse-recovers-inside-nested-blocks
kind: compileError
level: scenario
sources:
  - name: "parse-recovers-inside-nested-blocks.ges"
    program: main
```

### Source code under test

```ges
module m
on A {
  if true {
    let a be (
    let b be 1
  }
  let c be *
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "parse"
    code: "parse.syntax"
    line: 5
    column: 5
  - phase: "parse"
    code: "parse.syntax"
    line: 7
    column: 12
```

---

## Test: parse reports unclosed block at end of input

An unclosed handler block reports the missing brace and the failed closing at the end of input.

### Case description

```yaml
gesBlock: case
id: parse-reports-unclosed-block-at-end-of-input
kind: compileError
level: scenario
sources:
  - name: "parse-reports-unclosed-block-at-end-of-input.ges"
    program: main
```

### Source code under test

```ges
module m
on A {
  let a be 1
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "parse"
    code: "parse.syntax"
    line: 3
    column: 13
  - phase: "parse"
    code: "parse.syntax"
    line: 3
    column: 13
```

---

## Test: parse recovers at semicolons

Statements separated by semicolons resume at the next semicolon.

### Case description

```yaml
gesBlock: case
id: parse-recovers-at-semicolons
kind: compileError
level: scenario
sources:
  - name: "parse-recovers-at-semicolons.ges"
    program: main
```

### Source code under test

```ges
module m
on A { let a be ( ; let b be ] ; let c be 3 }
on B { emit }
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "parse"
    code: "parse.syntax"
    line: 2
    column: 19
  - phase: "parse"
    code: "parse.syntax"
    line: 2
    column: 30
  - phase: "parse"
    code: "parse.syntax"
    line: 3
    column: 13
```

---

## Test: parse skips illegal declaration tokens

Illegal tokens between declarations are each reported once and skipped.

### Case description

```yaml
gesBlock: case
id: parse-skips-illegal-declaration-tokens
kind: compileError
level: scenario
sources:
  - name: "parse-skips-illegal-declaration-tokens.ges"
    program: main
```

### Source code under test

```ges
module m
§
on A {}
@@
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "parse"
    code: "parse.syntax"
    line: 2
    column: 1
  - phase: "parse"
    code: "parse.syntax"
    line: 4
    column: 1
```

---

## Test: parse recovers in send statements

Malformed sends report each statement and parsing continues with the next handler.

### Case description

```yaml
gesBlock: case
id: parse-recovers-in-send-statements
kind: compileError
level: scenario
sources:
  - name: "parse-recovers-in-send-statements.ges"
    program: main
```

### Source code under test

```ges
module m
on A {
  emit B(x: 1,, y: 2)
  publish C(
}
on D {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "parse"
    code: "parse.syntax"
    line: 3
    column: 15
  - phase: "parse"
    code: "parse.syntax"
    line: 5
    column: 1
```

---

## Test: parse stops after first failing source

Every syntax error of the first source that fails is reported; later sources are not parsed.

### Case description

```yaml
gesBlock: case
id: parse-stops-after-first-failing-source
kind: compileError
level: scenario
sources:
  - name: "parse-stops-after-first-failing-source-first.ges"
    program: main
  - name: "parse-stops-after-first-failing-source-second.ges"
    program: main
```

### Source code under test

```ges
module m
on A { let a be ( }
on B { let b be ] }
```

### Source code under test

```ges
module m
on C { let c be ) }
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "parse"
    code: "parse.syntax"
    line: 2
    column: 19
  - phase: "parse"
    code: "parse.syntax"
    line: 3
    column: 17
```

---

## Test: validate collects declaration and body errors

A predicate/function conflict, a non-boolean predicate result and a repeated binding are all reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-declaration-and-body-errors
kind: compileError
level: scenario
sources:
  - name: "validate-collects-declaration-and-body-errors.ges"
    program: main
```

### Source code under test

```ges
module m
on Start {
  let a be 1
  let a be 2
}
predicate p(_ v) be v
function p(_ v) be v
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.predicateFunctionConflict"
  - phase: "validate"
    code: "validate.invalidPredicate"
  - phase: "validate"
    code: "validate.duplicateVariable"
```

---

## Test: validate collects parameter errors

Repeated parameters of a function and a handler and an unknown parameter type are all reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-parameter-errors
kind: compileError
level: scenario
sources:
  - name: "validate-collects-parameter-errors.ges"
    program: main
```

### Source code under test

```ges
module m
function f(a, a) be 1
on A(b, b) {}
on B(c as :Nope) {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicateDefinitionParameter"
  - phase: "validate"
    code: "validate.duplicateHandlerParameter"
  - phase: "validate"
    code: "validate.invalidTypeConstructor"
```

---

## Test: validate collects shadowing

Every shadowing binding of a handler is reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-shadowing
kind: compileError
level: scenario
sources:
  - name: "validate-collects-shadowing.ges"
    program: main
```

### Source code under test

```ges
module m
on A {
  let x be 1
  if true {
    let x be 2
  }
  for x in [1] {}
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.shadowedVariable"
  - phase: "validate"
    code: "validate.shadowedVariable"
```

---

## Test: validate collects predicate results

Each predicate with a non-boolean result is reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-predicate-results
kind: compileError
level: scenario
sources:
  - name: "validate-collects-predicate-results.ges"
    program: main
```

### Source code under test

```ges
module m
predicate p() be 1
predicate r() be "t"
on A {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.invalidPredicate"
  - phase: "validate"
    code: "validate.invalidPredicate"
```

---

## Test: validate collects system endpoint errors

Each malformed system endpoint is reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-system-endpoint-errors
kind: compileError
level: scenario
sources:
  - name: "validate-collects-system-endpoint-errors.ges"
    program: main
```

### Source code under test

```ges
module m
on undeliverable {}
on initialization(x) {}
on A {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.invalidMessageCase"
  - phase: "validate"
    code: "validate.invalidMessageCase"
```

---

## Test: validate reports duplicates before conflicts

A repeated function signature is reported before the predicate/function name conflict.

### Case description

```yaml
gesBlock: case
id: validate-reports-duplicates-before-conflicts
kind: compileError
level: scenario
sources:
  - name: "validate-reports-duplicates-before-conflicts.ges"
    program: main
```

### Source code under test

```ges
module m
function f(_ a) be 1
function f(_ b) be 2
predicate f(_ c) be true
on A {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicateFunction"
  - phase: "validate"
    code: "validate.predicateFunctionConflict"
```

---

## Test: validate collects repeated bindings

Every repeated binding in every handler is reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-repeated-bindings
kind: compileError
level: scenario
sources:
  - name: "validate-collects-repeated-bindings.ges"
    program: main
```

### Source code under test

```ges
module m
on A { let a be 1; let a be 2; let a be 3 }
on B { let b be 1; let b be 2 }
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicateVariable"
  - phase: "validate"
    code: "validate.duplicateVariable"
  - phase: "validate"
    code: "validate.duplicateVariable"
```

---

## Test: validate collects call errors

Wrong arities and a missing callable are reported in source order; a labeled call to an unknown name is left to lowering.

### Case description

```yaml
gesBlock: case
id: validate-collects-call-errors
kind: compileError
level: scenario
sources:
  - name: "validate-collects-call-errors.ges"
    program: main
```

### Source code under test

```ges
module m
function f(_ a) be a
on A {
  let x be f(1, 2)
  let y be f(b: 1)
  let z be h(1)
  let w be h(c: 1)
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.wrongFunctionArity"
  - phase: "validate"
    code: "validate.wrongFunctionArity"
  - phase: "validate"
    code: "validate.missingCallable"
```

---

## Test: validate reports each repeated argument once

Each repeated message argument label is reported once.

### Case description

```yaml
gesBlock: case
id: validate-reports-each-repeated-argument-once
kind: compileError
level: scenario
sources:
  - name: "validate-reports-each-repeated-argument-once.ges"
    program: main
```

### Source code under test

```ges
module m
on A {
  emit B(x: 1, x: 2, x: 3, y: 1, y: 2)
  emit C(a: 1, a: 2)
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicatePublishArgument"
  - phase: "validate"
    code: "validate.duplicatePublishArgument"
  - phase: "validate"
    code: "validate.duplicatePublishArgument"
```

---

## Test: validate collects vector constructor errors

Every invalid vector or point constructor is reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-vector-constructor-errors
kind: compileError
level: scenario
sources:
  - name: "validate-collects-vector-constructor-errors.ges"
    program: main
```

### Source code under test

```ges
module m
on A {
  let v be :Vector(z: 1, x: 2)
  let w be :Vector(x: 1, 2)
  let p be :Point(1, 2, 3, 4)
  let q be :Vector(q: 1)
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.invalidTypeConstructor"
  - phase: "validate"
    code: "validate.invalidTypeConstructor"
  - phase: "validate"
    code: "validate.invalidTypeConstructor"
  - phase: "validate"
    code: "validate.invalidTypeConstructor"
```

---

## Test: validate collects handler endpoint errors

Initialization and undeliverable endpoint violations are each reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-handler-endpoint-errors
kind: compileError
level: scenario
sources:
  - name: "validate-collects-handler-endpoint-errors.ges"
    program: main
```

### Source code under test

```ges
module m
on initialization(x) {}
on initialization matching #t {}
on undeliverable {}
on B as m {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.invalidMessageCase"
  - phase: "validate"
    code: "validate.invalidMessageCase"
  - phase: "validate"
    code: "validate.invalidMessageCase"
```

---

## Test: validate collects repeated constants and types

Every repeated constant and record type is reported, constants first.

### Case description

```yaml
gesBlock: case
id: validate-collects-repeated-constants-and-types
kind: compileError
level: scenario
sources:
  - name: "validate-collects-repeated-constants-and-types.ges"
    program: main
```

### Source code under test

```ges
module m
constant $a be 1
constant $b be 2
constant $a be 3
constant $b be 4
record :X as { a: :Number }
record :X as { b: :Number }
record :X as { c: :Number }
on A {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicateConstant"
  - phase: "validate"
    code: "validate.duplicateConstant"
  - phase: "validate"
    code: "validate.duplicateType"
  - phase: "validate"
    code: "validate.duplicateType"
```

---

## Test: validate orders program wide declarations

Repeated predicates, repeated functions and name conflicts are reported in that order.

### Case description

```yaml
gesBlock: case
id: validate-orders-program-wide-declarations
kind: compileError
level: scenario
sources:
  - name: "validate-orders-program-wide-declarations.ges"
    program: main
```

### Source code under test

```ges
module m
predicate p() be true
predicate p() be false
function q() be 1
function q() be 2
function p(_ x) be 1
predicate q(_ y) be true
on A {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicatePredicate"
  - phase: "validate"
    code: "validate.duplicateFunction"
  - phase: "validate"
    code: "validate.predicateFunctionConflict"
  - phase: "validate"
    code: "validate.predicateFunctionConflict"
```

---

## Test: validate collects binding errors in conditions and loops

Repeated if-let bindings, a shadowing then-branch binding and repeated loop bindings are all reported.

### Case description

```yaml
gesBlock: case
id: validate-collects-binding-errors-in-conditions-and-loops
kind: compileError
level: scenario
sources:
  - name: "validate-collects-binding-errors-in-conditions-and-loops.ges"
    program: main
```

### Source code under test

```ges
module m
on A {
  if let a be 1; let a be 2 { let b be 1 }
  if let c be 1 { let c be 2 }
  for x, x in [[1, 2]] {}
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicateVariable"
  - phase: "validate"
    code: "validate.duplicateVariable"
  - phase: "validate"
    code: "validate.duplicateVariable"
```

---

## Test: validate collects repeated parameters and types

Each repeated parameter name is reported once per declaration, together with unknown parameter types.

### Case description

```yaml
gesBlock: case
id: validate-collects-repeated-parameters-and-types
kind: compileError
level: scenario
sources:
  - name: "validate-collects-repeated-parameters-and-types.ges"
    program: main
```

### Source code under test

```ges
module m
function f(a, a, b, b) be 1
predicate g(c as :Nope, c as :Nope2) be true
on H(d, d, d) {}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.invalidTypeConstructor"
  - phase: "validate"
    code: "validate.invalidTypeConstructor"
  - phase: "validate"
    code: "validate.duplicateDefinitionParameter"
  - phase: "validate"
    code: "validate.duplicateDefinitionParameter"
  - phase: "validate"
    code: "validate.duplicateDefinitionParameter"
  - phase: "validate"
    code: "validate.duplicateHandlerParameter"
```

---

## Test: validate collects across sources

Program-wide duplicates of every source and the body errors of each module are reported in one list.

### Case description

```yaml
gesBlock: case
id: validate-collects-across-sources
kind: compileError
level: scenario
sources:
  - name: "validate-collects-across-sources-first.ges"
    program: main
  - name: "validate-collects-across-sources-second.ges"
    program: main
```

### Source code under test

```ges
module m
function f() be 1
constant $a be 1
```

### Source code under test

```ges
module m
function f() be 2
constant $a be 2
on A { let x be 1; let x be 2 }
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: "validate"
    code: "validate.duplicateConstant"
  - phase: "validate"
    code: "validate.duplicateFunction"
  - phase: "validate"
    code: "validate.duplicateVariable"
```
