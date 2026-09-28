// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace GameEventScript.Tool;

// CLI display strings use generated metadata so native AOT needs no reflection.
[JsonSerializable(typeof(string))]
internal partial class ToolJsonContext : JsonSerializerContext;
