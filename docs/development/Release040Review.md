<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# 0.4.0 closing review

Initial evidence review on 2026-10-10 against release tag `0.3.0`
(`4ed9d44ded085ddccebff943365fb924bd0a3ce5`) and development commit `791cae6a`.
The branch review was completed on 2026-10-11 for the changes from `e0acce07`
(`origin/main`) through `cd24e907`, including the corrections recorded below.
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
required distribution qualification at that point. Its completed CI qualification
and final defect/limitation disposition are recorded below for this branch.
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
package or release was published. These local checks alone did not complete the
remote matrix; the subsequent CI qualification below supplies that evidence.

## Point 7: completed branch review

The review covered the compiler allocation changes and Swift validator fast path,
reader probes and independent binary generation, upgrade and performance evidence,
Swift CI scope/cache/build reuse, distribution consumers, and release documentation.
Two verification defects were found and fixed:

1. The valid-program generator selected float patterns using the same index as
   the instruction kind. This omitted positive zero, the smallest subnormal and
   the largest finite binary64 value. Float selection now advances independently.
   A regression control decodes the actual instruction payloads and checks all
   advertised integer and floating boundaries with two seeds at the minimum case
   count. It failed before the correction and passes afterward.
2. Performance report validation previously trusted internally consistent case
   counts. A missing case with adjusted counts could pass. Validation now checks
   IDs and kinds against the approved reference inventory for the performance
   suites, accounting for C#'s measured-only reports and Swift's additional GESA
   snapshots. Controls reject missing, substituted and empty inventories.

Targeted verification passed: 33 Python control tests covering binary generation,
reader fuzzing, performance evidence, upgrade evidence, Swift CI results/scope and
API build reuse; 192 default-seed and 1,024 alternate-seed generated Programs
round-tripped through both implementations and both exchange directions. Existing
complete performance reports still validate with 31 measured workloads per
implementation; truncating a report and adjusting its counts is rejected. This
revalidates report completeness, not a new performance measurement.

No unresolved correctness, reliability or integration finding remains from this
branch review. Issue #8 point 7 is complete for this development state, with the
accepted limitations above explicitly retained. The corrections affect test
generation and evidence validation, not product semantics or the bytecode contract.
They add no Swift target or build and keep the CI property sample count unchanged.
At the time of this review, point 6 still required the remote matrix evidence
recorded below. Neither review nor CI qualification grants permission to publish.

## Point 6: completed CI qualification

On 2026-10-11 (Europe/Berlin), all five manually dispatched workflows passed for
the same clean revision, `12b674e4d8e8d54ae76292aa4a433f60cfe1d758`:

| Workflow | Evidence |
| --- | --- |
| [C# CI](https://github.com/schloepke/GameEventScript/actions/runs/38090897227) | 2,483 non-performance test executions, 42 allocation test executions, formatting, fresh package/DLL consumers and package reproducibility passed. |
| [Swift CI](https://github.com/schloepke/GameEventScript/actions/runs/38090899214) | Full manual scope: Linux highlighter, native Conformance/interoperability, API/documentation, isolated tagged SwiftPM consumers and aggregate `verify` passed. The compiler corpus passed 1,953 cases; independent Runtime verification passed 1,558. The strict report intentionally skipped 31 hardware-specific performance cases, measured separately below. |
| [C# Release Candidate](https://github.com/schloepke/GameEventScript/actions/runs/38090901585) | Version 0.4.0, `publish=false`: both 0.3.0 upgrade paths, SwiftPM consumption, four library packages plus symbols, portable CLI and all six native NuGet tool RIDs passed. `publish-nuget` was skipped. |
| [CLI Distribution](https://github.com/schloepke/GameEventScript/actions/runs/38090903706) | Version 0.4.0, `publish=false`: all six native downloads and the complete-set checksum/receipt verification passed. Every receipt records the same revision, clean sources and successful verification. `publish` was skipped. |
| [Website](https://github.com/schloepke/GameEventScript/actions/runs/38090905856) | Fresh C#/Swift API generation, native Card Lab mechanics, WASM/browser checks and the complete site/link build passed. The earlier local stale-API build limitation is resolved by this fresh CI build. Branch execution skipped `publish`. |

Swift CI recorded Apple Swift 6.1.2 (`swiftlang-6.1.2.1.2`,
`clang-1700.0.13.5`) targeting `arm64-apple-macosx15.0`; C# used SDK 10.0.201.
The native Swift job took 20 minutes 51 seconds, excluding its runner queue.
Its separate distribution job ran in parallel for 14 minutes 51 seconds. These
are observations from this run, not timing guarantees.

The same clean revision passed fresh local, profile-matched performance checks:
45 C# native test executions and 31 measured workloads in each implementation.
The receipt is `artifacts/release-performance/run-_kribndt/evidence.json`, using
the documented M3 Max profiles (.NET SDK 10.0.201 and Apple Swift 6.4). Hosted
Conformance results are not substituted for these hardware-specific measurements.

Run/job metadata, logs, artifact digests, upgrade summaries, Swift reports and
the nonpublished NuGet/CLI artifacts are retained under ignored
`artifacts/release-040-ci/12b674e4`; `summary.json` binds the evidence and file
hashes to this revision. All six downloaded CLI archives were checked again
against the complete-set collector and their receipts locally.

Issue #8 point 6 is complete for this revision. No tag, NuGet publication,
GitHub release or website deployment was created. This evidence record does not
transfer qualification to later source changes or replace explicit publication
authorization.

## Remaining publication gates

The successful CI qualification applies to the revision above and follows the
[release qualification checklist](../guide/distribution/Platforms.md#release-qualification).
Before tagging or publishing a later final candidate:

1. Review the final revision and any newly reported defects. Require full C# and
   Swift CI, including tagged SwiftPM consumers, rather than documentation-only
   skipped checks. Collect fresh upgrade and measured performance evidence.
2. Run the nonpublishing release preparation for package metadata, symbols,
   reproducibility, fresh consumers and the approved publication allowlists.
   Require every native NuGet tool target, every standalone CLI target, and
   Website/Card Lab verification for that revision. Existing results on older
   commits do not complete this step.
3. Regenerate and validate the website/API output from the final source, retaining
   its source-identity checks as in the successful workflow above.
4. Move the reviewed Unreleased notes to a dated 0.4.0 section and update applicable
   `Since: Unreleased` annotations as described in the
   [publication procedure](../guide/distribution/Packages.md#publish-a-shared-version).
   Use that versioned section for the release description.

Missing or failed required evidence remains a publication blocker. Qualification
for `12b674e4` is complete; release version/date preparation and publication remain
separate actions. Publication still requires explicit authorization.
