---
formatVersion: 1
suiteId: compile.extension-linking
title: Extension dynamic linking
kind: loadError
level: atomic
categories: [conformance]
tags: [extensions, linking, diagnostics]
---

# Extension dynamic linking

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies that a missing extension fails loading with one link diagnostic per call site, located through the Program's SourceMap and SourceArchive.

---

## Test: Missing extension reports every call site

A Program that calls a missing extension twice reports one diagnostic per call site, in source order, each naming the extension and covering the call.

### Case description

```yaml
gesBlock: case
id: missing-extension-call-sites
sources:
  - name: missing-extension-call-sites.ges
    program: main
```

### Source code under test

```ges
on Boot(bot) {
  publish Probe(value: :navigation.nope(x: bot))
}
function probe(_ value) be [1, :navigation.nope(x: value)]
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.missingExtension
    symbol: navigation.nope(x)
    sourceName: missing-extension-call-sites.ges
    line: 2
    column: 24
    endLine: 2
    endColumn: 48
  - phase: link
    code: link.missingExtension
    symbol: navigation.nope(x)
    sourceName: missing-extension-call-sites.ges
    line: 4
    column: 32
    endLine: 4
    endColumn: 58
```

---

## Test: Missing extension without a SourceArchive reports lines

Without an archived source the call sites keep their lines but no columns.

### Case description

```yaml
gesBlock: case
id: missing-extension-lines-only
compile:
  debugInfo: [sourceMap]
sources:
  - name: missing-extension-lines-only.ges
    program: main
```

### Source code under test

```ges
on Boot(bot) {
  publish Probe(value: :navigation.nope(x: bot))
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.missingExtension
    symbol: navigation.nope(x)
    sourceName: missing-extension-lines-only.ges
    line: 2
    column: null
    endLine: 2
    endColumn: null
```

---

## Test: Missing extension without debug information reports the symbol

Without a SourceMap the failure keeps one diagnostic with the symbol and no location.

### Case description

```yaml
gesBlock: case
id: missing-extension-without-debug-info
compile:
  debugInfo: []
sources:
  - name: missing-extension-without-debug-info.ges
    program: main
```

### Source code under test

```ges
on Boot(bot) {
  publish Probe(value: :navigation.nope(x: bot), again: :navigation.nope(x: 2))
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.missingExtension
    symbol: navigation.nope(x)
    sourceName: null
    line: null
    column: null
```
