namespace NinetyNine.Services;

/// <summary>
/// Outcome of a service operation that returns a typed value on success
/// and a stable <see cref="ErrorCode"/> plus human-readable message on failure.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ErrorCode"/> is a short, stable, PascalCase domain name that
/// UI code can switch on to pick localized copy or guide routing.
/// Examples: <c>SelfFriendship</c>, <c>AlreadyFriends</c>,
/// <c>FriendRequestRateLimited</c>, <c>CommunityNameTaken</c>.
/// </para>
/// <para>
/// <see cref="ErrorMessage"/> is a reasonable default English message
/// suitable for developer logs and unstyled UI fallbacks.
/// </para>
/// </remarks>
public sealed record ServiceResult<T>(bool Success, T? Value, string? ErrorCode, string? ErrorMessage)
{
    public static ServiceResult<T> Ok(T value) => new(true, value, null, null);

    public static ServiceResult<T> Fail(string errorCode, string errorMessage)
        => new(false, default, errorCode, errorMessage);
}

/// <summary>Value-less variant of <see cref="ServiceResult{T}"/>.</summary>
public sealed record ServiceResult(bool Success, string? ErrorCode, string? ErrorMessage)
{
    public static ServiceResult Ok() => new(true, null, null);

    public static ServiceResult Fail(string errorCode, string errorMessage)
        => new(false, errorCode, errorMessage);
}
