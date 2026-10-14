# ApiToMcpServer.ApprovalApi

An ASP.NET Core minimal API that records a person's **approve / reject decision** for a pending MCP tool call.

**approval-ui** calls it with the user's **WorkOS AuthKit access token**. The API checks the token, identifies the approver and updates the approval in the shared SQLite database. **ApiToMcpServer** reads that same row to decide whether to run the call.

```
ApiToMcpServer ──► INSERT approval (Pending)
                        │
approval-ui ──POST /approval/{id}──► ApprovalApi ──► UPDATE approval (Approved / Rejected)
                        │
ApiToMcpServer ──► reads status on retry ──► runs or refuses the call
```

---

## Configuration

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ApprovalDb": "Data Source=../../ApprovalDb.sqlite"
  },
  "WorkOS": {
    "Issuer": "https://api.workos.com/user_management/<clientId>",
    "ClientId": "<clientId>"
  }
}
```

Set the WorkOS values with user secrets so they stay out of source control (`appsettings.json` only holds placeholders):

```bash
dotnet user-secrets set "WorkOS:ApiKey"   "sk_test_..."
dotnet user-secrets set "WorkOS:ClientId" "client_..."
dotnet user-secrets set "WorkOS:Issuer"   "https://api.workos.com/user_management/client_..."
```

| Setting | Used for |
|---|---|
| `ConnectionStrings:ApprovalDb` | The SQLite database shared with ApiToMcpServer |
| `WorkOS:Issuer` | Expected `iss` of the access token |
| `WorkOS:ClientId` | Fetching the signing keys (JWKS) and the WorkOS SDK client |
| `WorkOS:ApiKey` | WorkOS SDK calls that load the approver's profile |

The client ID must match the one approval-ui uses (`REACT_APP_WORKOS_CLIENT_ID`).

## Run

```bash
dotnet run --launch-profile http     # http://localhost:5282
```

In Development, Swagger UI is available at `http://localhost:5282/swagger`.

---

## Endpoints

| Method | Route | Description |
|---|---|---|
| `POST` | `/approval/{id}?approvalStatus={2\|3}` | Approve (`2`) or reject (`3`) a pending approval. Needs a bearer token. |
| `GET` | `/approval/{id}` | Get one approval |
| `GET` | `/approvals` | List all approvals |

---

## How a request is validated

### 1. Signing keys are loaded at startup

Before it starts serving requests, the API downloads WorkOS's public signing keys for the client:

```csharp
var jwksUrl = $"https://api.workos.com/sso/jwks/{clientId}";
var jwks = new JsonWebKeySet(await httpClient.GetStringAsync(jwksUrl));
```

### 2. The JWT bearer token is validated

The approval-ui request carries `Authorization: Bearer <access token>`. The JwtBearer middleware checks:

| Check | Setting |
|---|---|
| **Signature**: signed by one of WorkOS's keys, matched by the token's `kid` | `ValidateIssuerSigningKey = true`, `IssuerSigningKeyResolver` |
| **Issuer**: `iss` equals `WorkOS:Issuer` | `ValidateIssuer = true` |
| **Lifetime**: the token has not expired | `ValidateLifetime = true` |
| **Audience** | Not checked (`ValidateAudience = false`). AuthKit session tokens don't carry an audience for this API. |

`MapInboundClaims = false` keeps the claim names exactly as WorkOS issued them, so the user ID can be read as `sub` instead of `ClaimTypes.NameIdentifier`.

A token that fails any of these checks never produces an authenticated user.

### 3. CORS

Only `http://localhost:3000` (approval-ui) may call the API from a browser.

---

## How the decision is saved

`POST /approval/{id}?approvalStatus=2`

1. **Identify the approver.** Read the `sub` claim, which is the WorkOS user ID. If it's missing, return `401 Unauthorized`.

2. **Load the profile from WorkOS.** The access token doesn't include email or name, so the API fetches them:
   ```csharp
   var profile = await workOSClient.UserManagement.GetAsync(workOsUserId);
   ```

3. **Get or create the local user.** `UserRepository.GetOrCreateUserAsync` looks the user up by `WorkOSId`. If there's no row, it inserts one into `Users`. ApiToMcpServer uses the same table, so a requester and an approver are stored the same way.

4. **Update the approval.** `ApprovalRepository.UpdateAsync`
 .

5. **Respond.** The API returns `200 OK`. If anything throws, it returns `400 Bad Request` with the error message.

### Where approvals come from

This API **does not create approvals**. ApiToMcpServer creates them (`ApprovalApiExecutor`) when an MCP client calls an operation whose risk is `write` or `destructive`:

| Column | Value |
|---|---|
| `Id` | New GUID. This is the `{id}` in `/approval/{id}`. |
| `Operation` | HMAC-SHA256 of the approval id, operationId, body fields and path params |
| `StatusId` | `1` (Pending) |
| `CreatedBy` | The requesting user's `Users.Id` |
| `CreatedAt` / `UpdatedAt` | Current UTC time |
| `ExpiresAt` | Creation time + 10 minutes |

When the MCP client retries, ApiToMcpServer:
- recomputes the HMAC, so the arguments can't be changed after approval
- checks `ExpiresAt`
- runs the call only if the status is **Approved**

---

## Database

SQLite, accessed with Dapper. ApiToMcpServer creates the tables at startup (`IRepository<Approval>.InitializeAsync`).

| Table | Columns |
|---|---|
| `Statuses` | `Id`, `Name` with the values 1 Pending, 2 Approved, 3 Rejected, 4 Expired |
| `Users` | `Id`, `FirstName`, `LastName`, `Email` (unique), `WorkOSId` (unique) |
| `Approvals` | `Id` (GUID text), `Operation`, `StatusId` → `Statuses`, `CreatedAt`, `UpdatedAt`, `ExpiresAt`, `CreatedBy` → `Users`, `UpdatedBy` → `Users` |
