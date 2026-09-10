<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Cross-language capability matrix

This matrix records implementations for the exact shared corpus identified by
their attached result artifacts. A check means the capability passed all cases
that require it. A circle means that no accepted result exists yet. Unity uses
the C# DLL and is not a separate language implementation; its arrow does not
claim that Unity integration testing has already been completed.

| Class | Capability | C#/.NET reference | Unity via C# DLL | Swift | Kotlin | Go | Rust | C++ |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Core | `compiler` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `program-binary` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `host` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `vm` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `message-api` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `value-api` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `external-types` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `native-handlers` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `publish-sink` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Core | `observer` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Optional | `performance` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |
| Optional | `bytecode-snapshot` | ✅ | ↪ | ○ | ○ | ○ | ○ | ○ |

Required Core support may never be represented by a skipped case. A port may
omit an optional capability during development, but it must then advertise that
omission and skip exactly the affected cases with the stable runner code. The
checked-in C# reference is the initial all-capabilities reference.
