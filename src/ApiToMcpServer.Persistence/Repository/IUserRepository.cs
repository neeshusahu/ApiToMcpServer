using ApiToMcpServer.Persistence;
using ApiToMcpServer.Persistence.Model;

public interface IUserRepository : IRepository<User>
{
     Task<User?> GetOrCreateUserAsync(User user);
     Task<User?> GetByEmailAsync(string email);
     Task<int> AddUserAsync(User user);

    
}