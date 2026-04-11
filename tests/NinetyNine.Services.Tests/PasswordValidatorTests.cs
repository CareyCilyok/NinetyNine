using NinetyNine.Services.Auth;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Pure unit tests for <see cref="PasswordValidator"/>. No external dependencies.
/// Validates all five password complexity rules in isolation and in combination.
/// </summary>
public class PasswordValidatorTests
{
    // ── Empty / null ───────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyString_ReturnsAllErrors()
    {
        var errors = PasswordValidator.Validate("");
        // All five rules fire on an empty password.
        errors.Should().HaveCount(5);
        errors.Should().Contain(e => e.Contains("10 characters"));
        errors.Should().Contain(e => e.Contains("uppercase"));
        errors.Should().Contain(e => e.Contains("lowercase"));
        errors.Should().Contain(e => e.Contains("digit"));
        errors.Should().Contain(e => e.Contains("symbol"));
    }

    // ── Length rule ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ab1!xyz")]          // 7 chars
    [InlineData("Ab1!xyzab")]        // 9 chars
    public void Validate_TooShort_ReturnsLengthError(string password)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().Contain(e => e.Contains("10 characters"),
            because: $"'{password}' is fewer than 10 characters");
    }

    [Fact]
    public void Validate_ExactlyTenChars_WithAllRules_Passes()
    {
        // "Test1234!a" — 10 chars, upper, lower, digit, symbol from allowed set
        var errors = PasswordValidator.Validate("Test1234!a");
        errors.Should().BeEmpty("'Test1234!a' satisfies all five rules at exactly 10 characters");
    }

    // ── Uppercase rule ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_MissingUppercase_ReturnsUppercaseError()
    {
        // 10 chars, lowercase, digit, symbol — no uppercase
        var errors = PasswordValidator.Validate("test1234!a");
        errors.Should().ContainSingle(e => e.Contains("uppercase"));
        errors.Should().NotContain(e => e.Contains("10 characters"));
        errors.Should().NotContain(e => e.Contains("lowercase"));
        errors.Should().NotContain(e => e.Contains("digit"));
        errors.Should().NotContain(e => e.Contains("symbol"));
    }

    // ── Lowercase rule ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_MissingLowercase_ReturnsLowercaseError()
    {
        // 10 chars, uppercase, digit, symbol — no lowercase
        var errors = PasswordValidator.Validate("TEST1234!A");
        errors.Should().ContainSingle(e => e.Contains("lowercase"));
    }

    // ── Digit rule ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_MissingDigit_ReturnsDigitError()
    {
        // 10 chars, upper, lower, symbol — no digit
        var errors = PasswordValidator.Validate("TestAbcd!x");
        errors.Should().ContainSingle(e => e.Contains("digit"));
    }

    // ── Symbol rule ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Test1234?a", '?')]   // '?' is not in the allowed set !@#$%^&*
    [InlineData("Test1234.a", '.')]   // '.' not in allowed set
    [InlineData("Test1234 a", ' ')]   // space not in allowed set
    public void Validate_SymbolNotInAllowedSet_ReturnsSymbolError(string password, char symbol)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().Contain(e => e.Contains("symbol"),
            because: $"'{symbol}' is not in the required symbol set !@#$%^&*");
    }

    [Theory]
    [InlineData("Test1234!a", '!')]
    [InlineData("Test1234@a", '@')]
    [InlineData("Test1234#a", '#')]
    [InlineData("Test1234$a", '$')]
    [InlineData("Test1234%a", '%')]
    [InlineData("Test1234^a", '^')]
    [InlineData("Test1234&a", '&')]
    [InlineData("Test1234*a", '*')]
    public void Validate_EachAllowedSymbol_Passes(string password, char symbol)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().BeEmpty(because: $"'{symbol}' is in the allowed symbol set");
    }

    // ── No symbol at all ──────────────────────────────────────────────────────

    [Fact]
    public void Validate_NoSymbol_ReturnsSymbolError()
    {
        // All alpha+digit, no symbol from the required set
        var errors = PasswordValidator.Validate("TestAbcd12");
        errors.Should().ContainSingle(e => e.Contains("symbol"));
    }

    // ── Unicode ───────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_UnicodePassword_EvaluatesRulesOnCodePoints()
    {
        // Unicode chars are not uppercase/lowercase in the A-Z sense — char.IsUpper/IsLower
        // works on Unicode, but digits/symbols are ASCII-only checks in the validator.
        // A password of purely emoji + unicode satisfies neither digit nor symbol rules.
        // This test documents actual behavior; it is not a requirement that unicode passes.
        var password = "Héllo★World1!"; // Has upper, lower, digit, symbol — should pass
        var errors = PasswordValidator.Validate(password);
        // 'H', 'W' are upper; 'é', 'l', etc. are lower; '1' is digit; '!' is in allowed set.
        errors.Should().BeEmpty(because: "the password contains all required categories");
    }

    // ── Multiple simultaneous failures ────────────────────────────────────────

    [Fact]
    public void Validate_MultipleRuleViolations_ReturnsAllRelevantErrors()
    {
        // 8 chars, has lower and digit, missing upper and symbol
        var errors = PasswordValidator.Validate("abcde123");
        errors.Should().Contain(e => e.Contains("10 characters"), "too short");
        errors.Should().Contain(e => e.Contains("uppercase"), "no uppercase");
        errors.Should().Contain(e => e.Contains("symbol"), "no symbol");
        errors.Should().NotContain(e => e.Contains("lowercase"), "has lowercase");
        errors.Should().NotContain(e => e.Contains("digit"), "has digits");
    }

    // ── Valid password ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ValidPass1!")]           // 10 chars, all rules
    [InlineData("MySecureP@ss99")]        // longer password with @
    [InlineData("Abc123456!x")]           // starts with uppercase
    [InlineData("aB3!aaaaaa")]            // minimal with each category
    public void Validate_ValidPassword_ReturnsEmptyList(string password)
    {
        var errors = PasswordValidator.Validate(password);
        errors.Should().BeEmpty(because: $"'{password}' satisfies all five password rules");
    }
}
