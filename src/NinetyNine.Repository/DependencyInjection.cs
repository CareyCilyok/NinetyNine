using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NinetyNine.Repository.Repositories;
using NinetyNine.Repository.Storage;

namespace NinetyNine.Repository;

/// <summary>
/// Extension methods for registering NinetyNine repository services with the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers MongoDB client, database context, repositories, and avatar store.
    /// Also calls <see cref="BsonConfiguration.Register"/> to configure BSON serialization.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">Application configuration providing the "MongoDb" section.</param>
    public static IServiceCollection AddNinetyNineRepository(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register BSON class maps before any MongoDB interaction
        BsonConfiguration.Register();

        // Bind settings via options pattern
        services.Configure<MongoDbSettings>(
            options => configuration.GetSection("MongoDb").Bind(options));

        // Singleton MongoClient — one per application lifetime per driver recommendations
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        // Scoped context + repositories
        services.AddScoped<INinetyNineDbContext, NinetyNineDbContext>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IAvatarStore, GridFsAvatarStore>();

        // Friends + Communities repositories (Sprint 0 S0.4 + Sprint 2 S2.1)
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IFriendRequestRepository, FriendRequestRepository>();
        services.AddScoped<ICommunityRepository, CommunityRepository>();
        services.AddScoped<ICommunityMemberRepository, CommunityMemberRepository>();
        services.AddScoped<ICommunityInvitationRepository, CommunityInvitationRepository>();
        services.AddScoped<ICommunityJoinRequestRepository, CommunityJoinRequestRepository>();

        // Sprint 4 S4.3
        services.AddScoped<IOwnershipTransferRepository, OwnershipTransferRepository>();

        // Sprint 5 S5.2 + S5.4
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPlayerBlockRepository, PlayerBlockRepository>();

        return services;
    }
}
