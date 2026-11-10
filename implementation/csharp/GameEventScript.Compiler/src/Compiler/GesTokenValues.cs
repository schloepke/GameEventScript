// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Compiler;

// Numeric token values follow the runtime number rules, so they belong to the Compiler rather than to the lexer.
internal static class GesTokenValues
{
    extension(GesToken token)
    {
        public double FloatValue => token.NumericValue.AsNumber();

        private GesValue NumericValue => GesTextNumberCast.Read(token.Text, percentage: token.Kind == GesTokenKind.Percentage,
            allowGrouping: false, percentageMagnitude: token.Kind == GesTokenKind.Percentage) ?? GesValue.GesNothing();

        // The unit type name of a UnitNumber, from the unit spelling that ends it, as the lexer matched it.
        public string UnitName => GesQuantityUnits.TryMatchTrailingSuffix(token.Text, 0, token.Text.Length, out var suffix) ? suffix.Unit.ToTypeName() : string.Empty;

        public long? GetIntegerValue()
        {
            var value = token.NumericValue;
            return value.Kind == GesBytecodeTypeKind.Integer ? value.IntegerValue : null;
        }
    }
}
