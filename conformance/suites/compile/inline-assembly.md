---
formatVersion: 1
suiteId: compile.inline-assembly
title: "Inline assembly"
categories: [conformance]
---

# Inline assembly

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite qualifies assembly scoping, definite assignment, explicit instruction execution and internal resource lifetimes.

---

## Test: initializer

This case verifies initializer behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: initializer
kind: scriptApi
level: scenario
sources:
  - name: initializer.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let answer be asm { LoadInteger answer, 42 }
    emit Done(value: answer)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: exports

This case verifies exports behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: exports
kind: scriptApi
level: scenario
sources:
  - name: exports.ges
    program: main
```

### Source code under test

```ges
on Start() {
    asm {
        let left, right
        LoadInteger left, 6
        LoadInteger right, 7
    }
    let answer be asm { Multiply answer, left, right }
    emit Done(value: answer)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: function

This case verifies function behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: function
kind: scriptApi
level: scenario
sources:
  - name: function.ges
    program: main
```

### Source code under test

```ges
function square(x) be asm { Multiply square, x, x }
on Start() { emit Done(value: square(x: 7)) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "49"
```

---

## Test: predicate

This case verifies predicate behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: predicate
kind: scriptApi
level: scenario
sources:
  - name: predicate.ges
    program: main
```

### Source code under test

```ges
predicate positive(x) be asm { Greater positive, x, 0 }
on Start() { emit Done(value: positive(x: 7)) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Boolean"
              value: true
```

---

## Test: computed

This case verifies computed behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: computed
kind: scriptApi
level: scenario
sources:
  - name: computed.ges
    program: main
```

### Source code under test

```ges
record :Sample as {
    input: :Number
    squareValue: :Number computed by asm { Multiply squareValue, input, input }
}
on Start() { emit Done(value: :Sample(input: 7).squareValue) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "49"
```

---

## Test: loop

This case verifies loop behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: loop
kind: scriptApi
level: scenario
sources:
  - name: loop.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let answer be asm {
        .register counter, condition
        LoadInteger answer, 0
        LoadInteger counter, 6
        again:
        Add answer, answer, 7
        Subtract counter, counter, 1
        Greater condition, counter, 0
        JumpIfTrue condition, again
    }
    emit Done(value: answer)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: iterator

This case verifies iterator behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: iterator
kind: scriptApi
level: scenario
sources:
  - name: iterator.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let items be [10, 20, 12]
    let answer be asm {
        .register iterator, item
        LoadInteger answer, 0
        IteratorCreate iterator, items
        next:
        IteratorNext item, iterator, end
        Add answer, answer, item
        Jump next
        end:
        IteratorClose iterator
    }
    emit Done(value: answer)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: builder

This case verifies builder behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: builder
kind: scriptApi
level: scenario
sources:
  - name: builder.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let items be asm {
        .register builder
        ListBuilderCreate builder
        ListBuilderAdd builder, 42
        ListBuilderFinish items, builder
    }
    emit Done(value: items[1])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: explicit nothing

This case verifies explicit nothing behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: explicit-nothing
kind: scriptApi
level: scenario
sources:
  - name: explicit-nothing.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let answer be asm { LoadNothing answer }
    emit Done(value: answer)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Nothing"
```

---

## Test: read before write

This case verifies read before write behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: read-before-write
kind: compileError
level: scenario
sources:
  - name: read-before-write.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { Add result, result, 1 } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: missing result

This case verifies missing result behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: missing-result
kind: compileError
level: scenario
sources:
  - name: missing-result.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { Nop } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: branch initialization

This case verifies branch initialization behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: branch-initialization
kind: compileError
level: scenario
sources:
  - name: branch-initialization.ges
    program: main
```

### Source code under test

```ges
on Start(flag) {
    let result be asm {
        JumpIfTrue flag, end
        LoadInteger result, 1
        end:
        Nop
    }
}
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: readonly input

This case verifies readonly input behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: readonly-input
kind: compileError
level: scenario
sources:
  - name: readonly-input.ges
    program: main
```

### Source code under test

```ges
on Start(input) { asm { LoadInteger input, 2 } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: unknown label

This case verifies unknown label behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: unknown-label
kind: compileError
level: scenario
sources:
  - name: unknown-label.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { Jump nowhere } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: duplicate label

This case verifies duplicate label behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: duplicate-label
kind: compileError
level: scenario
sources:
  - name: duplicate-label.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { here:; Nop; here:; Nop } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: raw stage

This case verifies raw stage behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: raw-stage
kind: compileError
level: scenario
sources:
  - name: raw-stage.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { StageNothing } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: raw return

This case verifies raw return behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: raw-return
kind: compileError
level: scenario
sources:
  - name: raw-return.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { ReturnVoid } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: shadow input

This case verifies shadow input behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: shadow-input
kind: compileError
level: scenario
sources:
  - name: shadow-input.ges
    program: main
```

### Source code under test

```ges
on Start(input) { let result be asm { .register input; LoadNothing result } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: callable result collision

This case verifies callable result collision behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: callable-result-collision
kind: compileError
level: scenario
sources:
  - name: callable-result-collision.ges
    program: main
```

### Source code under test

```ges
function square(square) be asm { Multiply square, square, square }
on Start() { emit Done(value: square(square: 2)) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: expression export

This case verifies expression export behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: expression-export
kind: compileError
level: scenario
sources:
  - name: expression-export.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { let other; LoadNothing result } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: predicate type

This case verifies predicate type behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: predicate-type
kind: compileError
level: scenario
sources:
  - name: predicate-type.ges
    program: main
```

### Source code under test

```ges
predicate valid() be asm { LoadInteger valid, 1 }
on Start() { emit Done(value: valid()) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: live resource

This case verifies live resource behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: live-resource
kind: compileError
level: scenario
sources:
  - name: live-resource.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register builder; ListBuilderCreate builder } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: resource copy

This case verifies resource copy behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: resource-copy
kind: compileError
level: scenario
sources:
  - name: resource-copy.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register builder, other; ListBuilderCreate builder; Move other, builder } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: resource export

This case verifies resource export behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: resource-export
kind: compileError
level: scenario
sources:
  - name: resource-export.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { let builder; ListBuilderCreate builder } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: resource after finish

This case verifies resource after finish behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: resource-after-finish
kind: compileError
level: scenario
sources:
  - name: resource-after-finish.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm {
    .register builder
    ListBuilderCreate builder
    ListBuilderFinish result, builder
    ListBuilderAdd builder, 1
} }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: resource wrong family

This case verifies resource wrong family behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: resource-wrong-family
kind: compileError
level: scenario
sources:
  - name: resource-wrong-family.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm {
    .register builder
    ListBuilderCreate builder
    MapBuilderFinish result, builder
} }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: unreachable unknown opcode

This case verifies unreachable unknown opcode behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: unreachable-unknown-opcode
kind: compileError
level: scenario
sources:
  - name: unreachable-unknown-opcode.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { Jump end; UnknownOpcode; end:; Nop } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: symbolic call

This case verifies symbolic call behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: symbolic-call
kind: scriptApi
level: scenario
sources:
  - name: symbolic-call.ges
    program: main
```

### Source code under test

```ges
function calculate(value) be value * 2
on Start() { let result be asm { Call result, calculate(value: 21) }; emit Done(value: result) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: symbolic list

This case verifies symbolic list behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: symbolic-list
kind: scriptApi
level: scenario
sources:
  - name: symbolic-list.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { .register items; CreateList items, [1, 2, 3]; Count result, items }; emit Done(value: result) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: symbolic message

This case verifies symbolic message behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: symbolic-message
kind: scriptApi
level: scenario
sources:
  - name: symbolic-message.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register result; LoadInteger result, 42; emit Done(value: result) } }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: expression send

This case verifies expression send behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: expression-send
kind: compileError
level: scenario
sources:
  - name: expression-send.ges
    program: main
```

### Source code under test

```ges
function result() be asm { LoadInteger result, 42; emit Done(value: result) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: symbolic uninitialized

This case verifies symbolic uninitialized behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: symbolic-uninitialized
kind: compileError
level: scenario
sources:
  - name: symbolic-uninitialized.ges
    program: main
```

### Source code under test

```ges
function calculate(value) be value
on Start() { let result be asm { .register input; Call result, calculate(value: input) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: cast type

This case verifies cast type behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: cast-type
kind: scriptApi
level: scenario
sources:
  - name: cast-type.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { Cast result, 42, :Number }; emit Done(value: result) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: random scope

This case verifies random scope behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: random-scope
kind: scriptApi
level: scenario
sources:
  - name: random-scope.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { RandomPushConstant 123; LoadInteger result, 42; RandomPop }; emit Done(value: result) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: random leak

This case verifies random leak behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: random-leak
kind: compileError
level: scenario
sources:
  - name: random-leak.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { RandomPushConstant 123 } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: random pop outer

This case verifies random pop outer behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: random-pop-outer
kind: compileError
level: scenario
sources:
  - name: random-pop-outer.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { RandomPop } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: random loop growth

This case verifies random loop growth behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: random-loop-growth
kind: compileError
level: scenario
sources:
  - name: random-loop-growth.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { again:; RandomPushConstant 123; Jump again } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: result send

This case verifies result send behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: result-send
kind: scriptApi
level: scenario
sources:
  - name: result-send.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { EmitInstant result, Done(value: 42) } }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: tagged send

This case verifies tagged send behavior at the portable assembly boundary.

### Case description

```yaml
gesBlock: case
id: tagged-send
kind: scriptApi
level: scenario
sources:
  - name: tagged-send.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { EmitMessageWithTags Done(value: 42), [#sample] } }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
        tags: [sample]
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```
