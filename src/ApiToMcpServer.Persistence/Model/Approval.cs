namespace ApiToMcpServer.Persistence;

public sealed record Approval
{
    public Guid Id { get; init; }
    public string Operation { get; init; } = string.Empty;
    public ApprovalStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }

    public int CreatedBy { get; init; }
    public int? UpdatedBy { get; init; }

}

public enum ApprovalStatus
{
   Pending=1,
    Approved,
    Rejected,
    Expired
}