// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

// Explicit transport identifiers: validation must not box values or depend on reflection caches.
// Native tests compare every representable value with the declared enums.
internal static class GesProgramEnumValidation
{
    internal static bool IsDefined(GameEventScriptBytecodeOpCode value)
        => (byte)value <= (byte)GameEventScriptBytecodeOpCode.Default
            || (byte)value >= (byte)GameEventScriptBytecodeOpCode.Or && (byte)value <= (byte)GameEventScriptBytecodeOpCode.AngleBetween3D
            || (byte)value >= (byte)GameEventScriptBytecodeOpCode.TakeFirst && (byte)value <= (byte)GameEventScriptBytecodeOpCode.ParseLiteral;

    internal static bool IsDefined(GameEventScriptBinaryBindKind value)
        => value is GameEventScriptBinaryBindKind.MessageHandler
            or GameEventScriptBinaryBindKind.MessageNameHandler
            or GameEventScriptBinaryBindKind.Function
            or GameEventScriptBinaryBindKind.Predicate
            or GameEventScriptBinaryBindKind.ExtensionCall
            or GameEventScriptBinaryBindKind.OutboundMessage
            or GameEventScriptBinaryBindKind.Record
            or GameEventScriptBinaryBindKind.ExternalType;

    internal static bool IsDefined(GameEventScriptBytecodeInstructionUnit value)
        => value is GameEventScriptBytecodeInstructionUnit.UnitNone
            or GameEventScriptBytecodeInstructionUnit.UnitDegree
            or GameEventScriptBytecodeInstructionUnit.UnitMeter
            or GameEventScriptBytecodeInstructionUnit.UnitSecond
            or GameEventScriptBytecodeInstructionUnit.UnitInvalid;

    internal static bool IsDefined(GameEventScriptBytecodeTypeKind value)
        => value is GameEventScriptBytecodeTypeKind.Nothing
            or GameEventScriptBytecodeTypeKind.Boolean
            or GameEventScriptBytecodeTypeKind.Integer
            or GameEventScriptBytecodeTypeKind.Float
            or GameEventScriptBytecodeTypeKind.Percentage
            or GameEventScriptBytecodeTypeKind.Tag
            or GameEventScriptBytecodeTypeKind.Text
            or GameEventScriptBytecodeTypeKind.Vector
            or GameEventScriptBytecodeTypeKind.Point
            or GameEventScriptBytecodeTypeKind.Range
            or GameEventScriptBytecodeTypeKind.Handler
            or GameEventScriptBytecodeTypeKind.Message
            or GameEventScriptBytecodeTypeKind.List
            or GameEventScriptBytecodeTypeKind.Dice
            or GameEventScriptBytecodeTypeKind.Map
            or GameEventScriptBytecodeTypeKind.ListBuilder
            or GameEventScriptBytecodeTypeKind.MapBuilder
            or GameEventScriptBytecodeTypeKind.DistinctBuilder
            or GameEventScriptBytecodeTypeKind.GroupBuilder
            or GameEventScriptBytecodeTypeKind.OrderBuilder
            or GameEventScriptBytecodeTypeKind.Iterator
            or GameEventScriptBytecodeTypeKind.Series
            or GameEventScriptBytecodeTypeKind.Custom;

    internal static bool IsDefined(GameEventScriptBytecodePatternKind value)
        => value is GameEventScriptBytecodePatternKind.CountAny or GameEventScriptBytecodePatternKind.CountFace or GameEventScriptBytecodePatternKind.FullHouse or GameEventScriptBytecodePatternKind.Straight;

    internal static bool IsDefined(GameEventScriptBytecodeSeriesKind value)
        => value is GameEventScriptBytecodeSeriesKind.Fibonacci or GameEventScriptBytecodeSeriesKind.Factorial;

    internal static bool IsDefined(GameEventScriptDebugSymbolKind value)
        => value is GameEventScriptDebugSymbolKind.Parameter or GameEventScriptDebugSymbolKind.Local;
}
