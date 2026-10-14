using ApiToMcpServer.Persistence;
using ApiToMcpServer.Persistence.Model;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ApprovalDb")
            ?? throw new InvalidOperationException("ConnectionStrings:UserDb is not configured.");
    }
    public async Task<int> AddUserAsync(User entity)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var userId = await connection.ExecuteScalarAsync<int>(SqlQueries.InsertUserSql, new
        {
            WorkOSId = entity.WorkOSId,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Email = entity.Email
        });
        return userId;
    }


    public Task DeleteAsync(User entity)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<User>> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(User entity)
    {
        throw new NotImplementedException();
    }

    public Task<User?> GetByIdAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public async Task<User?> GetOrCreateUserAsync(User user)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var currentUser = await connection.QuerySingleOrDefaultAsync<User>(
            SqlQueries.GetUserByWorkOSIdSql, new { WorkOSId = user.WorkOSId });
        if (currentUser != null)
        {
            return currentUser;
        }
        var newUser = new User
        {
           
            WorkOSId = user.WorkOSId,
            Email = user.Email ,
            FirstName = user.FirstName,
            LastName = user.LastName    
        };
        var userId = await AddUserAsync(newUser);
        newUser.Id = userId;
        return newUser;
    }

   

    public Task InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(User entity)
    {
        throw new NotImplementedException();
    }
    
}