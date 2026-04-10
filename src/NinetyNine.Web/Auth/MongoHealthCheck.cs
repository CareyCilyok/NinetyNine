using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using NinetyNine.Repository;

namespace NinetyNine.Web.Auth;

/// <summary>
/// Health check that pings MongoDB. Returns Healthy on success, Unhealthy on failure.
/// </summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;
    private readonly ILogger<MongoHealthCheck> _logger;

    public MongoHealthCheck(IMongoClient client, ILogger<MongoHealthCheck> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetDatabase("admin")
                .RunCommandAsync<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB health check failed.");
            return HealthCheckResult.Unhealthy("MongoDB is unreachable.", ex);
        }
    }
}
