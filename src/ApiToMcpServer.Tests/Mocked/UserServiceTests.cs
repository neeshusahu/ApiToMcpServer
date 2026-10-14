using System.Security.Claims;
using ApiToMcpServer.Persistence.Model;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using Moq;

[TestFixture]
public class UserServiceTests
{
    private Mock<IUserRepository> _repository = null!;
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IUserRepository>();
        _service = new UserService(_repository.Object, new ConfigurationBuilder().Build(), new WorkOSUserInfoClient());
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Bearer"));

    private static ClaimsPrincipal FullPrincipal() => Principal(
        ("sub", "user_123"), ("email", "ada@example.com"), ("first_name", "Ada"), ("last_name", "Lovelace"));

    [Test]
    public async Task EnsureUserExists_PassesClaimsToRepository()
    {
        _repository.Setup(r => r.GetOrCreateUserAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => u);

        await _service.EnsureUserExistsAsync(FullPrincipal());

        _repository.Verify(r => r.GetOrCreateUserAsync(It.Is<User>(u =>
            u.WorkOSId == "user_123" &&
            u.Email == "ada@example.com" &&
            u.FirstName == "Ada" &&
            u.LastName == "Lovelace")), Times.Once);
    }

    [Test]
    public async Task EnsureUserExists_ReturnsStoredUserWithDatabaseId()
    {
        // The repository returns the stored row; its Id is what approvals must use as CreatedBy
        _repository.Setup(r => r.GetOrCreateUserAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => new User
            {
                Id = 42, WorkOSId = u.WorkOSId, Email = u.Email, FirstName = u.FirstName, LastName = u.LastName
            });

        var user = await _service.EnsureUserExistsAsync(FullPrincipal());

        Assert.That(user.Id, Is.EqualTo(42));
    }

    [TestCase("sub")]
    [TestCase("email")]
    [TestCase("first_name")]
    [TestCase("last_name")]
    public void EnsureUserExists_MissingClaim_ThrowsWithoutTouchingRepository(string missing)
    {
        var claims = new[] { ("sub", "user_123"), ("email", "ada@example.com"), ("first_name", "Ada"), ("last_name", "Lovelace") }
            .Where(c => c.Item1 != missing)
            .ToArray();

        Assert.ThrowsAsync<McpException>(() => _service.EnsureUserExistsAsync(Principal(claims)));
        _repository.VerifyNoOtherCalls();
    }
}
