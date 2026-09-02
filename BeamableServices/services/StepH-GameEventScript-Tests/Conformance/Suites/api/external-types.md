---
formatVersion: 1
suiteId: "api.external-types"
title: "Portable External Type API"
categories: [conformance]
tags: [portable-api]
---

# Portable External Type API

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the language-neutral external-type catalog contract, including validation that must behave identically in every port.

---

## Test: duplicate external type names are rejected

This public API case exercises “duplicate external type names are rejected” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: duplicate-type-name
kind: externalTypeApi
level: atomic
externalTypeApi:
  typeNames: [sample, sample]
```

### Expectation

```yaml
gesBlock: expect
externalType:
  error: duplicateTypeName
```
