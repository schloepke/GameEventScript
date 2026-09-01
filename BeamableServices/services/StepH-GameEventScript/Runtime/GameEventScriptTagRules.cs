namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptTagRules
{
    internal static bool IsValidTagName(string? value)
        => value is not null && GameEventScriptText.IsTagName(value);

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
