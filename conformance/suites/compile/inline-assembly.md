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

---

## Test: signed int64 minimum

This case verifies signed int64 minimum using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: signed-int64-minimum
kind: scriptApi
level: scenario
sources:
  - name: signed-int64-minimum.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { LoadInteger result, -9223372036854775808 }; emit Done(value: result) }
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
              value: "-9223372036854775808"
```

---

## Test: negative unit preserved

This case verifies negative unit preserved using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: negative-unit-preserved
kind: scriptApi
level: scenario
sources:
  - name: negative-unit-preserved.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { .register number; Move number, -7m; Cast result, number, :Text }; emit Done(value: result) }
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
              type: ":Text"
              value: "-7m"
```

---

## Test: range iterator short

This case verifies range iterator short using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: range-iterator-short
kind: scriptApi
level: scenario
sources:
  - name: range-iterator-short.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm {
    .register iterator, item
    LoadInteger result, 0
    CreateRangeIteratorShort iterator, 1, 6, 1
    next:
    IteratorNext item, iterator, end
    Add result, result, item
    Jump next
    end:
    IteratorClose iterator
}; emit Done(value: result) }
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
              value: "21"
```

---

## Test: iterator failure edge

This case verifies iterator failure edge using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: iterator-failure-edge
kind: scriptApi
level: scenario
sources:
  - name: iterator-failure-edge.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm {
    .register iterator, item
    LoadInteger result, 42
    IteratorCreateOrJump iterator, nothing, end
    IteratorClose iterator
    end:
    Nop
}; emit Done(value: result) }
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

## Test: symbolic record

This case verifies symbolic record using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: symbolic-record
kind: scriptApi
level: scenario
sources:
  - name: symbolic-record.ges
    program: main
```

### Source code under test

```ges
record :Sample as { value: :Number }
on Start() { let result be asm { .register instance; CreateRecord instance, :Sample(value: 42); MemberAccess result, 'value', instance }; emit Done(value: result) }
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

## Test: map builder

This case verifies map builder using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: map-builder
kind: scriptApi
level: scenario
sources:
  - name: map-builder.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm {
    .register builder, map
    MapBuilderCreate builder
    MapBuilderAdd builder, 'answer', 42
    MapBuilderFinish map, builder
    MemberAccess result, 'answer', map
}; emit Done(value: result) }
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

## Test: handler binding

This case verifies handler binding using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: handler-binding
kind: scriptApi
level: scenario
sources:
  - name: handler-binding.ges
    program: main
```

### Source code under test

```ges
on Start() { asm {
    .register handler, message
    LoadHandler handler, Done(value)
    BindHandler message, handler(value: 42)
    EmitMessageValue message
} }
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

## Test: temporary does not escape

This case verifies temporary does not escape using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: temporary-does-not-escape
kind: compileError
level: scenario
sources:
  - name: temporary-does-not-escape.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register temp; LoadInteger temp, 1 }; emit Done(value: temp) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.unresolvedSymbol }
```

---

## Test: export does not escape branch

This case verifies export does not escape branch using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: export-does-not-escape-branch
kind: compileError
level: scenario
sources:
  - name: export-does-not-escape-branch.ges
    program: main
```

### Source code under test

```ges
on Start() { if true { asm { let result; LoadInteger result, 1 } }; emit Done(value: result) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.unresolvedSymbol }
```

---

## Test: late declaration

This case verifies late declaration using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: late-declaration
kind: compileError
level: scenario
sources:
  - name: late-declaration.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { Nop; .register temp } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: unreachable bad register

This case verifies unreachable bad register using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: unreachable-bad-register
kind: compileError
level: scenario
sources:
  - name: unreachable-bad-register.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { Jump end; Add unknown, 1, 2; end:; Nop } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: live resource overwrite

This case verifies live resource overwrite using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: live-resource-overwrite
kind: compileError
level: scenario
sources:
  - name: live-resource-overwrite.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register iterator; CreateRangeIteratorShort iterator, 1, 2, 1; LoadNothing iterator } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: closed resource join

This case verifies closed resource join using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: closed-resource-join
kind: compileError
level: scenario
sources:
  - name: closed-resource-join.ges
    program: main
```

### Source code under test

```ges
on Start(flag) { let result be asm {
    .register builder
    JumpIfTrue flag, plain
    ListBuilderCreate builder
    ListBuilderFinish result, builder
    Jump end
    plain:
    LoadNothing builder
    end:
    Move result, builder
} }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: uninitialized symbolic message

This case verifies uninitialized symbolic message using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: uninitialized-symbolic-message
kind: compileError
level: scenario
sources:
  - name: uninitialized-symbolic-message.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register value; emit Done(value: value) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: float in integer immediate

This case verifies float in integer immediate using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: float-in-integer-immediate
kind: compileError
level: scenario
sources:
  - name: float-in-integer-immediate.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register value; LoadInteger value, 1.5 } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: raw record construction

This case verifies raw record construction using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: raw-record-construction
kind: compileError
level: scenario
sources:
  - name: raw-record-construction.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { CreateRecordValue } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: raw register frame

This case verifies raw register frame using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: raw-register-frame
kind: compileError
level: scenario
sources:
  - name: raw-register-frame.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { RegisterLocals 3 } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: cyclic symbolic call

This case verifies cyclic symbolic call using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: cyclic-symbolic-call
kind: compileError
level: scenario
sources:
  - name: cyclic-symbolic-call.ges
    program: main
```

### Source code under test

```ges
function loop() be asm { Call loop, loop() }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.cyclicCallGraph }
```

---

## Test: instruction preservation

This case verifies instruction preservation using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: instruction-preservation
kind: bytecode
level: scenario
sources:
  - name: instruction-preservation.ges
    program: main
```

### Source code under test

```ges
on Start() { asm {
    .register first, second, ignored
    LoadInteger first, 7
    LoadInteger first, 8
    LoadInteger second, 6
    Multiply ignored, first, second
    Nop
    Jump end
    Nop
    end:
    Nop
} }
```

### Expectation

```yaml
gesBlock: expect
opcodes:
  counts:
    LoadInteger: 3
    Multiply: 1
    Nop: 3
    Jump: 1
```

---

## Test: zero argument symbolic message

This case verifies zero argument symbolic message using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: zero-argument-symbolic-message
kind: scriptApi
level: scenario
sources:
  - name: zero-argument-symbolic-message.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { EmitMessage Ping() } }
on Ping() { emit Done(value: 42) }
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
      - name: Ping
        args: []
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: zero argument message value

This case verifies zero argument message value using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: zero-argument-message-value
kind: scriptApi
level: scenario
sources:
  - name: zero-argument-message-value.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { .register message; LoadMessage message, Ping(); EmitMessageValue message } }
on Ping() { emit Done(value: 42) }
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
      - name: Ping
        args: []
      - name: Done
        args:
          - name: value
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: predicate boolean cast

This case verifies predicate boolean cast using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: predicate-boolean-cast
kind: scriptApi
level: scenario
sources:
  - name: predicate-boolean-cast.ges
    program: main
```

### Source code under test

```ges
predicate valid(value) be asm { Cast valid, value, :Boolean }
on Start() { emit Done(value: valid(value: true)) }
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

## Test: predicate call result

This case verifies predicate call result using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: predicate-call-result
kind: scriptApi
level: scenario
sources:
  - name: predicate-call-result.ges
    program: main
```

### Source code under test

```ges
predicate positive(value) be value > 0
predicate valid(value) be asm { Call valid, positive(value: value) }
on Start() { emit Done(value: valid(value: 1) and not valid(value: -1)) }
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

## Test: predicate cast and call join

This case verifies predicate cast and call join using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: predicate-cast-and-call-join
kind: scriptApi
level: scenario
sources:
  - name: predicate-cast-and-call-join.ges
    program: main
```

### Source code under test

```ges
predicate positive(value) be value > 0
predicate valid(value, chooseCast) be asm {
    JumpIfTrue chooseCast, cast
    Call valid, positive(value: value)
    Jump done
    cast:
    Cast valid, true, :Boolean
    done:
    Nop
}
on Start() { emit Done(value: valid(value: 1, chooseCast: false) and valid(value: 0, chooseCast: true)) }
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

## Test: predicate numeric cast rejected

This case verifies predicate numeric cast rejected using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: predicate-numeric-cast-rejected
kind: compileError
level: scenario
sources:
  - name: predicate-numeric-cast-rejected.ges
    program: main
```

### Source code under test

```ges
predicate invalid(value) be asm { Cast invalid, value, :Number }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: predicate function call rejected

This case verifies predicate function call rejected using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: predicate-function-call-rejected
kind: compileError
level: scenario
sources:
  - name: predicate-function-call-rejected.ges
    program: main
```

### Source code under test

```ges
function number() be 1
predicate invalid() be asm { Call invalid, number() }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: vector rejects point

This case verifies vector rejects point using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: vector-rejects-point
kind: compileError
level: scenario
sources:
  - name: vector-rejects-point.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { CreateVector result, :Point(x: 1, y: 2) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: point rejects vector

This case verifies point rejects vector using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: point-rejects-vector
kind: compileError
level: scenario
sources:
  - name: point-rejects-vector.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { CreatePoint result, :Vector(x: 1, y: 2) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: record rejects spatial

This case verifies record rejects spatial using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: record-rejects-spatial
kind: compileError
level: scenario
sources:
  - name: record-rejects-spatial.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { CreateRecord result, :Point(x: 1, y: 2) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: external rejects record

This case verifies external rejects record using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: external-rejects-record
kind: compileError
level: scenario
sources:
  - name: external-rejects-record.ges
    program: main
```

### Source code under test

```ges
record :Sample as { value: :Number }
on Start() { let result be asm { CreateExternalType result, :Sample(value: 1) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: data rejects record

This case verifies data rejects record using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: data-rejects-record
kind: compileError
level: scenario
sources:
  - name: data-rejects-record.ges
    program: main
```

### Source code under test

```ges
record :Sample as { value: :Number }
on Start() { let result be asm { ConstructData result, :Sample(value: 1) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: data rejects cast

This case verifies data rejects cast using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: data-rejects-cast
kind: compileError
level: scenario
sources:
  - name: data-rejects-cast.ges
    program: main
```

### Source code under test

```ges
on Start() { let result be asm { ConstructData result, :Number(1) } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.invalidAssembly }
```

---

## Test: negative symbolic call

This case verifies negative symbolic call using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: negative-symbolic-call
kind: scriptApi
level: scenario
sources:
  - name: negative-symbolic-call.ges
    program: main
```

### Source code under test

```ges
function identity(value) be value
on Start() { let result be asm { Call result, identity(value: -1) }; emit Done(value: result) }
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
              value: "-1"
```

---

## Test: negative symbolic values

This case verifies negative symbolic values using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: negative-symbolic-values
kind: scriptApi
level: scenario
sources:
  - name: negative-symbolic-values.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let result be asm {
        .register list, map, first, second
        CreateList list, [-1, -2]
        CreateMap map, [amount: -3]
        IndexAccess first, 1, list
        MemberAccess second, 'amount', map
        Add result, first, second
    }
    emit Done(value: result)
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
              value: "-4"
```

---

## Test: negative symbolic constructor

This case verifies negative symbolic constructor using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: negative-symbolic-constructor
kind: scriptApi
level: scenario
sources:
  - name: negative-symbolic-constructor.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let result be asm {
        .register point
        CreatePoint point, :Point(x: -1m, y: -2m)
        MemberAccess result, 'y', point
    }
    emit Done(value: result as :Text)
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
              type: ":Text"
              value: "-2m"
```

---

## Test: negative symbolic message

This case verifies negative symbolic message using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: negative-symbolic-message
kind: scriptApi
level: scenario
sources:
  - name: negative-symbolic-message.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { EmitMessage Done(value: -1) } }
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
              value: "-1"
```

---

## Test: negative symbolic emit

This case verifies negative symbolic emit using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: negative-symbolic-emit
kind: scriptApi
level: scenario
sources:
  - name: negative-symbolic-emit.ges
    program: main
```

### Source code under test

```ges
on Start() { asm { emit Done(value: -1) } }
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
              value: "-1"
```

---

## Test: symbolic data constructor

This case verifies symbolic data constructor using the shared portable compiler contract.

### Case description

```yaml
gesBlock: case
id: symbolic-data-constructor
kind: scriptApi
level: scenario
sources:
  - name: symbolic-data-constructor.ges
    program: main
```

### Source code under test

```ges
on Start() {
    let result be asm {
        .register fields, instance
        CreateMap fields, [value: -7]
        ConstructData instance, :Record('Example', fields)
        MemberAccess result, 'value', instance
    }
    emit Done(value: result)
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
              value: "-7"
```
