using ModelContextProtocol.Protocol;

public sealed record ApprovalContext(
    bool IsMrtrSupported,
    string? RequestState,
    IDictionary<string, InputResponse>? InputResponses);
