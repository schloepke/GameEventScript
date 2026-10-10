<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# 0.4.0 closing review

Reviewed on 2026-10-10 against release tag `0.3.0`
(`4ed9d44ded085ddccebff943365fb924bd0a3ce5`) and development commit `791cae6a`.
This is a development readiness review, not a release approval or a new
compatibility contract. Release notes remain under
[Unreleased](../../CHANGELOG.md#unreleased) until version/date preparation.

## Completed work and retained evidence

| Area | Disposition |
| --- | --- |
| Reader hardening | Bounded malformed-input checks exercise both implementations with fixed seeds, limits and process timeouts. Canonical/invalid fixtures remain authoritative. See [reader fuzzing](../../conformance/cross-language/BinaryReaderFuzz.md). |
| Binary roundtrips | An independent encoder supplies expected canonical bytes, and actual outputs are exchanged in both directions. The generated subset is not exhaustive Program generation. See [roundtrip properties](../../conformance/cross-language/BinaryRoundtripProperties.md). |
| Selected upgrade path | Pinned 0.3.0 packages/source and frozen binary fixtures qualify selected consumer upgrades. C# requires a clean rebuild to avoid stale CopyLocal DLLs. This is not blanket pre-1.0 binary compatibility. See [upgrade evidence](../../conformance/cross-language/ReleaseUpgrade.md). |
| Supported targets | The [platform matrix](../guide/distribution/Platforms.md) separates full Conformance, package consumption and CLI checks, and requires evidence for the exact candidate. |
| Performance findings | Allocation causes were isolated, unnecessary allocations removed and justified references reviewed. The [performance review](Release040Performance.md) records the original failures and their resolution. |
| Change and migration notes | The changelog now covers inline ASM, compact loops, both choice spellings, predicate shorthand, collection sources, strict numeric aggregates, profiling, Card Lab and hardening. Historical release sections match the 0.3.0 tag. |

The clean `791cae6a` performance receipt is retained locally under ignored
`artifacts/release-performance/run-h24boaah/evidence.json`: 45 C# test executions,
31 C# measured workloads and 31 Swift measured workloads passed. Its source
status is clean and its digest binds the measured checkout. Functionality,
interoperability and security checks are recorded in the performance review.
This receipt qualifies that revision; it cannot stand in for measurements on a
later final release candidate. Generated receipts and logs are not source files.

## Defect and limitation disposition

The open GitHub issue inventory was inspected on 2026-10-10. It contained no
separate known correctness or reliability bug designated as a release blocker.
That is an issue-tracker observation, not proof of absence of defects.
[Release issue #8](https://github.com/schloepke/GameEventScript/issues/8) still
requires distribution qualification and the final defect/limitation disposition.
This review does not close or change the issue.

- **Resolved:** the measured performance failures are explained and addressed;
  zero-allocation dispatch limits and internal-value safety validation remain
  enforced. Card Lab worker/cache mismatches have a versioned-asset fix and
  regression controls. No unresolved failure from those investigations is being
  accepted silently as a limitation.
- **Accepted scope:** version 0.x has no general backwards-compatibility promise.
  GESB V1 does not imply an older runtime understands newer instructions. Source
  recompilation and synchronized package upgrades are the migration path.
- **Accepted support limits:** full Swift Conformance is qualified on macOS
  ARM64 and C# on Linux x64; other matrix entries have narrower checks. WASM
  qualification covers Card Lab, not every SwiftPM product or every browser.
  The staged Unity DLL distribution still lacks a real Unity-project gate.
- **Accepted embedding responsibilities:** host-owned mutations and native
  effects have no automatic rollback. Card Lab provides example rules and UI,
  not networked multiplayer or an authoritative server. Profiling overhead and
  hardware-specific timings are not universal performance guarantees.
- **Separate from this release:** instruction-format review and the independent
  Conformance orchestrator remain 1.0 work; further language ports and optional
  signing/integration work retain their existing scope in the
  [canonical backlog](../../BACKLOG.md). No additional feature is required by
  this closing review.

## Distribution qualification follow-up

Local 0.4.0 package preparation was exercised against the `8f3a2095` development
sources. The review found and fixed a verification defect: existing C# consumer
output directories could retain DLLs from an older version even after restore
and an attempted Rebuild, because canonical artifacts share timestamps and can
share file sizes. Package and staged-DLL smoke tests now allocate separate fresh
build trees for every invocation; package restores also start with an empty
cache. The old contaminated outputs remained in place while the corrected tests
passed twice. Success removes only the newly allocated workspace; failure keeps
it for diagnosis. This changes verification scripts, not library behavior.

The following local checks passed; logs are retained under ignored
`artifacts/release-040-distribution`:

| Check | Local result and boundary |
| --- | --- |
| Four library NuGet packages and four symbol packages | Metadata, dependency graph, canonical package contents and portable PDB checks passed for 0.4.0. |
| Package and staged-DLL consumers | Both routes compiled and executed a fixture; separate Runtime/Bridge-only consumers ran it without Compiler/Conformance. Staged-DLL consumption is not a Unity editor/IL2CPP qualification. |
| Reproducibility | Two independent clean library/package builds produced byte-identical `.nupkg` and `.snupkg` files for all four products. |
| SwiftPM distribution | Fresh tagged-source full and Runtime-only consumers, root-package tests, product/dependency checks and CLI exposure passed. Logs are retained in `artifacts/swift-package/latest-logs`. |
| Portable NuGet CLI | Fresh isolated installation and CLI/editor checks passed. |
| Native NuGet CLI | Fresh macOS ARM64 AOT pointer/RID installation, CLI/editor and delayed-terminal checks passed. Other RIDs require their native jobs. |
| Swift standalone CLI | The macOS ARM64 archive passed relocation, version, compile/dump/run, UTF-8, REPL and delayed-message checks without SDKs on PATH. Its receipt records a dirty tree during the script fix, so it is development evidence, not a publishable final-candidate archive. |
| Publication and installer controls | Fake-client allowlist, missing/stale package, tool-package, installer lifecycle and archive collection controls passed. These are guard tests, not real execution on all six download targets. |

All listed local checks passed. A hash inventory and the scope of remaining
checks are recorded in `artifacts/release-040-distribution/summary.json`. No
package or release was published. The remaining remote matrix results cannot
be inferred from these local checks;
issue #8's distribution criterion remains open until its required candidate
workflow evidence is complete.

## Remaining publication gates

The development steps do not replace the
[release qualification checklist](../guide/distribution/Platforms.md#release-qualification).
Before tagging or publishing the final candidate:

1. Review the final revision and any newly reported defects. Require full C# and
   Swift CI, including tagged SwiftPM consumers, rather than documentation-only
   skipped checks. Collect fresh upgrade and measured performance evidence.
2. Run the nonpublishing release preparation for package metadata, symbols,
   reproducibility, fresh consumers and the approved publication allowlists.
   Require every native NuGet tool target, every standalone CLI target, and
   Website/Card Lab verification for that revision. Existing results on older
   commits do not complete this step.
3. Regenerate and validate the website/API output from the final source. An
   earlier local site build stopped at stale generated API documentation;
   record a successful fresh build rather than bypassing source-identity checks.
4. Move the reviewed Unreleased notes to a dated 0.4.0 section and update applicable
   `Since: Unreleased` annotations as described in the
   [publication procedure](../guide/distribution/Packages.md#publish-a-shared-version).
   Use that versioned section for the release description.

Missing or failed required evidence remains a publication blocker. No tag,
package upload, public release or additional Swift PR build is created by this
review. Publication remains an explicitly authorized later action.
