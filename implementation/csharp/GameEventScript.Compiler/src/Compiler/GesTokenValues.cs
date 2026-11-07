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

        public long? GetIntegerValue()
        {
            var value = token.NumericValue;
            return value.Kind == GesBytecodeTypeKind.Integer ? value.IntegerValue : null;
        }
    }
}
