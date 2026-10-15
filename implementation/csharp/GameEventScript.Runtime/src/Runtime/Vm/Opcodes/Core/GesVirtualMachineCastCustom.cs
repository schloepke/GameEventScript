// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCastCustom(ushort destinationRegister, in GesValue xValue, ushort typeTextPointer)
    {
        var dst = new GesValue();
        var typeName = FetchStringByPointer(typeTextPointer);
        if (IsCustomType(in xValue, typeName))
        {
            if (xValue.Kind is Custom)
            {
                dst = xValue;
                SetValue(destinationRegister, in dst);
                return;
            }

            dst.SetNothing();
            SetValue(destinationRegister, in dst);
            return;
        }

        if (xValue.Kind is Map && xValue.ObjectValue is GesValueMap map)
        {
            for (ushort recordId = 0; recordId < RecordConstructors.Length; recordId++)
            {
                var bind = RecordConstructors[recordId];
                if (bind.Kind is not GesBinaryBindKind.Record ||
                    !string.Equals(FetchStringByPointer(bind.Name), typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                ClearStage();
                for (var argumentIndex = 0; argumentIndex < bind.ArgumentNames.Count; argumentIndex++)
                {
                    var argumentName = FetchStringByPointer(bind.ArgumentNames[argumentIndex]);
                    if (map.Get(argumentName) is { } argument)
                    {
                        var stagedArgument = argument;
                        StageValue(in stagedArgument);
                    }
                    else StageNothing();
                }

                CallRecordConstructor(recordId, destinationRegister);
                return;
            }

            dst.SetNothing();
            SetValue(destinationRegister, in dst);
            return;
        }

        dst.SetNothing();
        SetValue(destinationRegister, in dst);
    }
}
