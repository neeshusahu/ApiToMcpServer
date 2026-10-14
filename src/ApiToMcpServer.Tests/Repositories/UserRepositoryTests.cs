using ApiToMcpServer.Persistence.Model;
using Dapper;

[TestFixture]
public class UserRepositoryTests
{
    private SqliteTestDatabase _db = null!;
    private UserRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _db = new SqliteTestDatabase();
        await _db.InitializeAsync();
        _repository = new UserRepository(_db.Configuration);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private static User NewUser(string workOsId) => new()
    {
        WorkOSId = workOsId,
        Email = $"{workOsId}@example.com",
        FirstName = "First",
        LastName = "Last"
    };

    private async Task<List<User>> AllUsers()
    {
        using var connection = _db.Open();
        return (await connection.QueryAsync<User>("SELECT Id, FirstName, LastName, Email, WorkOSId FROM Users ORDER BY Id")).ToList();
    }

    [Test]
    public async Task GetOrCreateUserAsync_NewUser_InsertsRow()
    {
        await _repository.GetOrCreateUserAsync(NewUser("user_a"));

        var users = await AllUsers();
        Assert.That(users, Has.Count.EqualTo(1));
        Assert.That(users[0].WorkOSId, Is.EqualTo("user_a"));
        Assert.That(users[0].Id, Is.GreaterThan(0), "Id should be assigned by SQLite, not 0");
    }

    [Test]
    public async Task GetOrCreateUserAsync_TwoDifferentUsers_BothGetTheirOwnId()
    {
        // Regression: inserting an explicit Id = 0 made the second user fail on the primary key
        await _repository.GetOrCreateUserAsync(NewUser("user_a"));
        await _repository.GetOrCreateUserAsync(NewUser("user_b"));

        var users = await AllUsers();
        Assert.That(users.Select(u => u.WorkOSId), Is.EqualTo(new[] { "user_a", "user_b" }));
        Assert.That(users.Select(u => u.Id), Is.Unique.And.All.GreaterThan(0));
    }

    [Test]
    public async Task GetOrCreateUserAsync_ExistingUser_ReturnsStoredRowWithoutInserting()
    {
        await _repository.GetOrCreateUserAsync(NewUser("user_a"));
        var storedId = (await AllUsers()).Single().Id;

        var again = await _repository.GetOrCreateUserAsync(NewUser("user_a"));

        Assert.That(again!.Id, Is.EqualTo(storedId));
        Assert.That(await AllUsers(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetOrCreateUserAsync_NewUser_ReturnsDatabaseAssignedId()
    {
        var created = await _repository.GetOrCreateUserAsync(NewUser("user_a"));

        var storedId = (await AllUsers()).Single().Id;
        Assert.That(created!.Id, Is.EqualTo(storedId), "Returned user should carry the Id SQLite assigned");
    }
}
