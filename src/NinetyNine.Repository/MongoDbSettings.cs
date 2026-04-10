namespace NinetyNine.Repository;

/// <summary>
/// Configuration options for the MongoDB connection.
/// Bound from the "MongoDb" configuration section.
/// </summary>
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "NinetyNine";
}
