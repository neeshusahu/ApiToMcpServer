using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ApiToMcpServer.Persistence.Model;
using ModelContextProtocol;

public sealed class WorkOSUserInfoClient
{
    public async Task<User> GetUserInfoAsync(
       ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        
    {
        var workOSId= principal?.FindFirst("sub")?.Value;
        var email= principal?.FindFirst("email")?.Value;
        var firstName=principal?.FindFirst("first_name")?.Value;
        var lastName=principal?.FindFirst("last_name")?.Value;
        if (string.IsNullOrWhiteSpace(workOSId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new McpException("One or more required user information fields are null or empty");
        }
        return new User
        {
            WorkOSId = workOSId,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };
    }
}