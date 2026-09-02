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

This negative loading case exercises “Missing runtime constructor is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: missing-runtime-constructor
externalTypeRegistry: absent
```

### Source code under test

```ges
on Start() {
  let value be :aim(range: 12m, bearing: 90°, steps: 4m, direction: :vector(1m, 2m, 3m))
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: link
  code: link.missingExternalTypeConstructor
```

---

## Test: Mismatched runtime constructor is rejected

This negative loading case exercises “Mismatched runtime constructor is rejected” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: mismatched-runtime-constructor
externalTypeRegistry: mismatch
```

### Source code under test

```ges
on Start() {
  let value be :aim(range: 12m, bearing: 90°, steps: 4m, direction: :vector(1m, 2m, 3m))
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: link
  code: link.mismatchedExternalTypeConstructor
```
