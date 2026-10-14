// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Conformance;

internal static class GameEventScriptConformanceExternalTypes
{
    private static readonly GesExternalTypeDefinition AimDefinition = CreateAimDefinition();
    private static readonly GesExternalTypeDefinition SpatialDefinition = new(
        "SpatialProbe",
        [
            new GesExternalTypeFieldDefinition("vector", GesBytecodeTypeKind.Vector),
            new GesExternalTypeFieldDefinition("point", GesBytecodeTypeKind.Point),
            new GesExternalTypeFieldDefinition("storedVector", "Vector"),
            new GesExternalTypeFieldDefinition("storedPoint", "Point")
        ],
        [new GesExternalTypeConstructorDefinition("SpatialProbe", [new GesExternalTypeParameterDefinition("vector", "Vector"), new GesExternalTypeParameterDefinition("point", "Point")])]);

    internal static readonly IGesExternalTypeCatalog Catalog =
        new GesDeclaredExternalTypeCatalog([AimDefinition, GameEventScriptConformanceCallbackProbe.Definition, SpatialDefinition]);

    internal static readonly IGesExternalTypeRegistry Registry =
        new TestRuntimeRegistry();

    private static GesExternalTypeDefinition CreateAimDefinition()
    {
        const string typeName = "Aim";
        return new GesExternalTypeDefinition(
            typeName,
            [
                new GesExternalTypeFieldDefinition("bearing", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitDegree),
                new GesExternalTypeFieldDefinition("range", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter),
                new GesExternalTypeFieldDefinition("steps", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter),
                new GesExternalTypeFieldDefinition("direction", GesBytecodeTypeKind.Vector, GesQuantityUnit.UnitMeter),
                new GesExternalTypeFieldDefinition("checksum", GesBytecodeTypeKind.Float)
            ],
            [
                new GesExternalTypeConstructorDefinition(
                    typeName,
                    [
                        new GesExternalTypeParameterDefinition("bearing", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitDegree),
                        new GesExternalTypeParameterDefinition("range", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter),
                        new GesExternalTypeParameterDefinition("steps", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitMeter),
                        new GesExternalTypeParameterDefinition("direction", GesBytecodeTypeKind.Vector, GesQuantityUnit.UnitMeter)
                    ])
            ]);
    }

    private sealed class TestRuntimeRegistry : IGesExternalTypeRegistry
    {
        private readonly AimConstructor _constructor = new(AimDefinition.Constructors[0]);
        private readonly SpatialConstructor _spatialConstructor = new();

        public IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference)
            => string.Equals(reference.SignatureId, _constructor.Definition.SignatureId, StringComparison.Ordinal)
                ? _constructor
                : string.Equals(reference.SignatureId, GameEventScriptConformanceCallbackProbe.Constructor.Definition.SignatureId, StringComparison.Ordinal)
                    ? GameEventScriptConformanceCallbackProbe.Constructor
                    : string.Equals(reference.SignatureId, _spatialConstructor.Definition.SignatureId, StringComparison.Ordinal)
                        ? _spatialConstructor
                        : null;
    }

    private sealed class SpatialConstructor : IGesExternalTypeConstructor
    {
        public GesExternalTypeConstructorDefinition Definition { get; } = SpatialDefinition.Constructors[0];

        public void Invoke(GesExternalTypeConstructorCall call)
            => call.SetExternalValue(new SpatialValue(call.Arguments[0], call.Arguments[1]));
    }

    private sealed class SpatialValue(GesValue vector, GesValue point) : IGesExternalValue
    {
        public GesExternalTypeDefinition Definition { get; } = SpatialDefinition;

        public GesValue? GetField(string fieldName)
            => fieldName switch
            {
                "vector" => vector,
                "point" => point,
                "storedVector" => GesValue.GesVector(1, 2, 3, GesQuantityUnit.UnitMeter),
                "storedPoint" => GesValue.GesPoint(4, 5, 6, GesQuantityUnit.UnitSecond),
                _ => null
            };
    }

    private sealed class AimConstructor(GesExternalTypeConstructorDefinition definition)
        : IGesExternalTypeConstructor
    {
        public GesExternalTypeConstructorDefinition Definition { get; } = definition;

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
                GesValue.GesFloat(bearing, GesQuantityUnit.UnitDegree),
                GesValue.GesFloat(range, GesQuantityUnit.UnitMeter),
                GesValue.GesFloat(steps, GesQuantityUnit.UnitMeter),
                call.Arguments[3],
                GesValue.GesFloat((long)(bearing + range + steps))));
        }
    }

    private sealed class AimValue(GesValue bearing, GesValue range, GesValue steps, GesValue direction, GesValue checksum) : IGesExternalValue
    {
        public GesExternalTypeDefinition Definition { get; } = AimDefinition;

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
