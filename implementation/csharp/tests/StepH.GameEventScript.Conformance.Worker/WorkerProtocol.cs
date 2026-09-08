// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace StepH.GameEventScript.Conformance.Worker;

internal sealed record WorkerMismatch(string Path, string Code, string? Expected, string? Actual);

internal sealed record WorkerResponse(int Version, string CaseId, string DocumentSha256, string FixtureSha256, string Status, string Code, WorkerMismatch[] Mismatches, string? TechnicalDetails);
