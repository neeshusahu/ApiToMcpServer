using System.Net;
using ApiToMcpServer.Persistence.Model;

[TestFixture]
public class ApiExecutorTests
{
    // Records the outgoing request and returns a canned response
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private FakeHandler _handler = null!;
    private ApiExecutor _executor = null!;

    private static readonly User Caller = new() { Id = 1, WorkOSId = "user_1" };
    private static readonly ApprovalContext NoApproval = new(false, null, null);

    private void Respond(HttpStatusCode status, string body = "")
    {
        _handler?.Dispose();
        _handler = new FakeHandler(status, body);
        _executor = new ApiExecutor(new HttpClient(_handler) { BaseAddress = new Uri("http://localhost:5080") });
    }

    [SetUp]
    public void SetUp() => Respond(HttpStatusCode.OK, """{"ok":true}""");

    [TearDown]
    public void TearDown() => _handler.Dispose();

    private static Operation Op(HttpMethod method, string path, List<PathParam>? pathParams = null,
        Pagination? pagination = null, RequestBody? body = null) =>
        new("op", "users", "", method, path, Risk.Read, pathParams ?? [], pagination, body);

    private Task<ApiResponse> Execute(Operation op, List<McpPathParam>? pathParams = null,
        List<McpRequestField>? fields = null, string? cursor = null, decimal? limit = null) =>
        _executor.ExecuteApi(op, pathParams ?? [], fields ?? [], cursor, limit, NoApproval, Caller);

    [Test]
    public async Task ReturnsUpstreamStatusAndBody()
    {
        Respond(HttpStatusCode.NotFound, """{"error":"missing"}""");

        var response = await Execute(Op(HttpMethod.Get, "/users"));

        Assert.That(response.Status, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content, Is.EqualTo("""{"error":"missing"}"""));
    }

    [Test]
    public async Task SendsMethodToBaseAddressPlusPath()
    {
        await Execute(Op(HttpMethod.Delete, "/users"));

        Assert.That(_handler.Request!.Method, Is.EqualTo(HttpMethod.Delete));
        Assert.That(_handler.Request.RequestUri, Is.EqualTo(new Uri("http://localhost:5080/users")));
    }

    [Test]
    public async Task SubstitutesAndEscapesPathParams()
    {
        var op = Op(HttpMethod.Get, "/users/{id}", [new PathParam("id", FieldType.String, null, null)]);

        await Execute(op, [new McpPathParam { Key = "id", Value = "../admin" }]);

        Assert.That(_handler.Request!.RequestUri!.AbsolutePath, Is.EqualTo("/users/..%2Fadmin"));
    }

    [Test]
    public void MissingPathParam_Throws()
    {
        var op = Op(HttpMethod.Get, "/users/{id}", [new PathParam("id", FieldType.String, null, null)]);

        Assert.ThrowsAsync<ArgumentException>(() => Execute(op));
        Assert.That(_handler.Request, Is.Null, "No request should be sent");
    }

    [Test]
    public async Task SendsBodyFieldsAsJson()
    {
        var op = Op(HttpMethod.Post, "/users",
            body: new RequestBody([new BodyField("name", FieldType.String), new BodyField("email", FieldType.String)]));

        await Execute(op, fields: [new() { Key = "name", Value = "Ada" }, new() { Key = "email", Value = "ada@example.com" }]);

        Assert.That(_handler.RequestBody, Is.EqualTo("""{"name":"Ada","email":"ada@example.com"}"""));
    }

    [Test]
    public void MissingBodyField_Throws()
    {
        var op = Op(HttpMethod.Post, "/users", body: new RequestBody([new BodyField("email", FieldType.String)]));

        Assert.ThrowsAsync<ArgumentException>(() => Execute(op, fields: [new() { Key = "name", Value = "Ada" }]));
        Assert.That(_handler.Request, Is.Null);
    }

    [Test]
    public async Task Pagination_AppendsCursorAndLimit()
    {
        var op = Op(HttpMethod.Get, "/users", pagination: new Pagination("cursor", "limit", 20, "data", "nextCursor", "hasNext"));

        await Execute(op, cursor: "2", limit: 5);

        Assert.That(_handler.Request!.RequestUri!.PathAndQuery, Is.EqualTo("/users?cursor=2&limit=5"));
    }

    [Test]
    public async Task Pagination_WithoutCursor_RequestsFirstPage()
    {
        var op = Op(HttpMethod.Get, "/users", pagination: new Pagination("cursor", "limit", 20, "data", "nextCursor", "hasNext"));

        await Execute(op);

        Assert.That(_handler.Request!.RequestUri!.PathAndQuery, Is.EqualTo("/users"));
    }
}
