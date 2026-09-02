---
formatVersion: 1
suiteId: bootstrap.snapshot
kind: bytecodeSnapshot
level: atomic
---

## Fixtures

| input | note |
| --- | --- |
| ignored | V1 documentation only |

## Test: Minimal snapshot

```yaml
gesBlock: case
id: minimal
```

```ges
on Start() {
  emit Done()
}
```

```gesa
.segment code
```
