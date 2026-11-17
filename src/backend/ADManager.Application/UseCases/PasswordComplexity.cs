namespace ADManager.Application.UseCases;

public static class PasswordComplexity
{
    private const int MinLength = 8;
    private const string Special = "!@#$%^&*()-_=+[]{};:,.<>?";

    public static string? Validate(string password)
    {
        if (password.Length < MinLength)
        {
            return $"Password must be at least {MinLength} characters.";
        }

        var classes = 0;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => Special.Contains(c))) classes++;

        return classes >= 3
            ? null
            : "Password must contain at least 3 of: uppercase, lowercase, digit, special character.";
    }
}
