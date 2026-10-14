using System.Net;

public sealed record ApiResponse
{

    public HttpStatusCode Status { get; set; }
    public string? Content { get; set; }

}