---
formatVersion: 1
suiteId: compile.external-type-linking
title: External type dynamic linking
kind: loadError
level: atomic
categories: [conformance]
tags: [external-types, linking]
requires:
  core: [external-types]
---

# External type dynamic linking

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies that portable external-type declarations are compiled independently and linked against the host registry only when loaded.

---

## Test: Missing runtime constructor is rejected

This negative loading case exercises “Missing runtime constructor is rejected” and verifies one located diagnostic per constructor call site.

### Case description

```yaml
gesBlock: case
id: missing-runtime-constructor
externalTypeRegistry: absent
```

### Source code under test

```ges
on Start() {
  let value be :Aim(range: 12m, bearing: 90°, steps: 4m, direction: :Vector(1m, 2m, 3m))
  emit Done(aim: :Aim(range: 1m, bearing: 0°, steps: 1m, direction: :Vector(0m, 0m, 1m)))
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.missingExternalTypeConstructor
    symbol: 'Aim(bearing,direction,range,steps)'
    sourceName: compile.external-type-linking.missing-runtime-constructor.ges
    line: 2
    column: 16
    endLine: 2
    endColumn: 89
  - phase: link
    code: link.missingExternalTypeConstructor
    symbol: 'Aim(bearing,direction,range,steps)'
    sourceName: compile.external-type-linking.missing-runtime-constructor.ges
    line: 3
    column: 18
    endLine: 3
    endColumn: 89
```

---

## Test: Mismatched runtime constructor is rejected

This negative loading case exercises “Mismatched runtime constructor is rejected” and verifies one located diagnostic per constructor call site.

### Case description

```yaml
gesBlock: case
id: mismatched-runtime-constructor
externalTypeRegistry: mismatch
```

### Source code under test

```ges
on Start() {
  let value be :Aim(range: 12m, bearing: 90°, steps: 4m, direction: :Vector(1m, 2m, 3m))
  emit Done(aim: :Aim(range: 1m, bearing: 0°, steps: 1m, direction: :Vector(0m, 0m, 1m)))
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.mismatchedExternalTypeConstructor
    symbol: 'Aim(bearing,direction,range,steps)'
    sourceName: compile.external-type-linking.mismatched-runtime-constructor.ges
    line: 2
    column: 16
    endLine: 2
    endColumn: 89
  - phase: link
    code: link.mismatchedExternalTypeConstructor
    symbol: 'Aim(bearing,direction,range,steps)'
    sourceName: compile.external-type-linking.mismatched-runtime-constructor.ges
    line: 3
    column: 18
    endLine: 3
    endColumn: 89
```
