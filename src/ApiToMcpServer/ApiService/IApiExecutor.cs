using ApiToMcpServer.Persistence.Model;
using ModelContextProtocol.Server;

public interface IApiExecutor
{
    Task<ApiResponse> ExecuteApi(Operation operation, 
        List<McpPathParam> pathParams, List<McpRequestField> fields,
        string? cursor, decimal?limit, ApprovalContext approval, User user,
        CancellationToken cancellationToken = default);
}