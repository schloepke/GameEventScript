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

        // `@X[:count]` and the other store reads, with a root those of one root's parts (specs/Language.md#stores).
        private void EmitStoreReadInto(GesStoreReadExpressionNode read, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            NameHandleType(read.TypeName.Substring(1));
            if (read.Root is null)
            {
                _builder.ReadStore(read.Selector switch
                {
                    GesStoreSelector.Count => GesBytecodeOpCode.CountStore,
                    GesStoreSelector.Keys => GesBytecodeOpCode.KeysOfStore,
                    GesStoreSelector.Values => GesBytecodeOpCode.ValuesOfStore,
                    _ => GesBytecodeOpCode.EntriesOfStore,
                }, destination, read.TypeName);
                return;
            }

            var root = EmitExpressionForRead(read.Root, context, state);
            _builder.ReadStoreParts(read.Selector switch
            {
                GesStoreSelector.Count => GesBytecodeOpCode.CountParts,
                GesStoreSelector.Keys => GesBytecodeOpCode.KeysOfParts,
                GesStoreSelector.Values => GesBytecodeOpCode.ValuesOfParts,
                _ => GesBytecodeOpCode.EntriesOfParts,
            }, destination, root, read.TypeName);
        }

        // An assembled store read names its type like a source one, when the module declares it.
        private void AssemblyStoreRead(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef? root, string typeName)
        {
            if (typeName.Length > 1 && typeName[0] == '@' && module.HandleTypes.ContainsKey(typeName.Substring(1))) NameHandleType(typeName.Substring(1));
            if (root is { } rootRegister) _builder.ReadStoreParts(opcode, destination, rootRegister, typeName);
            else _builder.ReadStore(opcode, destination, typeName);
        }

        private void AddHandleTypes()
        {
            foreach (var name in _namedHandleTypes)
            {
                // The validator reported every undeclared type before lowering.
                var type = module.HandleTypes[name];
                var projections = new string[type.Projections.Count];
                for (var index = 0; index < projections.Length; index++) projections[index] = type.Projections[index].Name;
                _builder.AddHandleType(name, type.LinkHash, type.StateName, projections);
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
            if (GesHandleTypeNames.HasProjection(typeName))
            {
                // Each alternative gives its own view, so a matching value casts to the alternative it matched.
                var alternatives = typeName.Split('|');
                var labels = new GesLabelRef[alternatives.Length];
                var end = _builder.AddLabel();
                for (var index = 0; index < alternatives.Length; index++)
                {
                    NameHandleType(GesHandleTypeNames.Split(alternatives[index]).Name);
                    labels[index] = _builder.AddLabel();
                    _builder.CheckCustomType(matches, value, alternatives[index]);
                    _builder.JumpIfTrue(matches, labels[index]);
                }

                _builder.LoadNothing(destination);
                _builder.Jump(end);
                for (var index = 0; index < alternatives.Length; index++)
                {
                    _builder.MarkLabel(labels[index]);
                    _builder.CastCustom(destination, value, alternatives[index]);
                    if (index < alternatives.Length - 1) _builder.Jump(end);
                }

                _builder.MarkLabel(end);
                return;
            }

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
                    // A part-less composite holds its root's id as its own, so only `@id` reads its identity as a root
                    // without a part; types whose composites all have a part keep the plain member.
                    _builder.MemberAccess(destination, GesHandleTypeNames.HasPartlessComposite(typeName, module.HandleTypes) ? "@id" : "id", target);
                    return;
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
            // A parameter already holds its view; a handle from elsewhere, such as a Record field a message delivered,
            // takes the static view first, so the read never sees more than the type allows.
            if (GesHandleTypeNames.HasProjection(typeName) && member.Target is not GesIdentifierExpressionNode)
            {
                var viewed = context.AddTemporary();
                EmitHandleTypeCast(viewed, target, typeName, context);
                target = viewed;
            }

            _builder.HandleStateRead(destination, member.Member, target, names);
        }

        // `h as :State` and `h as :P` of a statically typed handle read the state of the alternatives with that state schema
        // or projection. `c as :P` of any other value projects a Record with a projection of a handle type's state, which
        // the Program names so that the host links the schema it was compiled against.
        private void NoteHandleStateCast(GesTypeCastExpressionNode cast, LoweringContext context)
        {
            if (StaticTypeName(cast.Value, context) is { } typeName && GesHandleTypeNames.IsHandleType(typeName))
            {
                foreach (var name in GesHandleTypeNames.Names(typeName))
                    if (module.HandleTypes.TryGetValue(name, out var type) && (string.Equals(type.StateName, cast.TypeName, StringComparison.Ordinal) || type.FindProjection(cast.TypeName) is not null))
                        NameHandleType(name);
                return;
            }

            foreach (var type in module.HandleTypes.Values)
            {
                if (type.FindProjection(cast.TypeName) is null) continue;
                NameHandleType(type.Name);
                return;
            }
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
