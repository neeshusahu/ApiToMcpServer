using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using ApiToMcpServer.Persistence;
using ApiToMcpServer.Persistence.Model;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

public class ApprovalApiExecutor : IApiExecutor
{

    private readonly IApiExecutor _apiExecutor;
    private readonly IRepository<Approval> _repository;
    private readonly IConfiguration _configuration;

    public ApprovalApiExecutor(IApiExecutor apiExecutor, IRepository<Approval> repository, IConfiguration configuration)
    {

        _apiExecutor = apiExecutor;
        _repository = repository;
        _configuration = configuration;
    }
    public async Task<ApiResponse> ExecuteApi(Operation operation, List<McpPathParam> pathParams, List<McpRequestField> fields, string? cursor, decimal? limit, ApprovalContext approval, User user, CancellationToken cancellationToken = default)
    {

        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        if (operation.Risk == Risk.Read)
        {
            return await _apiExecutor.ExecuteApi(operation, pathParams, fields, cursor, limit, approval, user, cancellationToken);
        }
        if (approval.RequestState is not null)
        {
            if (!Guid.TryParse(approval.RequestState, out var existingId))
                throw new McpException("Invalid approval state.");

            var existing = await _repository.GetByIdAsync(existingId)
                ?? throw new McpException("Approval not found.");

            // Same id + same args must reproduce the stored HMAC, otherwise args were changed
            var expected = await HashAsync(existingId, operation.Name, fields, pathParams);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(existing.Operation)))
                throw new McpException("Request arguments changed after approval was requested.");

            if (existing.ExpiresAt < DateTime.UtcNow)
                return Forbidden("Approval expired. Call the tool again to request a new approval.");

            return existing.Status switch
            {
                ApprovalStatus.Approved => await _apiExecutor.ExecuteApi(operation, pathParams, fields, cursor, limit, approval, user, cancellationToken),
                ApprovalStatus.Pending => throw ApprovalRequired(existingId),   // still waiting: ask again, same state
                _ => Forbidden("Approval was rejected.")
            };
        }


        var approvalId = Guid.NewGuid();
        var hash = await HashAsync(approvalId, operation.Name, fields, pathParams);
        var now = DateTime.UtcNow;

        await _repository.AddAsync(new Approval
        {
            Id = approvalId,
            Operation = hash,
            CreatedBy=user.Id,
            Status = ApprovalStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddMinutes(10)
        });
        // try
        // {
        //     await RequestApprovalAsync(approvalId, mcpServer);
        // }
        // catch (Exception ex)
        // {
        //     // Handle any exceptions that occur during the approval request
        //     throw new McpException ($"Error requesting approval: {ex.Message}");
        // }
        if (!approval.IsMrtrSupported)
            return Forbidden($"Approval required. Open http://localhost:3000/approval/{approvalId} to approve, then retry.");

        throw ApprovalRequired(approvalId);

        // Approval workflow for non-read risks


    }

    private async Task<string> HashAsync(Guid id, string operationId, List<McpRequestField> fields, List<McpPathParam> pathParams)
    {
        var canonical = new
        {
            Id = id,
            OperationId = operationId,
            Fields = fields,
            PathParams = pathParams
        };
        var json = System.Text.Json.JsonSerializer.Serialize(canonical);
        var key = _configuration["Approval:HashKey"]!;
        //HMAC-SHA256 combines your secret key with the JSON bytes to produce a fixed-length, 256-bit authentication code.
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(key));

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hash);
    }

    private async Task RequestApprovalAsync(
        Guid approvalId,
        McpServer mcpServer)
    {
        await mcpServer.ElicitAsync(
            new ElicitRequestParams
            {
                Mode = "url",
                ElicitationId = approvalId.ToString(),
                Url = $"http://localhost:3000/approval/{approvalId}",
                Message = "This operation requires your approval."
            });
    }

    private async Task CheckApprovalAsync(string existingApprovalId)
    {
        while (true)
        {
            var approval = await _repository.GetByIdAsync(Guid.Parse(existingApprovalId));
            if (approval != null && approval.Status != ApprovalStatus.Pending)
            {
                return;
            }
            await Task.Delay(1000); // Wait for 1 second before checking again
        }

    }

    private static InputRequiredException ApprovalRequired(Guid approvalId) =>
        new(new Dictionary<string, InputRequest>
        {
            ["approval"] = InputRequest.ForElicitation(new ElicitRequestParams
            {
                Mode = "url",
                ElicitationId = approvalId.ToString(),
                Url = $"http://localhost:3000/approval/{approvalId}",
                Message = "This operation requires your approval."
            })
        },
            approvalId.ToString());   // requestState: the client sends this back unchanged on retry

    private static ApiResponse Forbidden(string message) => new()
    {
        Status = System.Net.HttpStatusCode.Forbidden,
        Content = message
    };

  
}