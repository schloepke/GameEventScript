// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Api;

/// <summary>An outbound publication staged until its initializing instance succeeds.</summary>
internal readonly record struct GesDeferredPublication(GameEventScriptMessage Message, bool LocalAccepted, GesProgramInstance Instance);
