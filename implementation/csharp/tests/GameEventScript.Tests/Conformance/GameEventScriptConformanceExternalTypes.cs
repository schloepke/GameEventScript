// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Conformance;

internal static class GameEventScriptConformanceExternalTypes
{
    private static readonly GameEventScriptExternalTypeDefinition AimDefinition = CreateAimDefinition();

    internal static readonly IGameEventScriptExternalTypeCatalog Catalog =
        new GameEventScriptExternalTypeCatalog([AimDefinition]);

    internal static readonly IGameEventScriptExternalTypeRegistry Registry =
        new AimRuntimeRegistry();

    private static GameEventScriptExternalTypeDefinition CreateAimDefinition()
    {
        const string typeName = "Aim";
        return new GameEventScriptExternalTypeDefinition(
            typeName,
            [
                new GameEventScriptExternalTypeFieldDefinition("bearing", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree),
                new GameEventScriptExternalTypeFieldDefinition("range", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter),
                new GameEventScriptExternalTypeFieldDefinition("steps", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter),
                new GameEventScriptExternalTypeFieldDefinition("direction", GameEventScriptBytecodeTypeKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter),
                new GameEventScriptExternalTypeFieldDefinition("checksum", GameEventScriptBytecodeTypeKind.Float)
            ],
            [
                new GameEventScriptExternalTypeConstructorDefinition(
                    typeName,
                    [
                        new GameEventScriptExternalTypeParameterDefinition("bearing", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree),
                        new GameEventScriptExternalTypeParameterDefinition("range", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter),
                        new GameEventScriptExternalTypeParameterDefinition("steps", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter),
                        new GameEventScriptExternalTypeParameterDefinition("direction", GameEventScriptBytecodeTypeKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)
                    ])
            ]);
    }

    private sealed class AimRuntimeRegistry : IGameEventScriptExternalTypeRegistry
    {
        private readonly AimConstructor _constructor = new(AimDefinition.Constructors[0]);

        public IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference)
            => string.Equals(reference.SignatureId, _constructor.Definition.SignatureId, StringComparison.Ordinal)
                ? _constructor
                : null;
    }

    private sealed class AimConstructor(GameEventScriptExternalTypeConstructorDefinition definition)
        : IGameEventScriptExternalTypeConstructor
    {
        public GameEventScriptExternalTypeConstructorDefinition Definition { get; } = definition;

        public void Invoke(GesExternalTypeConstructorCall call)
        {
            if (call.Arguments.Length != 4)
            {
                call.SetNothing();
                return;
            }

            var bearing = call.Arguments.GetAsNumber(0);
            var range = call.Arguments.GetAsNumber(1);
            var steps = call.Arguments.GetAsNumber(2);
            call.SetExternalValue(new AimValue(
                GesValue.GesFloat(bearing, GameEventScriptBytecodeInstructionUnit.UnitDegree),
                GesValue.GesFloat(range, GameEventScriptBytecodeInstructionUnit.UnitMeter),
                GesValue.GesFloat(steps, GameEventScriptBytecodeInstructionUnit.UnitMeter),
                call.Arguments[3],
                GesValue.GesFloat((long)(bearing + range + steps))));
        }
    }

    private sealed class AimValue(GesValue bearing, GesValue range, GesValue steps, GesValue direction, GesValue checksum) : IGameEventScriptExternalValue
    {
        public GameEventScriptExternalTypeDefinition Definition { get; } = AimDefinition;

        public GesValue? GetField(string fieldName)
            => fieldName switch
            {
                "bearing" => bearing,
                "range" => range,
                "steps" => steps,
                "direction" => direction,
                "checksum" => checksum,
                _ => null
            };
    }
}
