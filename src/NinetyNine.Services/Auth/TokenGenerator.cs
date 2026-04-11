using System.Security.Cryptography;

namespace NinetyNine.Services.Auth;

/// <summary>
/// Generates cryptographically secure, URL-safe random tokens suitable for
/// email verification and password reset links.
/// </summary>
public static class TokenGenerator
{
    /// <summary>
    /// Generates a cryptographically secure random token of <paramref name="byteLength"/> bytes,
    /// encoded as URL-safe base64 without padding characters.
    /// </summary>
    /// <param name="byteLength">
    /// Number of random bytes to generate. Defaults to 32, which produces a 43-character
    /// base64url string with ~256 bits of entropy — negligible collision probability.
    /// </param>
    /// <returns>
    /// A URL-safe base64 string (uses <c>-</c> instead of <c>+</c>, <c>_</c> instead of <c>/</c>,
    /// and has no trailing <c>=</c> padding). Safe to embed directly in query strings.
    /// </returns>
    public static string Generate(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
