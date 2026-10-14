using System.ComponentModel;
using System.Net.Http;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

[McpServerToolType]
public class ApiOperationTool
{
    private readonly IApiExecutor _apiExecutor;
    private readonly RegistryReader _apiregistry;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserService _userService;

    public ApiOperationTool(IApiExecutor apiExecutor, RegistryReader registry, IHttpContextAccessor httpContextAccessor, IUserService userService)
    {
        _apiExecutor = apiExecutor;
        _apiregistry = registry;
        _httpContextAccessor = httpContextAccessor;
        _userService = userService;
    }

    [McpServerTool]
    [Description("""
Executes a registered API operation by its operationId.

Use this tool when you need to call an API operation defined in the API registry.
Provide path parameters and request body fields required by the selected operation.
For paginated operations, provide cursor and limit when needed.

The operationId must exist in the API registry.
Path parameters must match the operation's required path parameters.
Request fields must match the operation's request body definition.

Some operations may require human approval before execution.
""")]
    public async Task<ApiResponse> InvokeRegisteredApiOperation([Description("Unique identifier of the registered API operation to execute.")]
string operationId,

[Description("Values for the path parameters required by the operation.")]
List<McpPathParam> pathParams,

[Description("Values for fields required in the API request body.")]
List<McpRequestField> requestFields,

[Description("Pagination cursor returned by a previous API response.")]
string? cursor,

[Description("Maximum number of records to request when the operation supports pagination.")]
int? limit,
RequestContext<CallToolRequestParams> context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;


        if (string.IsNullOrWhiteSpace(principal?.FindFirst("sub")?.Value)
            )
        {
            throw new McpException("Authenticated user or access token is missing.");
        }
        var user = await _userService.EnsureUserExistsAsync(principal);
        Console.WriteLine("User: {0} {1} {2}", user.Id, user.Email, user.WorkOSId, user.FirstName);
        if (string.IsNullOrEmpty(operationId))
            throw new McpException("OperationId cannot be null or empty"); ;
        if (_apiregistry == null)
            throw new McpException("RegistryReader is not initialized");
        if (!_apiregistry.registry.Operations.ContainsKey(operationId))
            throw new McpException($"OperationId '{operationId}' not found in the registry");
        var approvalContext = new ApprovalContext(
    context.Server.IsMrtrSupported,
    context.Params?.RequestState,       // null on the first call, set on every retry
    context.Params?.InputResponses);
        return await _apiExecutor.ExecuteApi(
            _apiregistry.registry.Operations[operationId],
            pathParams,
            requestFields,
            cursor,
            limit,
            approvalContext,
            user

        );
    }
}