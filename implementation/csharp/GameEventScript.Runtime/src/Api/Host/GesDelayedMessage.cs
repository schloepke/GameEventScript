// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Api;

/// <summary>A message or publication waiting for its monotonic deadline.</summary>
internal readonly record struct GesDelayedMessage(long Deadline, GesPendingMessage Pending, bool Publish, GesProgramInstance? Owner);
