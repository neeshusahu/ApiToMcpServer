using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ApiToMcpServer.Persistence;

public class ApprovalRepository : IRepository<Approval>
{
    private readonly string _connectionString;

    static ApprovalRepository()
    {
        // SQLite stores Guid as TEXT; Dapper needs help mapping it back
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
    }

    public ApprovalRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ApprovalDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ApprovalDb is not configured.");
    }

    // New connection per call; SQLite connections are cheap and pooled
    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task InitializeAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync(SqlQueries.CreateStatusesTableSql);
        await connection.ExecuteAsync(SqlQueries.InsertStatusesSql);
        await connection.ExecuteAsync(SqlQueries.CreateUsersTableSql);
        await connection.ExecuteAsync(SqlQueries.CreateApprovalsTableSql);
    }

    public async Task AddAsync(Approval approval)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync(
            SqlQueries.InsertApprovalSql,
            new
            {
                Id = approval.Id.ToString(),
                approval.Operation,
                StatusId = (int)approval.Status,
                approval.CreatedAt,
                approval.UpdatedAt,
                approval.ExpiresAt,
                approval.CreatedBy,
                approval.UpdatedBy
            });
            
    }

    // Only transitions approvals that are still Pending
    public async Task UpdateAsync(Approval approval)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync(
            SqlQueries.UpdateStatusSql,
            new
            {
                StatusId = (int)approval.Status,
                approval.UpdatedAt,
                Id = approval.Id.ToString(),
                PendingStatusId = (int)ApprovalStatus.Pending,
                UpdatedBy = approval.UpdatedBy
            });
    }

    public async Task DeleteAsync(Approval approval)
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync(SqlQueries.DeleteApprovalSql, new { Id = approval.Id.ToString() });
    }

    public async Task<Approval?> GetByIdAsync(Guid id)
    {
        await using var connection = await OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Approval>(
            SqlQueries.GetApprovalByIdSql, new { Id = id.ToString() });
    }

    public async Task<IEnumerable<Approval>> GetAllAsync()
    {
        await using var connection = await OpenConnectionAsync();
        return await connection.QueryAsync<Approval>(SqlQueries.GetAllApprovalsSql);
    }


    private sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(System.Data.IDbDataParameter parameter, Guid value)
            => parameter.Value = value.ToString();

        public override Guid Parse(object value)
            => Guid.Parse((string)value);
    }
}
