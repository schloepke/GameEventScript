---
formatVersion: 1
suiteId: bootstrap.message-api
kind: messageApi
level: atomic
---

## Test: Signature mismatch

```yaml
gesBlock: case
id: signature-mismatch
messageApi:
  signature:
    name: Start
    parameters: [a, b]
  message:
    name: Start
    args: []
```

```yaml
gesBlock: expect
message:
  name: Start
  signatureId: "Start(a,b)"
  messageSignatureId: "Start()"
  matches: false
  argumentCount: 0
```
