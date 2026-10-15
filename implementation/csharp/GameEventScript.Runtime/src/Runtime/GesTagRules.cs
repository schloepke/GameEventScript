// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Runtime;

internal static class GesTagRules
{
    internal static bool IsValidTagName(string? value)
        => value is not null && GesText.IsTagName(value);

    internal static string? NormalizeTextCast(string? value)
    {
        switch (value)
        {
            case "true":
            case "True":
                return "true";
            case "false":
            case "False":
                return "false";
        }

        if (IsValidTagName(value))
        {
            return value!;
        }

        return null;
    }
}
