using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Repository;

namespace NinetyNine.Repository.Tests;

/// <summary>
/// Helper class for creating SQLite in-memory database contexts for testing.
/// Keeps the connection open for the test lifetime to maintain the in-memory database.
/// </summary>
public class SqliteTestHelper : IDisposable
{
    private readonly SqliteConnection _connection;
    public DbContextOptions<LocalContext> Options { get; }

    public SqliteTestHelper()
    {
        // Create and open a SQLite in-memory connection
        // The connection must stay open to keep the database alive
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<LocalContext>()
            .UseSqlite(_connection)
            .Options;
    }

    /// <summary>
    /// Creates a new LocalContext and ensures the database schema is created
    /// </summary>
    public LocalContext CreateContext()
    {
        var context = new LocalContext(Options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates a fresh LocalContext using the same connection (for testing detached scenarios)
    /// </summary>
    public LocalContext CreateFreshContext()
    {
        return new LocalContext(Options);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
