namespace NinetyNine.Services;

/// <summary>
/// Seeds the database with test data for development / UX prototyping.
/// Idempotent: skips seeding if data already exists.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// The display names of seeded test players, in display order.
    /// Used by the mock auth endpoints to list "sign in as" options.
    /// </summary>
    /// <remarks>
    /// There are two Careys on the original score cards — the primary user
    /// (Carey Cilyok) and a second player also named Carey. DisplayName must
    /// be unique, so the second Carey is seeded as <c>carey_b</c>.
    /// </remarks>
    public static readonly IReadOnlyList<string> TestPlayerDisplayNames = new[]
    {
        "carey",
        "george",
        "carey_b"
    };

    /// <summary>
    /// The provider name used for mock linked identities on seeded test players.
    /// </summary>
    public const string MockProvider = "Mock";

    Task SeedAsync(CancellationToken ct = default);
}
