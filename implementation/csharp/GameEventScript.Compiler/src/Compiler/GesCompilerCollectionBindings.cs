// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        private void BindComponents(LoweringContext child, string identifier, IReadOnlyList<string>? names, GesRegisterRef current, ExpressionState state)
        {
            if (names is null || names.Count < 2)
            {
                MaterializeRow(current);
                child.DeclareExisting(identifier, current);
                return;
            }

            if (_componentRows.TryGetValue(current.Id, out var row))
            {
                for (var index = 0; index < names.Count; index++)
                {
                    if (index < row.Components.Length) child.DeclareExisting(names[index], row.Components[index]);
                    else
                    {
                        var missing = state.AllocateTemporary(_builder, child);
                        _builder.LoadNothing(missing);
                        child.DeclareExisting(names[index], missing);
                    }
                }
                return;
            }

            var values = state.AllocateTemporary(_builder, child);
            var check = state.AllocateTemporary(_builder, child);
            var targets = new GesRegisterRef[names.Count];
            for (var index = 0; index < names.Count; index++)
            {
                targets[index] = state.AllocateTemporary(_builder, child);
                child.DeclareExisting(names[index], targets[index]);
            }

            var map = _builder.AddLabel("binding_map");
            var list = _builder.AddLabel("binding_list");
            var done = _builder.AddLabel("binding_done");
            _builder.CheckType(check, current, GameEventScriptBytecodeTypeKind.Map);
            _builder.JumpIfTrue(check, map);
            _builder.Move(values, current);
            _builder.CheckType(check, current, GameEventScriptBytecodeTypeKind.List);
            _builder.JumpIfTrue(check, list);
            _builder.Move(targets[0], current);
            for (var index = 1; index < targets.Length; index++) _builder.LoadNothing(targets[index]);
            _builder.Jump(done);
            _builder.MarkLabel(map);
            _builder.ValuesOfMap(values, current);
            _builder.MarkLabel(list);
            for (var index = 0; index < targets.Length; index++) _builder.IndexAccess(targets[index], checked((ushort)(index + 1)), values);
            _builder.MarkLabel(done);
        }
    }
}
