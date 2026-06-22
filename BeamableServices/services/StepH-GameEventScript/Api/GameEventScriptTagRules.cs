namespace StepH.GameEventScript.Api;

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

    internal static bool TryNormalizeTextCast(string? value, out string tagName)
    {
        switch (value)
        {
            case "true":
            case "True":
                tagName = "true";
                return true;
            case "false":
            case "False":
                tagName = "false";
                return true;
        }

        if (IsValidTagName(value))
        {
            tagName = value!;
            return true;
        }

        tagName = string.Empty;
        return false;
    }
}
