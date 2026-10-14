// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

// Explicit transport identifiers: validation must not box values or depend on reflection caches.
// Native tests compare every representable value with the declared enums.
internal static class GesProgramEnumValidation
{
    internal static bool IsDefined(GesBytecodeOpCode value)
        => (byte)value <= (byte)GesBytecodeOpCode.Default
            || (byte)value >= (byte)GesBytecodeOpCode.Or && (byte)value <= (byte)GesBytecodeOpCode.AngleBetween3D
            || (byte)value >= (byte)GesBytecodeOpCode.TakeFirst && (byte)value <= (byte)GesBytecodeOpCode.Cartesian;

    internal static bool IsDefined(GesBinaryBindKind value)
        => value is GesBinaryBindKind.MessageHandler
            or GesBinaryBindKind.MessageNameHandler
            or GesBinaryBindKind.Function
            or GesBinaryBindKind.Predicate
            or GesBinaryBindKind.ExtensionCall
            or GesBinaryBindKind.OutboundMessage
            or GesBinaryBindKind.Record
            or GesBinaryBindKind.ExternalType;

    internal static bool IsDefined(GesQuantityUnit value)
        => value is GesQuantityUnit.UnitNone
            or GesQuantityUnit.UnitDegree
            or GesQuantityUnit.UnitMeter
            or GesQuantityUnit.UnitSecond
            or GesQuantityUnit.UnitInvalid;

    internal static bool IsDefined(GesBytecodeTypeKind value)
        => value is GesBytecodeTypeKind.Nothing
            or GesBytecodeTypeKind.Boolean
            or GesBytecodeTypeKind.Integer
            or GesBytecodeTypeKind.Float
            or GesBytecodeTypeKind.Percentage
            or GesBytecodeTypeKind.Tag
            or GesBytecodeTypeKind.Text
            or GesBytecodeTypeKind.Vector
            or GesBytecodeTypeKind.Point
            or GesBytecodeTypeKind.Range
            or GesBytecodeTypeKind.Handler
            or GesBytecodeTypeKind.Message
            or GesBytecodeTypeKind.List
            or GesBytecodeTypeKind.Dice
            or GesBytecodeTypeKind.Map
            or GesBytecodeTypeKind.ListBuilder
            or GesBytecodeTypeKind.MapBuilder
            or GesBytecodeTypeKind.DistinctBuilder
            or GesBytecodeTypeKind.GroupBuilder
            or GesBytecodeTypeKind.OrderBuilder
            or GesBytecodeTypeKind.Iterator
            or GesBytecodeTypeKind.Series
            or GesBytecodeTypeKind.Custom;

    internal static bool IsDefined(GesBytecodePatternKind value)
        => value is GesBytecodePatternKind.CountAny or GesBytecodePatternKind.CountFace or GesBytecodePatternKind.FullHouse or GesBytecodePatternKind.Straight;

    internal static bool IsDefined(GesBytecodeSeriesKind value)
        => value is GesBytecodeSeriesKind.Fibonacci or GesBytecodeSeriesKind.Factorial;

    internal static bool IsDefined(GesDebugSymbolKind value)
        => value is GesDebugSymbolKind.Parameter or GesDebugSymbolKind.Local;
}
