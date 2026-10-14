using ApiToMcpServer.Persistence;
using Dapper;

[TestFixture]
public class ApprovalRepositoryTests
{
    private SqliteTestDatabase _db = null!;
    private ApprovalRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _db = new SqliteTestDatabase();
        await _db.InitializeAsync();
        await _db.SeedUsersAsync(1, 2, 3);   // CreatedBy / UpdatedBy are foreign keys to Users
        _repository = new ApprovalRepository(_db.Configuration);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private static Approval NewPending(DateTime? expiresAt = null)
    {
        var now = DateTime.UtcNow;
        return new Approval
        {
            Id = Guid.NewGuid(),
            Operation = "hmac",
            Status = ApprovalStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = expiresAt ?? now.AddMinutes(10),
            CreatedBy = 1
        };
    }

    private static Approval Decision(Approval approval, ApprovalStatus status, int updatedBy = 2) => approval with
    {
        Status = status,
        UpdatedBy = updatedBy,
        UpdatedAt = DateTime.UtcNow
    };

    [Test]
    public async Task InitializeAsync_IsIdempotent_AndSeedsStatuses()
    {
        await _repository.InitializeAsync();   // second run

        using var connection = _db.Open();
        var statuses = await connection.QueryAsync<string>("SELECT Name FROM Statuses ORDER BY Id");

        Assert.That(statuses, Is.EqualTo(new[] { "Pending", "Approved", "Rejected", "Expired" }));
    }

    [Test]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrips()
    {
        var approval = NewPending();

        await _repository.AddAsync(approval);
        var loaded = await _repository.GetByIdAsync(approval.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Id, Is.EqualTo(approval.Id));
            Assert.That(loaded.Operation, Is.EqualTo("hmac"));
            Assert.That(loaded.Status, Is.EqualTo(ApprovalStatus.Pending));
            Assert.That(loaded.CreatedBy, Is.EqualTo(1));
            Assert.That(loaded.UpdatedBy, Is.Null);
            Assert.That(loaded.ExpiresAt, Is.EqualTo(approval.ExpiresAt).Within(TimeSpan.FromMilliseconds(1)));
        });
    }

    [Test]
    public async Task GetByIdAsync_Unknown_ReturnsNull()
    {
        Assert.That(await _repository.GetByIdAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task GetAllAsync_ReturnsEveryApproval()
    {
        await _repository.AddAsync(NewPending());
        await _repository.AddAsync(NewPending());

        Assert.That(await _repository.GetAllAsync(), Has.Exactly(2).Items);
    }

    [TestCase(ApprovalStatus.Approved)]
    [TestCase(ApprovalStatus.Rejected)]
    public async Task UpdateAsync_Pending_SavesStatusAndApprover(ApprovalStatus status)
    {
        var approval = NewPending();
        await _repository.AddAsync(approval);

        await _repository.UpdateAsync(Decision(approval, status, updatedBy: 2));
        var loaded = await _repository.GetByIdAsync(approval.Id);

        Assert.That(loaded!.Status, Is.EqualTo(status));
        Assert.That(loaded.UpdatedBy, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_AlreadyDecided_IsIgnored()
    {
        var approval = NewPending();
        await _repository.AddAsync(approval);
        await _repository.UpdateAsync(Decision(approval, ApprovalStatus.Rejected, updatedBy: 2));

        await _repository.UpdateAsync(Decision(approval, ApprovalStatus.Approved, updatedBy: 3));
        var loaded = await _repository.GetByIdAsync(approval.Id);

        Assert.That(loaded!.Status, Is.EqualTo(ApprovalStatus.Rejected));
        Assert.That(loaded.UpdatedBy, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_AfterExpiry_IsIgnored()
    {
        var approval = NewPending(expiresAt: DateTime.UtcNow.AddMinutes(-1));
        await _repository.AddAsync(approval);

        await _repository.UpdateAsync(Decision(approval, ApprovalStatus.Approved));
        var loaded = await _repository.GetByIdAsync(approval.Id);

        Assert.That(loaded!.Status, Is.EqualTo(ApprovalStatus.Pending));
        Assert.That(loaded.UpdatedBy, Is.Null);
    }

    [Test]
    public void AddAsync_UnknownCreatedBy_IsRejectedByForeignKey()
    {
        var approval = NewPending() with { CreatedBy = 999 };

        var ex = Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => _repository.AddAsync(approval));
        Assert.That(ex!.Message, Does.Contain("FOREIGN KEY"));
    }

    [Test]
    public async Task UpdateAsync_UnknownStatus_IsRejectedByForeignKey()
    {
        var approval = NewPending();
        await _repository.AddAsync(approval);

        var ex = Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => _repository.UpdateAsync(Decision(approval, (ApprovalStatus)99)));
        Assert.That(ex!.Message, Does.Contain("FOREIGN KEY"));
    }

    [Test]
    public async Task DeleteAsync_RemovesApproval()
    {
        var approval = NewPending();
        await _repository.AddAsync(approval);

        await _repository.DeleteAsync(approval);

        Assert.That(await _repository.GetByIdAsync(approval.Id), Is.Null);
    }
}
