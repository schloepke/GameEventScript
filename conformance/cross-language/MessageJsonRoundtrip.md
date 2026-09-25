<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Cross-language product JSON roundtrip gate

This integration guide supplements the normative [product message format](../../specs/MessageFormat.md).
The independent portable oracles are authored in [product-json.md](../suites/api/product-json.md).

```bash
python3 scripts/test-message-json-roundtrip.py
```

The script builds the executable C# and Swift Conformance adapters. It feeds each
successful Markdown JSON vector to both codecs, exchanges their actual output in
both directions, and compares every result with the independently authored canonical
JSON expectation. Exact comparison covers numeric strings, kind, unit, ordered
arguments, tags, recursive data and Unicode spelling. It does not compare only
one implementation against the other: equal bugs would still fail the oracle.
Two negative controls require rejection of a missing response and a changed value
kind. Invalid input and external-to-Record export are checked by the shared
Markdown suites, not duplicated in this integration script.

`--skip-build` reuses the adapters built by `scripts/test-swift.sh`. That script
runs this gate after native compilation, independent Runtime acceptance, and the
numeric text exchange gate. Inputs, outputs and `summary.json` are written below
`artifacts/swift/message-json-roundtrip`; Swift CI retains those reports.
