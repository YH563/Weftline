namespace Weftline.Language.Syntax;

/// <summary>
/// Small syntax helpers: identifier checks and boolean literal parsing.
/// Kept in one place so Parser and ConditionParser share the same rules.
/// </summary>
internal static class SyntaxFacts
{
    public static bool IsIdentifier(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!IsIdentifierStart(text[0]))
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (!IsIdentifierPart(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsIdentifierStart(char value)
        => char.IsLetter(value) || value == '_';

    public static bool IsIdentifierPart(char value)
        => char.IsLetterOrDigit(value) || value == '_';

    public static bool TryParseBoolean(string text, out bool value)
    {
        switch (text)
        {
            case "true":
                value = true;
                return true;
            case "false":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }
}
