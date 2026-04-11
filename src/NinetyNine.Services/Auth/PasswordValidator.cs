namespace NinetyNine.Services.Auth;

/// <summary>
/// Stateless password complexity validator.
/// All rules must pass; individual failures are collected and returned as a list.
/// </summary>
public static class PasswordValidator
{
    private const int MinimumLength = 10;
    private const string RequiredSymbols = "!@#$%^&*";

    /// <summary>
    /// Validates <paramref name="password"/> against the application password policy.
    /// </summary>
    /// <param name="password">The plain-text password to validate.</param>
    /// <returns>
    /// An empty list when the password satisfies all rules; otherwise a list of
    /// human-readable error messages describing each failing rule.
    /// </returns>
    /// <remarks>
    /// Rules (all must pass):
    /// <list type="bullet">
    ///   <item>Length ≥ 10 characters.</item>
    ///   <item>At least one uppercase letter (A–Z).</item>
    ///   <item>At least one lowercase letter (a–z).</item>
    ///   <item>At least one digit (0–9).</item>
    ///   <item>At least one symbol from the set <c>!@#$%^&amp;*</c>.</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<string> Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            errors.Add("Password must be at least 10 characters long.");

        if (string.IsNullOrEmpty(password) || !password.Any(char.IsUpper))
            errors.Add("Password must contain an uppercase letter.");

        if (string.IsNullOrEmpty(password) || !password.Any(char.IsLower))
            errors.Add("Password must contain a lowercase letter.");

        if (string.IsNullOrEmpty(password) || !password.Any(char.IsDigit))
            errors.Add("Password must contain a digit.");

        if (string.IsNullOrEmpty(password) || !password.Any(c => RequiredSymbols.Contains(c)))
            errors.Add("Password must contain a symbol (!@#$%^&*).");

        return errors;
    }
}
