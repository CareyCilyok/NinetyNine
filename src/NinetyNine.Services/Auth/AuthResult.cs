namespace NinetyNine.Services.Auth;

/// <summary>
/// Represents the outcome of an authentication operation that returns a typed value on success.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Value">The result value; populated only when <see cref="Success"/> is <see langword="true"/>.</param>
/// <param name="ErrorMessage">Human-readable failure reason; populated only when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record AuthResult<T>(bool Success, T? Value, string? ErrorMessage)
{
    /// <summary>Creates a successful result carrying the given value.</summary>
    public static AuthResult<T> Ok(T value) => new(true, value, null);

    /// <summary>Creates a failed result with the given error message.</summary>
    public static AuthResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// Represents the outcome of an authentication operation that has no return value on success.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="ErrorMessage">Human-readable failure reason; populated only when <see cref="Success"/> is <see langword="false"/>.</param>
public sealed record AuthResult(bool Success, string? ErrorMessage)
{
    /// <summary>Creates a successful result.</summary>
    public static AuthResult Ok() => new(true, null);

    /// <summary>Creates a failed result with the given error message.</summary>
    public static AuthResult Fail(string error) => new(false, error);
}
