namespace NinetyNine.Services;

/// <summary>
/// Seeds the database. Two modes:
/// <list type="bullet">
///   <item><see cref="SeedMode.Production"/> — seeds only the public real-
///     world venues (the "live" data shared across deployments). Skips
///     all mock players, mock games, mock matches, mock communities, and
///     the original 3 dev test players. Idempotent.</item>
///   <item><see cref="SeedMode.Development"/> — full mock seed: every
///     production venue + the private dev home table + the original 3
///     test players (carey/george/carey_b) + their hand-crafted demo
///     games + the seeded "Pocket Sports" community + the 33 mock
///     player roster + their generated game histories + the 20 mock
///     concurrent matches + the 5 themed mock communities. Idempotent.</item>
/// </list>
/// <para>
/// The mode is selected by the caller from <c>Seed:Mode</c> config.
/// <see cref="DataSeeder"/> reads no environment state on its own —
/// the caller (Program.cs) decides which mode to run.
/// </para>
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// The display names of the original three dev test players, in
    /// display order. Used by the mock auth endpoints to list
    /// "sign in as" options. NOT seeded in <see cref="SeedMode.Production"/>.
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

    Task SeedAsync(SeedMode mode, CancellationToken ct = default);
}

/// <summary>
/// Selects which seed dataset the seeder writes. See <see cref="IDataSeeder"/>
/// for the per-mode rules.
/// </summary>
public enum SeedMode
{
    /// <summary>Production deployments — real venues only; no mock data.</summary>
    Production = 0,

    /// <summary>Dev / integration testing — full mock dataset.</summary>
    Development = 1,
}
