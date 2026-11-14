// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The helpers of computed fields in generated code. Static code needs only the rules that plain binary64 and Boolean code
/// lacks: no negative zero, the equality of GES numbers, three-valued logic and NaN as nothing. Dynamic code and conversions
/// to GES values also take the exact values of the GES virtual machine: Int64 values with a binary64 fallback, binary64
/// values that are integers when they are whole, percentage ratios, three-valued Booleans and nothing, with the unit checks
/// resolved when the code is generated. Functions and powers add the math helpers, which only a generation that uses them
/// holds. Both generators write identical text.
enum GesComputedLibrary {
    static func write(_ code: inout GesSchemaPlanner.Code, _ helper: String, swift: Bool, values: Bool, math: Bool) {
        var text = (swift ? swiftHead : csharpHead) + (swift ? swiftStatic : csharpStatic)
        if math { text += "\n" + (swift ? swiftMath : csharpMath) }
        if values { text += "\n" + (swift ? swiftValues : csharpValues) }
        if values && math { text += "\n" + (swift ? swiftMathValues : csharpMathValues) }
        text += "}"
        for line in GesComputedEmitter.replace(text, "@HELPER@", helper).split(separator: "\n", omittingEmptySubsequences: false) { code.line(String(line)) }
    }

    /// The C library that holds the math functions of Swift on each platform, which the helpers of a generation call.
    static func mathImport(_ code: inout GesSchemaPlanner.Code) {
        for line in ["#if canImport(Darwin)", "    import Darwin", "#elseif canImport(Glibc)", "    import Glibc", "#elseif canImport(Musl)", "    import Musl", "#elseif canImport(ucrt)", "    import ucrt", "#endif"] {
            code.line(line)
        }
    }

    private static let csharpHead = """
        /// <summary>The rules of computed fields that plain binary64 and Boolean code does not have.</summary>
        internal static class @HELPER@
        {

        """

    private static let swiftHead = """
        /// The rules of computed fields that plain binary64 and Boolean code does not have.
        enum @HELPER@ {

        """

    private static let csharpStatic = """
            /// <summary>GES has no negative zero.</summary>
            internal static double PositiveZero(double value) => value == 0 ? 0 : value;

            /// <summary>The number of an optional field, absent where the expression gives nothing.</summary>
            internal static double? Optional(double value) => double.IsNaN(value) ? null : value == 0 ? 0 : value;

            /// <summary>The ratio of a Percentage field, NaN where the ratio is not finite, which is no percentage.</summary>
            internal static double Ratio(double value) => double.IsInfinity(value) ? double.NaN : value == 0 ? 0 : value;

            /// <summary>The ratio of an optional Percentage field, absent where the expression gives nothing or no finite ratio.</summary>
            internal static double? OptionalRatio(double value) => double.IsNaN(value) || double.IsInfinity(value) ? null : value == 0 ? 0 : value;

            /// <summary>A product as an operation of its own: an addition beside it in one expression could be fused with it.</summary>
            internal static double Product(double a, double b) => a * b;

            internal static double AddPercentage(double value, double ratio) => value + Product(value, ratio);

            internal static double SubtractPercentage(double value, double ratio) => value - Product(value, ratio);

            /// <summary>The smaller of two numbers, the left one on a tie, and nothing where either is.</summary>
            internal static double Min(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : b < a ? b : a;

            /// <summary>The larger of two numbers, the left one on a tie, and nothing where either is.</summary>
            internal static double Max(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : b > a ? b : a;

            internal static double Clamp(double value, double first, double second)
            {
                if (double.IsNaN(value) || double.IsNaN(first) || double.IsNaN(second)) return double.NaN;
                var lower = first <= second ? first : second;
                var upper = first <= second ? second : first;
                return value < lower ? lower : value > upper ? upper : value;
            }

            /// <summary>A number, or the fallback where it is nothing.</summary>
            internal static double Otherwise(double value, double fallback) => double.IsNaN(value) ? fallback : value;

            private static bool Whole(double value) => value >= -9223372036854775808d && value < 9223372036854775808d && value == global::System.Math.Truncate(value);

            private static ulong Ordered(double value)
            {
                var bits = unchecked((ulong)global::System.BitConverter.DoubleToInt64Bits(value));
                return (bits & 0x8000000000000000UL) != 0 ? ~bits : bits | 0x8000000000000000UL;
            }

            private static bool Close(double left, double right)
            {
                if (double.IsNaN(left) || double.IsNaN(right)) return false;
                if (double.IsInfinity(left) || double.IsInfinity(right)) return left == right;
                if (left == right) return true;
                var a = Ordered(left);
                var b = Ordered(right);
                return (a >= b ? a - b : b - a) <= 2;
            }

            /// <summary>Whole numbers are equal when they are the same, others within two units in the last place; nothing is unequal.</summary>
            internal static bool Equal(double a, double b) => Whole(a) && Whole(b) ? a == b : Close(a, b);

            internal static bool? EqualOrNothing(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? null : Equal(a, b);

            /// <summary>Percentages are equal within two units in the last place of their ratios.</summary>
            internal static bool EqualRatios(double a, double b) => Close(a, b);

            internal static bool? EqualRatiosOrNothing(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? null : Close(a, b);

            internal static bool? LessOrNothing(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? null : a < b;

            internal static bool? LessOrEqualOrNothing(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? null : a <= b;

            internal static bool? GreaterOrNothing(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? null : a > b;

            internal static bool? GreaterOrEqualOrNothing(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? null : a >= b;

            /// <summary>Whether two Booleans are equal, nothing where either is.</summary>
            internal static bool? Same(bool? a, bool? b) => a is null || b is null ? null : a == b;

            /// <summary>Three-valued and: false wins over nothing.</summary>
            internal static bool? And(bool? a, bool? b) => a == false || b == false ? false : a == true && b == true ? true : null;

            /// <summary>Three-valued or: true wins over nothing.</summary>
            internal static bool? Or(bool? a, bool? b) => a == true || b == true ? true : a == false && b == false ? false : null;

        """

    private static let swiftStatic = """
            /// GES has no negative zero.
            static func positiveZero(_ value: Double) -> Double { value == 0 ? 0 : value }

            /// The number of an optional field, absent where the expression gives nothing.
            static func optional(_ value: Double) -> Double? { value.isNaN ? nil : value == 0 ? 0 : value }

            /// The ratio of a Percentage field, NaN where the ratio is not finite, which is no percentage.
            static func ratio(_ value: Double) -> Double { value.isInfinite ? .nan : value == 0 ? 0 : value }

            /// The ratio of an optional Percentage field, absent where the expression gives nothing or no finite ratio.
            static func optionalRatio(_ value: Double) -> Double? { value.isNaN || value.isInfinite ? nil : value == 0 ? 0 : value }

            static func addPercentage(_ value: Double, _ ratio: Double) -> Double { value + value * ratio }

            static func subtractPercentage(_ value: Double, _ ratio: Double) -> Double { value - value * ratio }

            /// The smaller of two numbers, the left one on a tie, and nothing where either is.
            static func min(_ a: Double, _ b: Double) -> Double { a.isNaN || b.isNaN ? .nan : b < a ? b : a }

            /// The larger of two numbers, the left one on a tie, and nothing where either is.
            static func max(_ a: Double, _ b: Double) -> Double { a.isNaN || b.isNaN ? .nan : b > a ? b : a }

            static func clamp(_ value: Double, _ first: Double, _ second: Double) -> Double {
                if value.isNaN || first.isNaN || second.isNaN { return .nan }
                let lower = first <= second ? first : second
                let upper = first <= second ? second : first
                return value < lower ? lower : value > upper ? upper : value
            }

            /// A number, or the fallback where it is nothing.
            static func otherwise(_ value: Double, _ fallback: Double) -> Double { value.isNaN ? fallback : value }

            private static func whole(_ value: Double) -> Bool {
                value >= -9_223_372_036_854_775_808.0 && value < 9_223_372_036_854_775_808.0 && value == value.rounded(.towardZero)
            }

            private static func ordered(_ value: Double) -> UInt64 {
                let bits = value.bitPattern
                return bits & 0x8000_0000_0000_0000 != 0 ? ~bits : bits | 0x8000_0000_0000_0000
            }

            private static func close(_ left: Double, _ right: Double) -> Bool {
                if left.isNaN || right.isNaN { return false }
                if left.isInfinite || right.isInfinite { return left == right }
                if left == right { return true }
                let a = ordered(left)
                let b = ordered(right)
                return (a >= b ? a - b : b - a) <= 2
            }

            /// Whole numbers are equal when they are the same, others within two units in the last place; nothing is unequal.
            static func equal(_ a: Double, _ b: Double) -> Bool { whole(a) && whole(b) ? a == b : close(a, b) }

            static func equalOrNothing(_ a: Double, _ b: Double) -> Bool? { a.isNaN || b.isNaN ? nil : equal(a, b) }

            /// Percentages are equal within two units in the last place of their ratios.
            static func equalRatios(_ a: Double, _ b: Double) -> Bool { close(a, b) }

            static func equalRatiosOrNothing(_ a: Double, _ b: Double) -> Bool? { a.isNaN || b.isNaN ? nil : close(a, b) }

            static func lessOrNothing(_ a: Double, _ b: Double) -> Bool? { a.isNaN || b.isNaN ? nil : a < b }

            static func lessOrEqualOrNothing(_ a: Double, _ b: Double) -> Bool? { a.isNaN || b.isNaN ? nil : a <= b }

            static func greaterOrNothing(_ a: Double, _ b: Double) -> Bool? { a.isNaN || b.isNaN ? nil : a > b }

            static func greaterOrEqualOrNothing(_ a: Double, _ b: Double) -> Bool? { a.isNaN || b.isNaN ? nil : a >= b }

            /// Whether two Booleans are equal, nothing where either is.
            static func same(_ a: Bool?, _ b: Bool?) -> Bool? { a == nil || b == nil ? nil : a == b }

            /// Three-valued and: false wins over nothing.
            static func and(_ a: Bool?, _ b: Bool?) -> Bool? { a == false || b == false ? false : a == true && b == true ? true : nil }

            /// Three-valued or: true wins over nothing.
            static func or(_ a: Bool?, _ b: Bool?) -> Bool? { a == true || b == true ? true : a == false && b == false ? false : nil }

        """

    private static let csharpValues = """
            /// <summary>A GES scalar: kind 0 nothing, 1 an Int64, 2 a binary64, 3 a percentage ratio, 4 false, 5 true.</summary>
            internal readonly struct Value
            {
                internal readonly byte Kind;
                internal readonly long Whole;
                internal readonly double Number;

                internal Value(byte kind, long whole, double number)
                {
                    Kind = kind;
                    Whole = whole;
                    Number = number;
                }

                internal double Numeric => Kind == 1 ? Whole : Number;
            }

            internal static readonly Value Nothing = default;

            internal static Value Integer(long value) => new Value(1, value, 0);

            internal static Value Float(double value)
            {
                if (double.IsNaN(value)) return Nothing;
                if (!double.IsInfinity(value) && value >= -9223372036854775808d && value < 9223372036854775808d && value == global::System.Math.Truncate(value)) return Integer((long)value);
                return new Value(2, 0, value);
            }

            internal static Value Percentage(double ratio)
            {
                if (double.IsNaN(ratio)) return Nothing;
                if (ratio == 0) ratio = 0;
                return new Value(double.IsInfinity(ratio) ? (byte)2 : (byte)3, 0, ratio);
            }

            internal static Value Boolean(bool value) => new Value(value ? (byte)5 : (byte)4, 0, 0);

            internal static Value Read(double? value) => value is { } present ? Float(present) : Nothing;

            internal static Value Read(long? value) => value is { } present ? Integer(present) : Nothing;

            internal static Value Read(bool? value) => value is { } present ? Boolean(present) : Nothing;

            internal static Value Read(Value? value) => value ?? Nothing;

            internal static Value ReadPercentage(double? value) => value is { } present ? Percentage(present) : Nothing;

            private static long? AddExact(long left, long right)
            {
                var result = unchecked(left + right);
                return ((left ^ result) & (right ^ result)) < 0 ? null : result;
            }

            private static long? SubtractExact(long left, long right)
            {
                var result = unchecked(left - right);
                return ((left ^ right) & (left ^ result)) < 0 ? null : result;
            }

            private static long? MultiplyExact(long left, long right)
            {
                if (left == 0 || right == 0) return 0;
                if (left == -1) return right == long.MinValue ? null : -right;
                if (right == -1) return left == long.MinValue ? null : -left;
                var result = unchecked(left * right);
                return result / right == left ? result : null;
            }

            internal static Value Add(Value a, Value b, bool sameUnit, bool percentage) => Sum(a, b, sameUnit, percentage, false);

            internal static Value Subtract(Value a, Value b, bool sameUnit, bool percentage) => Sum(a, b, sameUnit, percentage, true);

            private static Value Sum(Value a, Value b, bool sameUnit, bool percentage, bool subtract)
            {
                if (a.Kind == 0 || b.Kind == 0) return Nothing;
                if (a.Kind == 1 && b.Kind == 1)
                {
                    if (!sameUnit) return Nothing;
                    var exact = subtract ? SubtractExact(a.Whole, b.Whole) : AddExact(a.Whole, b.Whole);
                    return exact is { } whole ? Integer(whole) : Float(subtract ? (double)a.Whole - b.Whole : (double)a.Whole + b.Whole);
                }

                if (a.Kind is 1 or 2 && b.Kind is 1 or 2) return sameUnit ? Float(subtract ? a.Numeric - b.Numeric : a.Numeric + b.Numeric) : Nothing;
                if (a.Kind is 1 or 2 && b.Kind == 3)
                {
                    if (!percentage) return Nothing;
                    var x = a.Numeric;
                    return Float(subtract ? SubtractPercentage(x, b.Number) : AddPercentage(x, b.Number));
                }

                return a.Kind == 3 && b.Kind == 3 ? Percentage(subtract ? a.Number - b.Number : a.Number + b.Number) : Nothing;
            }

            internal static Value Multiply(Value a, Value b, bool product, bool leftPercentage, bool rightPercentage)
            {
                if (a.Kind == 0 || b.Kind == 0) return Nothing;
                if (a.Kind == 1 && b.Kind == 1)
                {
                    if (!product) return Nothing;
                    return MultiplyExact(a.Whole, b.Whole) is { } whole ? Integer(whole) : Float((double)a.Whole * b.Whole);
                }

                if (a.Kind is 1 or 2 && b.Kind is 1 or 2) return product ? Float(a.Numeric * b.Numeric) : Nothing;
                if (a.Kind is 1 or 2 && b.Kind == 3) return leftPercentage ? Float(a.Numeric * b.Number) : Nothing;
                if (a.Kind == 3 && b.Kind == 3) return Percentage(a.Number * b.Number);
                if (a.Kind == 3 && b.Kind is 1 or 2) return rightPercentage ? Float(a.Number * b.Numeric) : Nothing;
                return Nothing;
            }

            internal static Value Divide(Value a, Value b, bool quotient, bool leftPercentage, bool rightUnitless)
            {
                if (a.Kind == 0 || b.Kind == 0) return Nothing;
                if (a.Kind is 1 or 2 && b.Kind is 1 or 2) return quotient ? Float(a.Numeric / b.Numeric) : Nothing;
                if (a.Kind == 3 && b.Kind == 3) return Float(a.Number / b.Number);
                if (a.Kind == 3 && b.Kind is 1 or 2) return rightUnitless ? Percentage(a.Number / b.Numeric) : Nothing;
                if (a.Kind is 1 or 2 && b.Kind == 3) return leftPercentage ? Float(a.Numeric / b.Number) : Nothing;
                return Nothing;
            }

            internal static Value Negate(Value a) => a.Kind switch
            {
                1 => a.Whole == long.MinValue ? Float(-(double)a.Whole) : Integer(-a.Whole),
                2 => Float(-a.Number),
                3 => Percentage(-a.Number),
                _ => Nothing
            };

            internal static Value Abs(Value a) => a.Kind switch
            {
                1 => a.Whole == long.MinValue ? Float(-(double)a.Whole) : Integer(global::System.Math.Abs(a.Whole)),
                2 => Float(global::System.Math.Abs(a.Number)),
                3 => Percentage(global::System.Math.Abs(a.Number)),
                _ => Nothing
            };

            internal static Value Compare(Value a, Value b, bool sameUnit, int relation)
            {
                if (a.Kind == 0 || b.Kind == 0) return Nothing;
                if (!sameUnit) return Boolean(false);
                if (a.Kind == 1 && b.Kind == 1)
                    return Boolean(relation == 0 ? a.Whole < b.Whole : relation == 1 ? a.Whole <= b.Whole : relation == 2 ? a.Whole > b.Whole : a.Whole >= b.Whole);
                var x = a.Numeric;
                var y = b.Numeric;
                return Boolean(relation == 0 ? x < y : relation == 1 ? x <= y : relation == 2 ? x > y : x >= y);
            }

            internal static Value Equal(Value a, Value b, bool sameUnit, bool negate)
            {
                if (a.Kind == 0 || b.Kind == 0) return Nothing;
                bool equal;
                if (a.Kind > 3 || b.Kind > 3) equal = a.Kind == b.Kind;
                else if (a.Kind == 1 && b.Kind == 1) equal = sameUnit && a.Whole == b.Whole;
                else equal = sameUnit && Close(a.Numeric, b.Numeric);
                return Boolean(equal != negate);
            }

            internal static Value Not(Value a) => a.Kind == 5 ? Boolean(false) : a.Kind == 4 ? Boolean(true) : Nothing;

            internal static Value And(Value a, Value b)
            {
                if (a.Kind == 4 || b.Kind == 4) return Boolean(false);
                return a.Kind == 5 && b.Kind == 5 ? Boolean(true) : Nothing;
            }

            internal static Value Or(Value a, Value b)
            {
                if (a.Kind == 5 || b.Kind == 5) return Boolean(true);
                return a.Kind == 4 && b.Kind == 4 ? Boolean(false) : Nothing;
            }

            internal static Value Extreme(Value a, Value b, bool sameUnit, bool maximum)
            {
                if (a.Kind == 0 || b.Kind == 0 || !sameUnit) return Nothing;
                var right = a.Kind == 1 && b.Kind == 1
                    ? maximum ? b.Whole > a.Whole : b.Whole < a.Whole
                    : maximum ? b.Numeric > a.Numeric : b.Numeric < a.Numeric;
                return right ? b : a;
            }

            internal static Value Clamp(Value value, Value first, Value second, bool sameUnit)
            {
                if (value.Kind == 0 || first.Kind == 0 || second.Kind == 0 || !sameUnit) return Nothing;
                if (value.Kind == 1 && first.Kind == 1 && second.Kind == 1)
                {
                    var low = first.Whole <= second.Whole ? first.Whole : second.Whole;
                    var high = first.Whole <= second.Whole ? second.Whole : first.Whole;
                    return Integer(value.Whole < low ? low : value.Whole > high ? high : value.Whole);
                }

                var x = value.Numeric;
                var a = first.Numeric;
                var b = second.Numeric;
                var lower = a <= b ? a : b;
                var upper = a <= b ? b : a;
                return Float(x < lower ? lower : x > upper ? upper : x);
            }

            internal static Value When(Value condition, Value value, Value otherwise) => condition.Kind == 5 ? value : otherwise;

            internal static Value Fallback(Value value, Value fallback) => value.Kind == 0 ? fallback : value;

            internal static Value Has(Value value) => Boolean(value.Kind != 0);

            internal static Value CastPercentage(Value value)
            {
                if (value.Kind is not (1 or 2)) return value;
                var ratio = value.Numeric;
                return double.IsInfinity(ratio) ? Nothing : Percentage(ratio);
            }

            internal static Value CastBoolean(Value value) => Boolean(value.Kind == 5);

            internal static double Number(Value value) => value.Kind == 0 ? double.NaN : value.Numeric;

            internal static double? OptionalNumber(Value value) => value.Kind == 0 ? null : value.Numeric;

            internal static bool IsTrue(Value value) => value.Kind == 5;

            internal static global::GameEventScript.Runtime.Values.GesValue ToGesValue(Value value, GesQuantityUnit unit) => value.Kind switch
            {
                1 => global::GameEventScript.Runtime.Values.GesValue.GesInteger(value.Whole, unit),
                2 => global::GameEventScript.Runtime.Values.GesValue.GesFloat(value.Number, unit),
                3 => global::GameEventScript.Runtime.Values.GesValue.GesPercentage(value.Number),
                4 or 5 => global::GameEventScript.Runtime.Values.GesValue.GesBoolean(value.Kind == 5),
                _ => global::GameEventScript.Runtime.Values.GesValue.GesNothing()
            };

        """

    private static let swiftValues = """
            /// A GES scalar: kind 0 nothing, 1 an Int64, 2 a binary64, 3 a percentage ratio, 4 false, 5 true.
            struct Value {
                let kind: UInt8
                let whole: Int64
                let number: Double

                var numeric: Double { kind == 1 ? Double(whole) : number }
            }

            static let nothing = Value(kind: 0, whole: 0, number: 0)

            static func integer(_ value: Int64) -> Value { Value(kind: 1, whole: value, number: 0) }

            static func float(_ value: Double) -> Value {
                if value.isNaN { return nothing }
                if value.isFinite && value >= -9_223_372_036_854_775_808.0 && value < 9_223_372_036_854_775_808.0 && value == value.rounded(.towardZero) { return integer(Int64(value)) }
                return Value(kind: 2, whole: 0, number: value)
            }

            static func percentage(_ ratio: Double) -> Value {
                if ratio.isNaN { return nothing }
                let canonical = ratio == 0 ? 0 : ratio
                return Value(kind: canonical.isInfinite ? 2 : 3, whole: 0, number: canonical)
            }

            static func boolean(_ value: Bool) -> Value { Value(kind: value ? 5 : 4, whole: 0, number: 0) }

            static func read(_ value: Double?) -> Value { value.map(float) ?? nothing }

            static func read(_ value: Int?) -> Value { value.map { integer(Int64($0)) } ?? nothing }

            static func read(_ value: Int64?) -> Value { value.map(integer) ?? nothing }

            static func read(_ value: Bool?) -> Value { value.map(boolean) ?? nothing }

            static func read(_ value: Value?) -> Value { value ?? nothing }

            static func readPercentage(_ value: Double?) -> Value { value.map(percentage) ?? nothing }

            static func add(_ a: Value, _ b: Value, _ sameUnit: Bool, _ percentage: Bool) -> Value { sum(a, b, sameUnit, percentage, false) }

            static func subtract(_ a: Value, _ b: Value, _ sameUnit: Bool, _ percentage: Bool) -> Value { sum(a, b, sameUnit, percentage, true) }

            private static func sum(_ a: Value, _ b: Value, _ sameUnit: Bool, _ percent: Bool, _ subtract: Bool) -> Value {
                if a.kind == 0 || b.kind == 0 { return nothing }
                if a.kind == 1 && b.kind == 1 {
                    if !sameUnit { return nothing }
                    let (exact, overflow) = subtract ? a.whole.subtractingReportingOverflow(b.whole) : a.whole.addingReportingOverflow(b.whole)
                    return overflow ? float(subtract ? Double(a.whole) - Double(b.whole) : Double(a.whole) + Double(b.whole)) : integer(exact)
                }
                if (a.kind == 1 || a.kind == 2) && (b.kind == 1 || b.kind == 2) { return sameUnit ? float(subtract ? a.numeric - b.numeric : a.numeric + b.numeric) : nothing }
                if (a.kind == 1 || a.kind == 2) && b.kind == 3 {
                    if !percent { return nothing }
                    let x = a.numeric
                    return float(subtract ? x - x * b.number : x + x * b.number)
                }
                return a.kind == 3 && b.kind == 3 ? percentage(subtract ? a.number - b.number : a.number + b.number) : nothing
            }

            static func multiply(_ a: Value, _ b: Value, _ product: Bool, _ leftPercentage: Bool, _ rightPercentage: Bool) -> Value {
                if a.kind == 0 || b.kind == 0 { return nothing }
                if a.kind == 1 && b.kind == 1 {
                    if !product { return nothing }
                    let (exact, overflow) = a.whole.multipliedReportingOverflow(by: b.whole)
                    return overflow ? float(Double(a.whole) * Double(b.whole)) : integer(exact)
                }
                let left = a.kind == 1 || a.kind == 2
                let right = b.kind == 1 || b.kind == 2
                if left && right { return product ? float(a.numeric * b.numeric) : nothing }
                if left && b.kind == 3 { return leftPercentage ? float(a.numeric * b.number) : nothing }
                if a.kind == 3 && b.kind == 3 { return percentage(a.number * b.number) }
                if a.kind == 3 && right { return rightPercentage ? float(a.number * b.numeric) : nothing }
                return nothing
            }

            static func divide(_ a: Value, _ b: Value, _ quotient: Bool, _ leftPercentage: Bool, _ rightUnitless: Bool) -> Value {
                if a.kind == 0 || b.kind == 0 { return nothing }
                let left = a.kind == 1 || a.kind == 2
                let right = b.kind == 1 || b.kind == 2
                if left && right { return quotient ? float(a.numeric / b.numeric) : nothing }
                if a.kind == 3 && b.kind == 3 { return float(a.number / b.number) }
                if a.kind == 3 && right { return rightUnitless ? percentage(a.number / b.numeric) : nothing }
                if left && b.kind == 3 { return leftPercentage ? float(a.numeric / b.number) : nothing }
                return nothing
            }

            static func negate(_ a: Value) -> Value {
                switch a.kind {
                case 1: return a.whole == Int64.min ? float(-Double(a.whole)) : integer(-a.whole)
                case 2: return float(-a.number)
                case 3: return percentage(-a.number)
                default: return nothing
                }
            }

            static func abs(_ a: Value) -> Value {
                switch a.kind {
                case 1: return a.whole == Int64.min ? float(-Double(a.whole)) : integer(Swift.abs(a.whole))
                case 2: return float(Swift.abs(a.number))
                case 3: return percentage(Swift.abs(a.number))
                default: return nothing
                }
            }

            static func compare(_ a: Value, _ b: Value, _ sameUnit: Bool, _ relation: Int) -> Value {
                if a.kind == 0 || b.kind == 0 { return nothing }
                if !sameUnit { return boolean(false) }
                if a.kind == 1 && b.kind == 1 {
                    return boolean(relation == 0 ? a.whole < b.whole : relation == 1 ? a.whole <= b.whole : relation == 2 ? a.whole > b.whole : a.whole >= b.whole)
                }
                let x = a.numeric
                let y = b.numeric
                return boolean(relation == 0 ? x < y : relation == 1 ? x <= y : relation == 2 ? x > y : x >= y)
            }

            static func equal(_ a: Value, _ b: Value, _ sameUnit: Bool, _ negate: Bool) -> Value {
                if a.kind == 0 || b.kind == 0 { return nothing }
                let equal: Bool
                if a.kind > 3 || b.kind > 3 {
                    equal = a.kind == b.kind
                } else if a.kind == 1 && b.kind == 1 {
                    equal = sameUnit && a.whole == b.whole
                } else {
                    equal = sameUnit && close(a.numeric, b.numeric)
                }
                return boolean(equal != negate)
            }

            static func not(_ a: Value) -> Value { a.kind == 5 ? boolean(false) : a.kind == 4 ? boolean(true) : nothing }

            static func and(_ a: Value, _ b: Value) -> Value {
                if a.kind == 4 || b.kind == 4 { return boolean(false) }
                return a.kind == 5 && b.kind == 5 ? boolean(true) : nothing
            }

            static func or(_ a: Value, _ b: Value) -> Value {
                if a.kind == 5 || b.kind == 5 { return boolean(true) }
                return a.kind == 4 && b.kind == 4 ? boolean(false) : nothing
            }

            static func extreme(_ a: Value, _ b: Value, _ sameUnit: Bool, _ maximum: Bool) -> Value {
                if a.kind == 0 || b.kind == 0 || !sameUnit { return nothing }
                let right =
                    a.kind == 1 && b.kind == 1
                    ? (maximum ? b.whole > a.whole : b.whole < a.whole) : (maximum ? b.numeric > a.numeric : b.numeric < a.numeric)
                return right ? b : a
            }

            static func clamp(_ value: Value, _ first: Value, _ second: Value, _ sameUnit: Bool) -> Value {
                if value.kind == 0 || first.kind == 0 || second.kind == 0 || !sameUnit { return nothing }
                if value.kind == 1 && first.kind == 1 && second.kind == 1 {
                    let low = first.whole <= second.whole ? first.whole : second.whole
                    let high = first.whole <= second.whole ? second.whole : first.whole
                    return integer(value.whole < low ? low : value.whole > high ? high : value.whole)
                }
                let x = value.numeric
                let a = first.numeric
                let b = second.numeric
                let lower = a <= b ? a : b
                let upper = a <= b ? b : a
                return float(x < lower ? lower : x > upper ? upper : x)
            }

            static func when(_ condition: Value, _ value: Value, _ otherwise: Value) -> Value { condition.kind == 5 ? value : otherwise }

            static func fallback(_ value: Value, _ fallback: Value) -> Value { value.kind == 0 ? fallback : value }

            static func has(_ value: Value) -> Value { boolean(value.kind != 0) }

            static func castPercentage(_ value: Value) -> Value {
                if value.kind != 1 && value.kind != 2 { return value }
                let ratio = value.numeric
                return ratio.isInfinite ? nothing : percentage(ratio)
            }

            static func castBoolean(_ value: Value) -> Value { boolean(value.kind == 5) }

            static func number(_ value: Value) -> Double { value.kind == 0 ? .nan : value.numeric }

            static func optionalNumber(_ value: Value) -> Double? { value.kind == 0 ? nil : value.numeric }

            static func isTrue(_ value: Value) -> Bool { value.kind == 5 }

            static func gesValue(_ value: Value, _ unit: GesQuantityUnit) -> GesValue {
                switch value.kind {
                case 1: return .integer(value.whole, unit: unit)
                case 2: return .float(value.number, unit: unit)
                case 3: return .percentage(value.number)
                case 4, 5: return .boolean(value.kind == 5)
                default: return .nothing
                }
            }

        """

    private static let csharpMath = """
            /// <summary>A power, nothing where either operand is: the platform's pow, and the correctly rounded square root for the exponent 0.5.</summary>
            internal static double Power(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : b == 0.5 ? SquareRoot(a) : global::System.Math.Pow(a, b);

            /// <summary>The correctly rounded square root, and infinity for negative infinity, as the power with the exponent 0.5 gives.</summary>
            internal static double SquareRoot(double value) => double.IsNegativeInfinity(value) ? double.PositiveInfinity : global::System.Math.Sqrt(value);

            /// <summary>The power with the exponent 1/3 that a cube root is; a negative number has none.</summary>
            internal static double CubeRoot(double value) => global::System.Math.Pow(value, 1.0 / 3.0);

            internal static double Sine(double radians) => global::System.Math.Sin(radians);

            internal static double Cosine(double radians) => global::System.Math.Cos(radians);

            internal static double Tangent(double radians) => global::System.Math.Tan(radians);

            internal static double Arcsine(double value) => global::System.Math.Asin(value);

            internal static double Arccosine(double value) => global::System.Math.Acos(value);

            internal static double Arctangent(double value) => global::System.Math.Atan(value);

            /// <summary>The angle of the direction (x, y) in radians, 0 for (0, 0).</summary>
            internal static double Arctangent(double y, double x) => y == 0 && x == 0 ? 0 : global::System.Math.Atan2(y, x);

            internal static double Hypotenuse(double a, double b) => global::System.Math.Sqrt(Product(a, a) + Product(b, b));

            internal static double Hypotenuse(double a, double b, double c) => global::System.Math.Sqrt(Product(a, a) + Product(b, b) + Product(c, c));

            internal static double Exponential(double value) => global::System.Math.Exp(value);

            internal static double Logarithm(double value) => global::System.Math.Log(value);

            /// <summary>Rounds to the nearest integer and a half toward zero.</summary>
            internal static double RoundHalfDown(double value)
            {
                var absolute = global::System.Math.Abs(value);
                var floor = global::System.Math.Floor(absolute);
                var rounded = absolute - floor > 0.5 ? floor + 1 : floor;
                return value < 0 ? -rounded : rounded;
            }

            /// <summary>Degrees in radians, NaN for an infinite angle.</summary>
            internal static double Radians(double degrees) => double.IsInfinity(degrees) ? double.NaN : degrees / 180 * global::System.Math.PI;

            /// <summary>Radians in degrees, NaN for an infinite angle.</summary>
            internal static double Degrees(double radians) => double.IsInfinity(radians) ? double.NaN : radians / global::System.Math.PI * 180;

        """

    private static let swiftMath = """
            /// A power, nothing where either operand is: the platform's pow, and the correctly rounded square root for the exponent 0.5.
            static func power(_ a: Double, _ b: Double) -> Double { a.isNaN || b.isNaN ? .nan : b == 0.5 ? squareRoot(a) : pow(a, b) }

            /// The correctly rounded square root, and infinity for negative infinity, as the power with the exponent 0.5 gives.
            static func squareRoot(_ value: Double) -> Double { value == -.infinity ? .infinity : value.squareRoot() }

            /// The power with the exponent 1/3 that a cube root is; a negative number has none.
            static func cubeRoot(_ value: Double) -> Double { pow(value, 1.0 / 3.0) }

            static func sine(_ radians: Double) -> Double { sin(radians) }

            static func cosine(_ radians: Double) -> Double { cos(radians) }

            static func tangent(_ radians: Double) -> Double { tan(radians) }

            static func arcsine(_ value: Double) -> Double { asin(value) }

            static func arccosine(_ value: Double) -> Double { acos(value) }

            static func arctangent(_ value: Double) -> Double { atan(value) }

            /// The angle of the direction (x, y) in radians, 0 for (0, 0).
            static func arctangent(_ y: Double, _ x: Double) -> Double { y == 0 && x == 0 ? 0 : atan2(y, x) }

            static func hypotenuse(_ a: Double, _ b: Double) -> Double { (a * a + b * b).squareRoot() }

            static func hypotenuse(_ a: Double, _ b: Double, _ c: Double) -> Double { (a * a + b * b + c * c).squareRoot() }

            static func exponential(_ value: Double) -> Double { exp(value) }

            static func logarithm(_ value: Double) -> Double { log(value) }

            /// Rounds to the nearest integer and a half toward zero.
            static func roundHalfDown(_ value: Double) -> Double {
                let absolute = Swift.abs(value)
                let floor = absolute.rounded(.down)
                let rounded = absolute - floor > 0.5 ? floor + 1 : floor
                return value < 0 ? -rounded : rounded
            }

            /// Degrees in radians, NaN for an infinite angle.
            static func radians(_ degrees: Double) -> Double { degrees.isInfinite ? .nan : degrees / 180 * .pi }

            /// Radians in degrees, NaN for an infinite angle.
            static func degrees(_ radians: Double) -> Double { radians.isInfinite ? .nan : radians / .pi * 180 }

        """

    private static let csharpMathValues = """
            internal static Value Power(Value a, Value b) => a.Kind == 0 || b.Kind == 0 ? Nothing : Float(Power(a.Numeric, b.Numeric));

            internal static Value SquareRoot(Value a) => a.Kind == 0 ? Nothing : Float(SquareRoot(a.Numeric));

            internal static Value CubeRoot(Value a) => a.Kind == 0 ? Nothing : Float(CubeRoot(a.Numeric));

            internal static Value Sine(Value a, bool degrees) => a.Kind == 0 ? Nothing : Float(Sine(degrees ? Radians(a.Numeric) : a.Numeric));

            internal static Value Cosine(Value a, bool degrees) => a.Kind == 0 ? Nothing : Float(Cosine(degrees ? Radians(a.Numeric) : a.Numeric));

            internal static Value Tangent(Value a, bool degrees) => a.Kind == 0 ? Nothing : Float(Tangent(degrees ? Radians(a.Numeric) : a.Numeric));

            internal static Value Arcsine(Value a) => a.Kind == 0 ? Nothing : Float(Arcsine(a.Numeric));

            internal static Value Arccosine(Value a) => a.Kind == 0 ? Nothing : Float(Arccosine(a.Numeric));

            internal static Value Arctangent(Value a) => a.Kind == 0 ? Nothing : Float(Arctangent(a.Numeric));

            internal static Value Arctangent(Value y, Value x) => y.Kind == 0 || x.Kind == 0 ? Nothing : Float(Arctangent(y.Numeric, x.Numeric));

            internal static Value Hypotenuse(Value a, Value b) => a.Kind == 0 || b.Kind == 0 ? Nothing : Float(Hypotenuse(a.Numeric, b.Numeric));

            internal static Value Hypotenuse(Value a, Value b, Value c) => a.Kind == 0 || b.Kind == 0 || c.Kind == 0 ? Nothing : Float(Hypotenuse(a.Numeric, b.Numeric, c.Numeric));

            internal static Value Exponential(Value a) => a.Kind == 0 ? Nothing : Float(Exponential(a.Numeric));

            internal static Value Logarithm(Value a) => a.Kind == 0 ? Nothing : Float(Logarithm(a.Numeric));

            /// <summary>A rounded number is an Int64, saturated at its bounds.</summary>
            private static Value Saturated(double value) => Integer(value >= 9223372036854775808d ? long.MaxValue : value < -9223372036854775808d ? long.MinValue : (long)value);

            internal static Value Floor(Value a) => a.Kind == 0 ? Nothing : Saturated(global::System.Math.Floor(a.Numeric));

            internal static Value Ceiling(Value a) => a.Kind == 0 ? Nothing : Saturated(global::System.Math.Ceiling(a.Numeric));

            internal static Value Truncate(Value a) => a.Kind == 0 ? Nothing : Saturated(global::System.Math.Truncate(a.Numeric));

            internal static Value RoundHalfEven(Value a) => a.Kind == 0 ? Nothing : Saturated(global::System.Math.Round(a.Numeric, global::System.MidpointRounding.ToEven));

            internal static Value RoundHalfUp(Value a) => a.Kind == 0 ? Nothing : Saturated(global::System.Math.Round(a.Numeric, global::System.MidpointRounding.AwayFromZero));

            internal static Value RoundHalfDown(Value a) => a.Kind == 0 ? Nothing : Saturated(RoundHalfDown(a.Numeric));

            internal static Value Radians(Value a) => a.Kind == 0 ? Nothing : Float(Radians(a.Numeric));

            internal static Value Degrees(Value a) => a.Kind == 0 ? Nothing : Float(Degrees(a.Numeric));

        """

    private static let swiftMathValues = """
            static func power(_ a: Value, _ b: Value) -> Value { a.kind == 0 || b.kind == 0 ? nothing : float(power(a.numeric, b.numeric)) }

            static func squareRoot(_ a: Value) -> Value { a.kind == 0 ? nothing : float(squareRoot(a.numeric)) }

            static func cubeRoot(_ a: Value) -> Value { a.kind == 0 ? nothing : float(cubeRoot(a.numeric)) }

            static func sine(_ a: Value, _ degrees: Bool) -> Value { a.kind == 0 ? nothing : float(sine(degrees ? radians(a.numeric) : a.numeric)) }

            static func cosine(_ a: Value, _ degrees: Bool) -> Value { a.kind == 0 ? nothing : float(cosine(degrees ? radians(a.numeric) : a.numeric)) }

            static func tangent(_ a: Value, _ degrees: Bool) -> Value { a.kind == 0 ? nothing : float(tangent(degrees ? radians(a.numeric) : a.numeric)) }

            static func arcsine(_ a: Value) -> Value { a.kind == 0 ? nothing : float(arcsine(a.numeric)) }

            static func arccosine(_ a: Value) -> Value { a.kind == 0 ? nothing : float(arccosine(a.numeric)) }

            static func arctangent(_ a: Value) -> Value { a.kind == 0 ? nothing : float(arctangent(a.numeric)) }

            static func arctangent(_ y: Value, _ x: Value) -> Value { y.kind == 0 || x.kind == 0 ? nothing : float(arctangent(y.numeric, x.numeric)) }

            static func hypotenuse(_ a: Value, _ b: Value) -> Value { a.kind == 0 || b.kind == 0 ? nothing : float(hypotenuse(a.numeric, b.numeric)) }

            static func hypotenuse(_ a: Value, _ b: Value, _ c: Value) -> Value {
                a.kind == 0 || b.kind == 0 || c.kind == 0 ? nothing : float(hypotenuse(a.numeric, b.numeric, c.numeric))
            }

            static func exponential(_ a: Value) -> Value { a.kind == 0 ? nothing : float(exponential(a.numeric)) }

            static func logarithm(_ a: Value) -> Value { a.kind == 0 ? nothing : float(logarithm(a.numeric)) }

            /// A rounded number is an Int64, saturated at its bounds.
            private static func saturated(_ value: Double) -> Value {
                integer(value >= 9_223_372_036_854_775_808.0 ? Int64.max : value < -9_223_372_036_854_775_808.0 ? Int64.min : Int64(value))
            }

            static func floor(_ a: Value) -> Value { a.kind == 0 ? nothing : saturated(a.numeric.rounded(.down)) }

            static func ceiling(_ a: Value) -> Value { a.kind == 0 ? nothing : saturated(a.numeric.rounded(.up)) }

            static func truncate(_ a: Value) -> Value { a.kind == 0 ? nothing : saturated(a.numeric.rounded(.towardZero)) }

            static func roundHalfEven(_ a: Value) -> Value { a.kind == 0 ? nothing : saturated(a.numeric.rounded(.toNearestOrEven)) }

            static func roundHalfUp(_ a: Value) -> Value { a.kind == 0 ? nothing : saturated(a.numeric.rounded(.toNearestOrAwayFromZero)) }

            static func roundHalfDown(_ a: Value) -> Value { a.kind == 0 ? nothing : saturated(roundHalfDown(a.numeric)) }

            static func radians(_ a: Value) -> Value { a.kind == 0 ? nothing : float(radians(a.numeric)) }

            static func degrees(_ a: Value) -> Value { a.kind == 0 ? nothing : float(degrees(a.numeric)) }

        """
}
