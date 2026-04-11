using Microsoft.Extensions.DependencyInjection;
using NinetyNine.Services.Auth;

namespace NinetyNine.Services;

/// <summary>
/// Extension methods for registering NinetyNine domain services with the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all domain service implementations as scoped services.
    /// Assumes <c>AddNinetyNineRepository</c> has already been called.
    /// </summary>
    public static IServiceCollection AddNinetyNineServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IVenueService, VenueService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<AvatarService>();
        services.AddScoped<IDataSeeder, DataSeeder>();
        services.AddScoped<IAuthService, AuthService>();

        // Friends + Communities (Sprint 1+) — plan docs/plans/friends-communities-v1.md
        services.AddScoped<IFriendService, FriendService>();

        return services;
    }
}
