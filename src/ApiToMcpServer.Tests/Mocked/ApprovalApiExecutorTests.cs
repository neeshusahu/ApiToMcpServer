using System.Net;
using ApiToMcpServer.Persistence;
using ApiToMcpServer.Persistence.Model;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

[TestFixture]
public class ApprovalApiExecutorTests
{
    private Mock<IApiExecutor> _inner = null!;
    private Mock<IRepository<Approval>> _repository = null!;
    private ApprovalApiExecutor _executor = null!;

    private static readonly User Requester = new() { Id = 7, WorkOSId = "user_requester" };
    private static readonly ApiResponse InnerResponse = new() { Status = HttpStatusCode.OK, Content = "done" };

    private static readonly List<McpPathParam> PathParams = [new() { Key = "id", Value = "u_1" }];
    private static readonly List<McpRequestField> Fields = [new() { Key = "name", Value = "Ada" }];

    [SetUp]
    public void SetUp()
    {
        _inner = new Mock<IApiExecutor>();
        _inner.Setup(e => e.ExecuteApi(It.IsAny<Operation>(), It.IsAny<List<McpPathParam>>(), It.IsAny<List<McpRequestField>>(),
                It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<ApprovalContext>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InnerResponse);

        _repository = new Mock<IRepository<Approval>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Approval:HashKey"] = "test-hash-key" })
            .Build();

        _executor = new ApprovalApiExecutor(_inner.Object, _repository.Object, configuration);
    }

    private static Operation Op(Risk risk) =>
        new("updateUser", "users", "Update a user", HttpMethod.Put, "/users/{id}", risk, [], null, null);

    private static ApprovalContext FirstCall(bool mrtr = false) => new(mrtr, null, null);
    private static ApprovalContext Retry(Guid id) => new(true, id.ToString(), null);

    private Task<ApiResponse> Execute(Operation op, ApprovalContext ctx,
        List<McpPathParam>? pathParams = null, List<McpRequestField>? fields = null) =>
        _executor.ExecuteApi(op, pathParams ?? PathParams, fields ?? Fields, null, null, ctx, Requester);

    // Runs a first call and returns the approval it stored, so retries can be tested with a real HMAC
    private async Task<Approval> CreatePendingApproval(Operation op)
    {
        Approval? stored = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Approval>()))
            .Callback<Approval>(a => stored = a)
            .Returns(Task.CompletedTask);

        await Execute(op, FirstCall());
        Assert.That(stored, Is.Not.Null, "First call should have stored an approval");
        return stored!;
    }

    private void VerifyInnerNotCalled() =>
        _inner.Verify(e => e.ExecuteApi(It.IsAny<Operation>(), It.IsAny<List<McpPathParam>>(), It.IsAny<List<McpRequestField>>(),
            It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<ApprovalContext>(), It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);

    // ---------- read operations ----------

    [Test]
    public async Task ReadOperation_RunsImmediately_WithoutTouchingRepository()
    {
        var result = await Execute(Op(Risk.Read), FirstCall());

        Assert.That(result, Is.SameAs(InnerResponse));
        _repository.VerifyNoOtherCalls();
    }

    // ---------- first call on a risky operation ----------

    [TestCase(Risk.Write)]
    [TestCase(Risk.Destructive)]
    public async Task RiskyOperation_FirstCall_StoresPendingApproval(Risk risk)
    {
        var before = DateTime.UtcNow;

        var approval = await CreatePendingApproval(Op(risk));

        Assert.Multiple(() =>
        {
            Assert.That(approval.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(approval.Status, Is.EqualTo(ApprovalStatus.Pending));
            Assert.That(approval.CreatedBy, Is.EqualTo(Requester.Id));
            Assert.That(approval.Operation, Is.Not.Empty, "HMAC of the call arguments");
            Assert.That(approval.CreatedAt, Is.InRange(before, DateTime.UtcNow));
            Assert.That(approval.ExpiresAt, Is.EqualTo(approval.CreatedAt.AddMinutes(10)));
        });
        VerifyInnerNotCalled();
    }

    [Test]
    public async Task RiskyOperation_FirstCall_WithoutMrtr_ReturnsForbiddenWithApprovalLink()
    {
        Approval? stored = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Approval>())).Callback<Approval>(a => stored = a).Returns(Task.CompletedTask);

        var result = await Execute(Op(Risk.Write), FirstCall(mrtr: false));

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(result.Content, Does.Contain($"http://localhost:3000/approval/{stored!.Id}"));
    }

    [Test]
    public void RiskyOperation_FirstCall_WithMrtr_ThrowsInputRequired()
    {
        Assert.ThrowsAsync<InputRequiredException>(() => Execute(Op(Risk.Write), FirstCall(mrtr: true)));
        _repository.Verify(r => r.AddAsync(It.IsAny<Approval>()), Times.Once);
    }

    // ---------- retry with request state ----------

    [Test]
    public async Task Retry_Approved_RunsTheOperation()
    {
        var op = Op(Risk.Write);
        var approval = await CreatePendingApproval(op);
        _repository.Setup(r => r.GetByIdAsync(approval.Id))
            .ReturnsAsync(approval with { Status = ApprovalStatus.Approved });

        var result = await Execute(op, Retry(approval.Id));

        Assert.That(result, Is.SameAs(InnerResponse));
    }

    [Test]
    public async Task Retry_Rejected_ReturnsForbidden()
    {
        var op = Op(Risk.Write);
        var approval = await CreatePendingApproval(op);
        _repository.Setup(r => r.GetByIdAsync(approval.Id))
            .ReturnsAsync(approval with { Status = ApprovalStatus.Rejected });

        var result = await Execute(op, Retry(approval.Id));

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(result.Content, Does.Contain("rejected"));
        VerifyInnerNotCalled();
    }

    [Test]
    public async Task Retry_StillPending_AsksForApprovalAgain()
    {
        var op = Op(Risk.Write);
        var approval = await CreatePendingApproval(op);
        _repository.Setup(r => r.GetByIdAsync(approval.Id)).ReturnsAsync(approval);

        Assert.ThrowsAsync<InputRequiredException>(() => Execute(op, Retry(approval.Id)));
        VerifyInnerNotCalled();
    }

    [Test]
    public async Task Retry_Expired_ReturnsForbidden_EvenIfApproved()
    {
        var op = Op(Risk.Write);
        var approval = await CreatePendingApproval(op);
        _repository.Setup(r => r.GetByIdAsync(approval.Id)).ReturnsAsync(approval with
        {
            Status = ApprovalStatus.Approved,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });

        var result = await Execute(op, Retry(approval.Id));

        Assert.That(result.Status, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(result.Content, Does.Contain("expired"));
        VerifyInnerNotCalled();
    }

    [Test]
    public async Task Retry_WithChangedArguments_Throws()
    {
        var op = Op(Risk.Write);
        var approval = await CreatePendingApproval(op);
        _repository.Setup(r => r.GetByIdAsync(approval.Id))
            .ReturnsAsync(approval with { Status = ApprovalStatus.Approved });

        var tampered = new List<McpRequestField> { new() { Key = "name", Value = "Mallory" } };

        var ex = Assert.ThrowsAsync<McpException>(() => Execute(op, Retry(approval.Id), fields: tampered));
        Assert.That(ex!.Message, Does.Contain("arguments changed"));
        VerifyInnerNotCalled();
    }

    [Test]
    public void Retry_UnknownApproval_Throws()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Approval?)null);

        var ex = Assert.ThrowsAsync<McpException>(() => Execute(Op(Risk.Write), Retry(id)));
        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void Retry_InvalidState_Throws()
    {
        var ctx = new ApprovalContext(true, "not-a-guid", null);

        var ex = Assert.ThrowsAsync<McpException>(() => Execute(Op(Risk.Write), ctx));
        Assert.That(ex!.Message, Does.Contain("Invalid approval state"));
        _repository.VerifyNoOtherCalls();
    }
}
