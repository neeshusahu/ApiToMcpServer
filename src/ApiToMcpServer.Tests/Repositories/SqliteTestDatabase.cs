using ApiToMcpServer.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

// A throwaway SQLite file per test, with the schema created by ApprovalRepository.InitializeAsync
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"approvals-{Guid.NewGuid():N}.sqlite");

    public IConfiguration Configuration { get; }

    public SqliteTestDatabase()
    {
        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApprovalDb"] = $"Data Source={_path}"
            })
            .Build();
    }

    public async Task InitializeAsync() => await new ApprovalRepository(Configuration).InitializeAsync();

    // Inserts users with fixed ids so approvals can reference them
    public async Task SeedUsersAsync(params int[] ids)
    {
        using var connection = Open();
        foreach (var id in ids)
            await connection.ExecuteAsync(
                "INSERT INTO Users (Id, FirstName, LastName, Email, WorkOSId) VALUES (@Id, 'Test', 'User', @Email, @WorkOSId)",
                new { Id = id, Email = $"user{id}@example.com", WorkOSId = $"user_{id}" });
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(Configuration.GetConnectionString("ApprovalDb"));
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();   // release the file handle before deleting
        File.Delete(_path);
    }
}
