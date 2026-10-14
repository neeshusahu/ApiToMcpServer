using System.Security.Claims;
using ApiToMcpServer.Persistence.Model;

public interface IUserService
{
    Task<User> EnsureUserExistsAsync(
       
       ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default);
}