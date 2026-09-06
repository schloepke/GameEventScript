---
formatVersion: 1
suiteId: bootstrap.invalid-step
kind: scriptApi
level: atomic
---

## Test: Unknown expectation step

```yaml
gesBlock: case
id: unknown
```

```ges
on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

```yaml
gesBlock: expect
steps:
  missing:
    accepted: true
```
