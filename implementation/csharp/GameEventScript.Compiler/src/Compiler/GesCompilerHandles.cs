// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

// Lowering of handle types: parameter and field checks, `is @Name`, identity members and state reads
// (specs/Language.md#handles, specs/Bytecode.md#handles).
internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        // The handle types the Program names, imported as HandleType bindings.
        private readonly SortedSet<string> _namedHandleTypes = new(StringComparer.Ordinal);

        private void NameHandleType(string name) => _namedHandleTypes.Add(name);

        private void AddHandleTypes()
        {
            foreach (var name in _namedHandleTypes)
            {
                // The validator reported every undeclared type before lowering.
                var type = module.HandleTypes[name];
                _builder.AddHandleType(name, type.LinkHash, type.StateName);
            }
        }

        // A parameter or field of a handle type keeps a matching handle and turns any other value into nothing.
        private void EmitHandleTypeCast(GesRegisterRef destination, GesRegisterRef value, string typeName, LoweringContext? context)
        {
            if (typeName.IndexOf('|') < 0)
            {
                NameHandleType(GesHandleTypeNames.Split(typeName).Name);
                _builder.CastCustom(destination, value, typeName);
                return;
            }

            if (context is null) throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, "GameEventScript binary compiler requires a lowering context for a handle union.");
            var matches = context.AddTemporary();
            var keep = _builder.AddLabel();
            var done = _builder.AddLabel();
            foreach (var alternative in typeName.Split('|'))
            {
                NameHandleType(GesHandleTypeNames.Split(alternative).Name);
                _builder.CheckCustomType(matches, value, alternative);
                _builder.JumpIfTrue(matches, keep);
            }

            _builder.LoadNothing(destination);
            _builder.Jump(done);
            _builder.MarkLabel(keep);
            if (destination.Id != value.Id) _builder.Move(destination, value);
            _builder.MarkLabel(done);
        }

        // `is @Name` and `is @Name~` check the handle type and, with `~`, a carried version.
        private void EmitHandleTypeCheck(GesRegisterRef destination, GesRegisterRef value, string typeName)
        {
            NameHandleType(GesHandleTypeNames.Split(typeName).Name);
            _builder.CheckCustomType(destination, value, typeName);
        }

        // `h.id`, `h.version` and `h.root` read the identity; any other member of a statically typed handle reads its
        // state, which the binding of each possible type locates.
        private void EmitMemberAccessInto(GesMemberAccessExpressionNode member, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var target = EmitExpressionForRead(member.Target, context, state);
            if (StaticTypeName(member.Target, context) is not { } typeName || !GesHandleTypeNames.IsHandleType(typeName))
            {
                _builder.MemberAccess(destination, member.Member, target);
                return;
            }

            switch (member.Member)
            {
                case "id":
                case "version":
                    _builder.MemberAccess(destination, member.Member, target);
                    return;
                case "root":
                    if (GesHandleTypeNames.CommonRoot(typeName, module.HandleTypes) is { } root)
                    {
                        NameHandleType(root.Name);
                        _builder.MemberAccess(destination, "@" + root.Name, target);
                    }
                    else _builder.MemberAccess(destination, member.Member, target);

                    return;
            }

            var names = GesHandleTypeNames.Names(typeName);
            foreach (var name in names) NameHandleType(name);
            _builder.HandleStateRead(destination, member.Member, target, names);
        }

        // `h as :State` of a statically typed handle reads the state of the alternatives with that state schema.
        private void NoteHandleStateCast(GesTypeCastExpressionNode cast, LoweringContext context)
        {
            if (StaticTypeName(cast.Value, context) is not { } typeName || !GesHandleTypeNames.IsHandleType(typeName)) return;
            foreach (var name in GesHandleTypeNames.Names(typeName))
                if (module.HandleTypes.TryGetValue(name, out var type) && string.Equals(type.StateName, cast.TypeName, StringComparison.Ordinal))
                    NameHandleType(name);
        }

        // The static type of an expression as far as handle lowering needs it: bindings with a declared type, record
        // fields, `h.root` of composites and casts.
        private string? StaticTypeName(GesExpressionNode expression, LoweringContext context)
        {
            switch (expression)
            {
                case GesIdentifierExpressionNode identifier:
                    return context.TypeOf(identifier.Name);
                case GesTypeCastExpressionNode cast:
                    return cast.TypeName;
                case GesMemberAccessExpressionNode member:
                {
                    if (StaticTypeName(member.Target, context) is not { } targetType) return null;
                    if (GesHandleTypeNames.IsHandleType(targetType))
                        return member.Member == "root" && GesHandleTypeNames.CommonRoot(targetType, module.HandleTypes) is { } root ? "@" + root.Name : null;
                    if (!module.TypeDefinitions.TryGetValue(targetType, out var record)) return null;
                    foreach (var field in record.Fields)
                        if (string.Equals(field.Name, member.Member, StringComparison.Ordinal)) return field.TypeName;
                    return null;
                }
                default:
                    return null;
            }
        }
    }
}
