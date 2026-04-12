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
        services.AddScoped<ICommunityService, CommunityService>();

        // Sprint 5 S5.2 + S5.3
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationDeliveryService, ConsoleNotificationDeliveryService>();

        // Sprint 9 S9.3
        services.AddScoped<IPollService, PollService>();

        // Sprint 10 S10.2
        services.AddScoped<IMatchService, MatchService>();

        return services;
    }
}
