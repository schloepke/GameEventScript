# Game Event Script Cross-Language Conformance V1

This document defines how C#, Swift, Kotlin, and C++ prove that they parsed and
executed the same Conformance Markdown V1 corpus. Unity uses the accepted C#
library; Unity integration tests may be reported separately but do not define a
different language implementation.

The normative authoring and execution contracts remain
`ConformanceMarkdownV1.md` and `ConformanceRunnerV1.md`. This document defines
corpus identity, the compact comparison artifact, bootstrap parser fixtures,
capability reporting, and acceptance rules.

## Shared inputs

Every port consumes the same files below without copying or translating them:

- `StepH-GameEventScript-Tests/Conformance/Suites/**/*.md`;
- `StepH-GameEventScript-Tests/Conformance/Fixtures/GesbV1/*` through their
  resource IDs and hashes in the Markdown corpus;
- `StepH-GameEventScript-Tests/Conformance/Fixtures/MarkdownV1/*` through
  `manifest.tsv`.

File paths are packaging details. Suite IDs, full case IDs, resource IDs, and
the bytes authenticated by the manifests are portable identities. A port may
load the files from a filesystem, package resources, or caller-provided memory.

## Corpus identity

`ConformanceCrossLanguageResultJsonWriter.Identify` computes one SHA-256 over
the exact authored Markdown bytes. The input sequence is:

1. ASCII `GES-CONFORMANCE-CORPUS-V1` followed by one zero byte;
2. document count as unsigned 32-bit Little Endian;
3. documents sorted by `suiteId` using Unicode-scalar ordinal order;
4. for every document:
   - Markdown `formatVersion` as unsigned 32-bit Little Endian;
   - byte length of the UTF-8 suite ID as unsigned 32-bit Little Endian;
   - UTF-8 suite-ID bytes;
   - exact document byte length as unsigned 32-bit Little Endian;
   - exact document bytes, including a BOM or original line endings when
     present.

The result is written as 64 uppercase hexadecimal characters. Duplicate suite
or full case IDs, mixed Markdown format versions, an empty corpus, or a count
outside the representable bounds are errors. Discovery order and native file
names do not influence the identity.

Binary resources need not be added to this hash separately: their resource IDs
and expected SHA-256 values are part of the hashed Markdown, and the runner
verifies the resolved bytes before decoding them.

## Compact cross-language result

`ConformanceCrossLanguageResultJsonWriter` accepts the complete parsed corpus
and exactly one result for every case. It rejects missing, duplicate, extra, or
metadata-inconsistent results. It emits strict UTF-8 JSON without BOM, with
`LF`, two-space indentation, a terminal `LF`, and this V1 shape:

```json
{
  "schemaVersion": 1,
  "corpus": {
    "markdownFormatVersion": 1,
    "sha256": "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
    "documentCount": 1,
    "caseCount": 1
  },
  "cases": [
    {
      "id": "runtime.math/integer-add",
      "kind": "scriptApi",
      "level": "atomic",
      "requires": {
        "core": ["compiler", "host", "observer", "publish-sink", "vm"],
        "optional": []
      },
      "status": "passed",
      "code": "conformance.passed"
    }
  ]
}
```

Cases and requirement sets are sorted ordinally. Runner ID, implementation ID,
framework name, execution order, titles, prose, diagnostics, technical detail,
actual assembler, and measured performance values are deliberately absent.
They remain available in the full `ConformanceResults.json` and Markdown
report. The compact artifact is only the stable cross-language comparison
surface.

The checked-in initial reference is
`StepH-GameEventScript-Tests/Conformance/CrossLanguage/CSharpReferenceResults.json`.
The C# adapter writes
`Received/CSharpReferenceResults.received.json` from the results already
collected by the individually discoverable tests and compares it byte-for-byte
with the reference. It does not execute the corpus a second time.

## Comparison and acceptance

A candidate port always emits its full canonical `ConformanceResults.json`, its
human-readable report, and the compact cross-language result. Comparison uses
full case IDs, never a testframework class, method, display name, or execution
position.

Before comparing case outcomes, tooling verifies matching schema version,
Markdown format version, corpus SHA-256, document count, and case count. It then
builds maps by full case ID and rejects missing, additional, duplicate, or
kind/level/requirement-inconsistent entries.

For an accepted complete port:

- every case without a missing optional capability is `passed` with
  `conformance.passed`;
- absence of a required Core capability is always an `error` and fails
  acceptance; it is never converted into a skip;
- a case may be `skipped` only when at least one of its declared optional
  capabilities is absent, the full result uses
  `conformance.runner.missingOptionalCapability`, and the missing set exactly
  reflects the advertised capability set;
- advertising an optional capability makes all cases requiring it mandatory;
- `failed` and `error` outcomes always fail acceptance.

The C# reference supports every V1 Core and optional capability and therefore
contains only passing entries. During development an incomplete port can still
publish a useful report and capability row, but it is not a complete-port
acceptance until every Core capability is present.

Bytecode snapshot cases compare their canonical GESA against the expected block
inside the shared Markdown before they can pass. The compact result therefore
does not duplicate large assembler text. Performance cases likewise execute
their portable correctness portion first. Their profile IDs, reference values,
and measurements may differ by language/platform; only the resulting portable
case status participates in cross-language acceptance.

## Parser bootstrap fixtures

`Fixtures/MarkdownV1/manifest.tsv` is intentionally simpler than Markdown or
YAML so it can test a new parser without depending on that parser. Its exact
format is described beside the manifest. Every port must:

1. verify each fixture's exact SHA-256;
2. parse every `valid` fixture and compare suite and local case IDs;
3. reject every `invalid` fixture and compare the first stable diagnostic code.

Corpus execution provides the broader normalized-model check: a parser that
interprets a valid field differently cannot produce the same outcomes for all
stable case IDs. New parser-boundary ambiguities should first receive a shared
bootstrap fixture and manifest row, then a semantic corpus case where possible.

## Capability matrix

The live implementation matrix is
`StepH-GameEventScript-Tests/Conformance/CrossLanguage/CapabilityMatrix.md`.
Changing a cell requires an attached canonical full result and compact result
for the exact corpus fingerprint. A planned implementation is not marked as
supported. Unity's reuse of the C# DLL is shown separately from independent
Unity integration acceptance.

The matrix is informative status tracking. Capability meaning, case
requirements, and skip/error behavior come exclusively from the normalized
corpus and the runner contract.
