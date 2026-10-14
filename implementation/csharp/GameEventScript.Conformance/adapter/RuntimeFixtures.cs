// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

internal sealed class RuntimeFixtures : IGesExtensionRegistry
{
    /// <summary>Resolves only the explicitly supported shared test extension signatures.</summary>
    public IGesExtensionFunction? Resolve(GesExtensionReference reference)
    {
        if (reference.ExtensionName == "nav" && reference.FunctionName == "isNorth" && reference.ArgumentLabels.Count == 1) return new FixtureFunction("isNorth");
        return reference.SignatureId switch
        {
            "test.echo(_)" => new FixtureFunction("echo"),
            "test.notify(_)" => new FixtureFunction("notify"),
            "test.truth()" => new FixtureFunction("truth"),
            "test.failWithRandomScope()" => new FixtureFunction("failWithRandomScope"),
            "test.declaredFault()" => new FixtureFunction("declaredFault"),
            "test.declaredFaultWithRandomScope()" => new FixtureFunction("declaredFaultWithRandomScope"),
            "test.fail()" => new FixtureFunction("fail"),
            "test.toJson(_)" => new FixtureFunction("toJson"),
            "test.fromJson(_)" => new FixtureFunction("fromJson"),
            "test.vectorSum(_)" => new FixtureFunction("vectorSum"),
            "nav.shortestTurn(from,to)" => new FixtureFunction("shortestTurn"),
            "math.floor(_)" => new FixtureFunction("floor"),
            _ => null
        };
    }

    private sealed class FixtureFunction(string name) : IGesExtensionFunction
    {
        /// <summary>Performs the shared fixture effect; it contains no test assertions.</summary>
        public void Invoke(GesExtensionCall call)
        {
            switch (name)
            {
                case "echo": call.SetValue(call.Arguments[0]); break;
                case "notify":
                    call.Context.Emit("Effect", [new GesMessageArgument("value", call.Arguments[0])]);
                    call.SetValue(call.Arguments[0]);
                    break;
                case "failWithRandomScope":
                    call.Random.Push(7L);
                    call.Random.NextInclusiveInteger(1, 100);
                    throw new InvalidOperationException("Configured conformance extension failure with an open random scope.");
                case "declaredFaultWithRandomScope":
                    call.Random.Push(7L);
                    call.Random.NextInclusiveInteger(1, 100);
                    goto case "declaredFault";
                case "declaredFault": throw new GesExtensionFaultException("test.declaredFault", "Configured conformance declared fault.");
                case "truth": call.SetBoolean(true); break;
                case "floor": call.SetFloat(Math.Floor(call.Arguments.GetAsNumber(0)), call.Arguments.UnitAt(0)); break;
                case "vectorSum":
                    if (call.Arguments.KindAt(0) == GesBytecodeTypeKind.Vector)
                        call.SetFloat(call.Arguments.GetX(0) + call.Arguments.GetY(0) + call.Arguments.GetZ(0), call.Arguments.UnitAt(0));
                    else call.SetNothing();
                    break;
                case "shortestTurn":
                    call.SetFloat((call.Arguments.GetAsNumber(1) - call.Arguments.GetAsNumber(0) + 540d) % 360d - 180d, GesQuantityUnit.UnitDegree);
                    break;
                case "isNorth":
                    var wrapped = ((call.Arguments.GetAsNumber(0) % 360d) + 360d) % 360d;
                    call.SetBoolean(wrapped is <= 45d or >= 315d);
                    break;
                case "toJson":
                    try { call.SetText(call.Arguments[0].ToJson()); }
                    catch (GesMessageFormatException exception) { throw new GesExtensionFaultException(exception.Code, exception.Code); }
                    break;
                case "fromJson":
                    if (call.Arguments[0].ValueKind != GesBytecodeTypeKind.Text) { call.SetNothing(); break; }
                    try { call.SetValue(GesValue.FromJson(call.Arguments[0].AsText()!)); }
                    catch (GesMessageFormatException exception) { throw new GesExtensionFaultException(exception.Code, exception.Code); }
                    break;
                default: throw new InvalidOperationException("Configured conformance extension failure.");
            }
        }
    }
}
