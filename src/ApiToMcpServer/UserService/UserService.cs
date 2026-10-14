using System.Security.Claims;
using ApiToMcpServer.Persistence.Model;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    private readonly WorkOSUserInfoClient _workOSUserInfoClient;

    public UserService(
        IUserRepository userRepository,
        IConfiguration configuration,
        WorkOSUserInfoClient workOSUserInfoClient)
    {
        _userRepository = userRepository;
        _workOSUserInfoClient = workOSUserInfoClient;
    }

    public async Task<User> EnsureUserExistsAsync(

       ClaimsPrincipal claimsPrincipal,

        CancellationToken cancellationToken = default)
    {


        var user = await _workOSUserInfoClient.GetUserInfoAsync(claimsPrincipal);

        // Return the stored row: its Id is what approvals reference as CreatedBy
        return await _userRepository.GetOrCreateUserAsync(user) ?? user;
    }
}