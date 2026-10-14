# MCP Approval Workflow --- Challenges and Solutions

## 1. URL Elicitation in a Stateless MCP Server

**Challenge:** Calling `mcpServer.ElicitAsync()` with `Mode = "url"`
threw the exception `Elicitation is not supported in stateless mode`.
The elicitation flow required server-side state, which conflicted with
the decision to keep the MCP HTTP server stateless.

**Cause:** URL elicitation is a request the server sends to the client
in the middle of a tool call. That needs a stateful HTTP session, but
the server runs with `WithHttpTransport(o => o.Stateless = true)`.

**Solution:** Kept the server stateless and switched to **MRTR
(Multi Round-Trip Requests)** in the MCP C# SDK. Rather than holding a
session open, the tool creates a pending approval and **ends the call**
by throwing an `InputRequiredException`. The exception carries two
things:

- a URL elicitation pointing to the approval page in the React UI
- a `requestState` set to the approval ID

The client opens the URL, then **calls the tool again** with the same
`requestState` (and any `InputResponses`). Each request stands on its
own, so no server session is needed.

**Clients without MRTR support** (`IsMrtrSupported == false`) get a
`403` response with the approval link. The user approves the request
and then asks the agent to retry.

## 2. Binding an Approval to the Original Request with HMAC

**Challenge:** A tool retry could contain a different operation or
modified body fields/path parameters than the request the user
originally approved. Approval status alone would not detect these
changes.

**Solution:** Created a canonical representation of the approval ID,
operation ID, request fields, and path parameters, then calculated an
HMAC-SHA256 using a server-side secret key. Stored the HMAC with the
approval record and recalculated it on retry. Compared the expected and
stored HMAC using a constant-time comparison; reject the retry if they
do not match. The HMAC key is stored in configuration/secrets, not
committed to source control.

**Additional validation:** HMAC verifies request integrity, not user
consent. Before execution, the server also checks that the approval is
**Approved** and **not expired** (`ExpiresAt`, 10 minutes after
creation).

**Not yet enforced:** the server doesn't check *who* approved. There's
no rule that the approver differs from the requester, and no approver
role. `CreatedBy` and `UpdatedBy` are both recorded, so these checks
can be added.

## 3. Detecting Retries and Preserving Approval State

**Challenge:** In a stateless MCP server, the initial tool call and a
later retry are separate requests. The server cannot rely on in-memory
session state to know which approval the retry refers to.

**Solution:** The tool reads the MCP call parameters through
`RequestContext<CallToolRequestParams>`. From them it builds an
`ApprovalContext`:

- `IsMrtrSupported`: whether the client can do MRTR
- `RequestState`: `null` on a first call, and the approval ID on a retry
- `InputResponses`: what the client collected for the elicitation

**A call is a retry when `RequestState` holds the approval ID.** On a
retry, the server:

1. validates the state as a GUID
2. loads the approval record from SQLite
3. recomputes the HMAC and compares it with the stored value
4. checks expiry and status

Approved requests proceed. Pending requests are asked again with the
same state. Rejected or expired approvals are denied, and expired ones
need a new tool call.

**Implementation details:**

- `RequestState` only identifies a retry if the MCP client keeps that
  state and sends it back.
- Approval IDs are new GUIDs, which makes them unique, and they're
  included in the HMAC.
- The HMAC is stored in the `Approvals.Operation` column, which holds
  the **hash, not the operation name**. The retry check compares against
  `existing.Operation` for that reason. Renaming the column to `Hash`
  would make that clearer.


## 4. OAuth Protected Resource Metadata for MCP

### Challenge

An OAuth-aware MCP client needs to discover which authorization server protects the MCP endpoint and which resource it should request a token for. Without protected-resource metadata, clients may not know where to start the OAuth authorization flow.

### Code

```csharp
app.MapGet("/.well-known/oauth-protected-resource", () =>
    Results.Json(new
    {
        resource,
        authorization_servers = new[] { issuer },
        bearer_methods_supported = new[] { "header" }
    }));
```

- `resource`: the identifier of the protected MCP resource, such as `http://localhost:5001/mcp`.
- `authorization_servers`: the authorization server(s) protecting the resource, such as the WorkOS AuthKit issuer.
- `bearer_methods_supported`: indicates that the server accepts bearer tokens in the HTTP `Authorization` header.

### Discovery and authentication flow

```text
Claude Code
    |
    | 1. Calls MCP endpoint: http://localhost:5001/mcp
    v
MCP server
    |
    | 2. If unauthenticated, returns 401 with a
    |    WWW-Authenticate challenge referencing metadata
    v
Protected-resource metadata endpoint
/.well-known/oauth-protected-resource
    |
    | 3. Publishes the resource identifier and
    |    authorization server
    v
WorkOS AuthKit
    |
    | 4. Client discovers OAuth endpoints and obtains
    |    an access token for the MCP resource
    v
Claude Code retries MCP request with:
Authorization: Bearer <access-token>
    |
    v
ASP.NET Core JWT bearer middleware
    |
    | 5. Validates token signature, issuer, audience,
    |    and expiration
    v
MCP endpoint is authorized and processes the request
```

### Important distinction

The metadata endpoint is public discovery information. It does **not** authenticate users, issue tokens, or validate JWTs. Authentication enforcement is performed by the ASP.NET Core authentication and authorization middleware, for example:

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();
```

Ensure the `resource` value matches the resource identifier requested by the OAuth client and the audience expected by JWT validation. A mismatch can cause a validly signed token to be rejected.
