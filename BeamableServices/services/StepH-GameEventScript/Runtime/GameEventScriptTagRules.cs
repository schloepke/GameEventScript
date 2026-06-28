namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptTagRules
{
    internal static bool IsValidTagName(string? value)
    {
        if (string.IsNullOrEmpty(value) || !char.IsLower(value[0]))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsLetter(value[i]))
            {
                return false;
            }
        }

        return true;
    }

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
