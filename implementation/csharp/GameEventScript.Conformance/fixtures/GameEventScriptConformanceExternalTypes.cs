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

    private static readonly GesExternalTypeDefinition ReadProbeDefinition = new(
        "ReadProbe",
        [
            new GesExternalTypeFieldDefinition("failOther", GesBytecodeTypeKind.Boolean),
            new GesExternalTypeFieldDefinition("count", GesBytecodeTypeKind.Float),
            new GesExternalTypeFieldDefinition("other", GesBytecodeTypeKind.Float)
        ],
        [new GesExternalTypeConstructorDefinition("ReadProbe", [new GesExternalTypeParameterDefinition("failOther", GesBytecodeTypeKind.Boolean)])]);

    private static readonly GesExternalTypeDefinition OptionalDefinition = new(
        "OptionalProbe",
        [
            new GesExternalTypeFieldDefinition("mount", GesBytecodeTypeKind.Tag),
            new GesExternalTypeFieldDefinition("label", GesBytecodeTypeKind.Text),
            new GesExternalTypeFieldDefinition("armed", GesBytecodeTypeKind.Boolean),
            new GesExternalTypeFieldDefinition("share", GesBytecodeTypeKind.Percentage),
            new GesExternalTypeFieldDefinition("power", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitWatt),
            new GesExternalTypeFieldDefinition("stall", GesBytecodeTypeKind.Tag)
        ],
        [
            new GesExternalTypeConstructorDefinition(
                "OptionalProbe",
                [
                    new GesExternalTypeParameterDefinition("mount", GesBytecodeTypeKind.Tag),
                    new GesExternalTypeParameterDefinition("label", GesBytecodeTypeKind.Text),
                    new GesExternalTypeParameterDefinition("armed", GesBytecodeTypeKind.Boolean),
                    new GesExternalTypeParameterDefinition("share", GesBytecodeTypeKind.Percentage),
                    new GesExternalTypeParameterDefinition("power", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitWatt)
                ])
        ]);

    private static readonly GesExternalTypeDefinition DeclaredDefinition = new(
        "DeclaredProbe",
        [
            new GesExternalTypeFieldDefinition("name", GesBytecodeTypeKind.Text),
            new GesExternalTypeFieldDefinition("power", GesBytecodeTypeKind.Float, GesQuantityUnit.UnitWatt),
            new GesExternalTypeFieldDefinition("heading", GesBytecodeTypeKind.Vector, GesQuantityUnit.UnitMeter),
            new GesExternalTypeFieldDefinition("ratio", GesBytecodeTypeKind.Percentage),
            new GesExternalTypeFieldDefinition("label", GesBytecodeTypeKind.Text),
            new GesExternalTypeFieldDefinition("kind", GesBytecodeTypeKind.Tag),
            new GesExternalTypeFieldDefinition("ready", GesBytecodeTypeKind.Boolean)
        ],
        [new GesExternalTypeConstructorDefinition("DeclaredProbe", [new GesExternalTypeParameterDefinition("name", GesBytecodeTypeKind.Text)])]);

    // The fixtures follow the standard geometry library, which resolves :Box and :Poly in front of them.
    internal static readonly IGesExternalTypeCatalog Catalog = GesGeometryLibrary.Catalog(
        new GesDeclaredExternalTypeCatalog([AimDefinition, GameEventScriptConformanceCallbackProbe.Definition, SpatialDefinition, OptionalDefinition, DeclaredDefinition, ReadProbeDefinition]));

    internal static readonly IGesExternalTypeRegistry Registry = GesGeometryLibrary.ExternalTypes(new TestRuntimeRegistry());

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

    private sealed class ReadProbeConstructor : IGesExternalTypeConstructor
    {
        public GesExternalTypeConstructorDefinition Definition { get; } = ReadProbeDefinition.Constructors[0];

        public void Invoke(GesExternalTypeConstructorCall call) => call.SetExternalValue(new ReadProbeValue(call.Arguments[0].AsBoolean()));
    }

    private sealed class ReadProbeValue(bool failOther) : IGesExternalValue
    {
        private long _reads;

        public GesExternalTypeDefinition Definition { get; } = ReadProbeDefinition;

        public GesValue? GetField(string fieldName)
            => fieldName switch
            {
                "failOther" => GesValue.GesBoolean(failOther),
                "count" => GesValue.GesInteger(++_reads),
                "other" when failOther => throw new InvalidOperationException("Configured field failure."),
                "other" => GesValue.GesInteger(0),
                _ => null
            };
    }

    private sealed class TestRuntimeRegistry : IGesExternalTypeRegistry
    {
        private readonly AimConstructor _constructor = new(AimDefinition.Constructors[0]);
        private readonly SpatialConstructor _spatialConstructor = new();
        private readonly OptionalConstructor _optionalConstructor = new();
        private readonly DeclaredConstructor _declaredConstructor = new();
        private readonly ReadProbeConstructor _readConstructor = new();

        public IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference)
            => string.Equals(reference.SignatureId, _constructor.Definition.SignatureId, StringComparison.Ordinal)
                ? _constructor
                : string.Equals(reference.SignatureId, GameEventScriptConformanceCallbackProbe.Constructor.Definition.SignatureId, StringComparison.Ordinal)
                    ? GameEventScriptConformanceCallbackProbe.Constructor
                    : string.Equals(reference.SignatureId, _spatialConstructor.Definition.SignatureId, StringComparison.Ordinal)
                        ? _spatialConstructor
                        : string.Equals(reference.SignatureId, _optionalConstructor.Definition.SignatureId, StringComparison.Ordinal)
                            ? _optionalConstructor
                            : string.Equals(reference.SignatureId, _declaredConstructor.Definition.SignatureId, StringComparison.Ordinal)
                                ? _declaredConstructor
                                : string.Equals(reference.SignatureId, _readConstructor.Definition.SignatureId, StringComparison.Ordinal)
                                    ? _readConstructor
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

    private sealed class OptionalConstructor : IGesExternalTypeConstructor
    {
        public GesExternalTypeConstructorDefinition Definition { get; } = OptionalDefinition.Constructors[0];

        public void Invoke(GesExternalTypeConstructorCall call)
            => call.SetExternalValue(new OptionalValue(call.Arguments[0], call.Arguments[1], call.Arguments[2], call.Arguments[3], call.Arguments[4]));
    }

    private sealed class OptionalValue(GesValue mount, GesValue label, GesValue armed, GesValue share, GesValue power) : IGesExternalValue
    {
        public GesExternalTypeDefinition Definition { get; } = OptionalDefinition;

        public GesValue? GetField(string fieldName)
            => fieldName switch
            {
                "mount" => mount,
                "label" => label,
                "armed" => armed,
                "share" => share,
                "power" => power,
                "stall" => GesValue.GesNothing(),
                _ => null
            };
    }

    private sealed class DeclaredConstructor : IGesExternalTypeConstructor
    {
        public GesExternalTypeConstructorDefinition Definition { get; } = DeclaredDefinition.Constructors[0];

        public void Invoke(GesExternalTypeConstructorCall call)
            => call.SetExternalValue(new DeclaredValue(call.Arguments[0]));
    }

    // Every field except name returns a value that differs from its declaration.
    private sealed class DeclaredValue(GesValue name) : IGesExternalValue
    {
        public GesExternalTypeDefinition Definition { get; } = DeclaredDefinition;

        public GesValue? GetField(string fieldName)
            => fieldName switch
            {
                "name" => name,
                "power" => GesValue.GesInteger(3),
                "heading" => GesValue.GesVector(1, 2, 3, GesQuantityUnit.UnitSecond),
                "ratio" => GesValue.GesFloat(0.5),
                "label" => GesValue.GesTag("gun"),
                "kind" => GesValue.GesText("turret"),
                "ready" => GesValue.GesInteger(1),
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
