
using System.Net;
using System.Net.Http.Json;
using ApiToMcpServer.Persistence.Model;
using ModelContextProtocol.Server;

public class ApiExecutor : IApiExecutor
{
    private readonly HttpClient _httpClient;

    public ApiExecutor(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    public async Task<ApiResponse> ExecuteApi(Operation operation, List<McpPathParam> pathParams, List<McpRequestField> fields, string? cursor, decimal? limit, ApprovalContext approval, User user, CancellationToken cancellationToken = default)
    {
        ApiResponse response = new ApiResponse();
    
       if(operation == null)
       {
           throw new ArgumentNullException(nameof(operation));
       }
       var opsPath = operation.Path;
        if (string.IsNullOrEmpty(opsPath))
        {
            throw new ArgumentException("Operation path cannot be null or empty", nameof(operation));
        }

        if (operation.PathParams.Count > 0)
        {
            opsPath = SubstitutePathParams(opsPath, pathParams, operation.PathParams);
        }

        //Check for pagination parameters
        if (operation.Pagination != null)
        {
            opsPath = SubstitutePaginationParams(opsPath, operation.Pagination, cursor, limit);
        }
        // Http request message
        var request = new HttpRequestMessage(
    new HttpMethod(operation.Method.Method),
    opsPath);
    // Set request content if body is present
        if(operation.Body!=null)
        {
            if(!ValidateRequestBody(operation.Body, fields))
            {
                throw new ArgumentException("Request body validation failed.", nameof(fields));
            }
            request.Content = JsonContent.Create(
        fields.ToDictionary(f => f.Key, f => f.Value));
        }
        //Actual Http api call; the body (including nextCursor for paginated lists) is passed back to the model as is
        using var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
        response.Status = httpResponse.StatusCode;
        response.Content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        return response;
    }

    private string SubstitutePathParams(string opsPath, List<McpPathParam> pathParams, List<PathParam> operationPathParams)
    {
        foreach (var path in operationPathParams)
        {
            var currentPath = pathParams.FirstOrDefault(i => i.Key == path.Name);
            if (currentPath?.Value == null)
            {
                throw new ArgumentException($"Path parameter '{path.Name}' is missing a value.", nameof(pathParams));
            }
            // Escape so a value like "../admin" can't change which endpoint is called
            opsPath = opsPath.Replace(string.Concat("{", currentPath.Key, "}"), Uri.EscapeDataString(currentPath.Value));
        }
        return opsPath;
    }
    private string SubstitutePaginationParams(string opsPath, Pagination operationPagination, string? cursor, decimal? limit)
    {
        if (operationPagination != null)
{
    if (!string.IsNullOrEmpty(cursor))
    {
        limit = limit.HasValue && limit.Value > 0
            ? limit.Value
            : operationPagination.DefaultLimit ?? 5;

        string paginationQuery = string.Concat(
            "?",
            operationPagination.CursorParam,
            "=",
            Uri.EscapeDataString(cursor),
            "&",
            operationPagination.LimitParam,
            "=",
            limit
        );

        opsPath = string.Concat(opsPath, paginationQuery);
    }
}
        return opsPath;
    }
    private bool ValidateRequestBody(RequestBody requestBody,List<McpRequestField> fields)
    {
        foreach(var field in requestBody.Fields)
    {
        var currentField= fields.FirstOrDefault(i=>i.Key==field.Name);
        if(currentField?.Value ==null)
        {
            return false;
        }
    }
        return true;
    }
}