# ApiToMcpServer.Persistence

The shared data-access library for **ApiToMcpServer** and **ApiToMcpServer.ApprovalApi**. It stores **users** and **approvals** in a single SQLite database using [Dapper](https://github.com/DapperLib/Dapper) and plain SQL.

Both apps point at the same database file:
- **ApiToMcpServer** creates approvals.
- **ApprovalApi** records the approver's decision on them.

---

## Setup

### 1. Register the repositories

```csharp
using ApiToMcpServer.Persistence;

builder.Services.AddPersistence();
```

| Service | Implementation | Lifetime |
|---|---|---|
| `IRepository<Approval>` | `ApprovalRepository` | Scoped |
| `IUserRepository` | `UserRepository` | Scoped |

### 2. Configure the connection string

Both repositories read `ConnectionStrings:ApprovalDb`:

```json
{
  "ConnectionStrings": {
    "ApprovalDb": "Data Source=../../ApprovalDb.sqlite"
  }
}
```

Use the **same absolute path** in both apps so they share one database.

### 3. Create the tables

Call `InitializeAsync` once when the app starts. ApiToMcpServer does this in `Program.cs`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IRepository<Approval>>();
    await repository.InitializeAsync();
}
```

It creates `Statuses`, `Users` and `Approvals` if they don't exist, and seeds the status values. It's safe to run on every startup.

---

## Schema

```
Statuses (1) ◄─── StatusId ─── Approvals ─── CreatedBy / UpdatedBy ───► Users
```

### `Statuses`

| Id | Name |
|---|---|
| 1 | Pending |
| 2 | Approved |
| 3 | Rejected |
| 4 | Expired |

These match the `ApprovalStatus` enum.

### `Users`

| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER | Primary key. **Assigned by SQLite** (`AUTOINCREMENT`). |
| `FirstName` | TEXT | Required |
| `LastName` | TEXT | Required |
| `Email` | TEXT | Required, unique |
| `WorkOSId` | TEXT | Required, unique. The WorkOS user ID (the token's `sub` claim). |

### `Approvals`

| Column | Type | Notes |
|---|---|---|
| `Id` | TEXT | Primary key. A GUID stored as text. |
| `Operation` | TEXT | HMAC of the operation and its arguments, used to detect argument changes |
| `StatusId` | INTEGER | → `Statuses.Id` |
| `CreatedAt` | TEXT | UTC |
| `UpdatedAt` | TEXT | UTC |
| `ExpiresAt` | TEXT | UTC. After this time the approval can no longer be decided. |
| `CreatedBy` | INTEGER | → `Users.Id`, the person who requested the operation |
| `UpdatedBy` | INTEGER, nullable | → `Users.Id`, the person who approved or rejected it |

**All timestamps are UTC.** Write them with `DateTime.UtcNow`. SQLite stores dates as text with no time zone, and comparisons such as `@UpdatedAt < ExpiresAt` are text comparisons, so they're only correct when every value uses the same zone.

---

## Models

```csharp
public sealed record Approval
{
    Guid Id; string Operation; ApprovalStatus Status;
    DateTime CreatedAt, UpdatedAt, ExpiresAt;
    int CreatedBy; int? UpdatedBy;
}

public enum ApprovalStatus { Pending = 1, Approved, Rejected, Expired }

public class User
{
    int Id; string WorkOSId, FirstName, LastName, Email;
}
```

---

## Repositories

### `IRepository<T>`

The generic interface both repositories implement:

| Method | Purpose |
|---|---|
| `InitializeAsync()` | Create tables and seed data |
| `AddAsync(T)` | Insert |
| `UpdateAsync(T)` | Update |
| `DeleteAsync(T)` | Delete |
| `GetByIdAsync(Guid)` | Get one |
| `GetAllAsync()` | Get all |

### `ApprovalRepository`

| Method | SQL | Behaviour |
|---|---|---|
| `AddAsync` | `InsertApprovalSql` | Inserts a new approval, normally with status Pending |
| `UpdateAsync` | `UpdateStatusSql` | Records the decision (see below) |
| `GetByIdAsync` | `GetApprovalByIdSql` | Returns `null` if not found |
| `GetAllAsync` | `GetAllApprovalsSql` | All approvals |
| `DeleteAsync` | `DeleteApprovalSql` | Deletes by id |

**`UpdateAsync` only changes an approval that is still open.** The update runs only when both of these hold:

```sql
WHERE Id = @Id
  AND StatusId = @PendingStatusId      -- still Pending
  AND @UpdatedAt < ExpiresAt;          -- not yet expired
```

As a result, an approval can be decided **only once** and **only before it expires**. If either condition fails, nothing is updated.

### `UserRepository` (`IUserRepository`)

| Method | Behaviour |
|---|---|
| `GetOrCreateUserAsync(User)` | Looks the user up by `WorkOSId`. If there's no row, it inserts one and SQLite assigns the `Id`. Returns the stored user. |
| `AddAsync(User)` | Inserts a user without an `Id`, so SQLite assigns it |

`GetByIdAsync`, `GetByEmailAsync`, `GetAllAsync`, `UpdateAsync`, `DeleteAsync` and `InitializeAsync` are not implemented yet; they throw `NotImplementedException`. The `Users` table is created by `ApprovalRepository.InitializeAsync`.

---

## Type mapping notes

| Concern | How it's handled |
|---|---|
| **GUIDs** | SQLite has no GUID type. A Dapper `GuidTypeHandler` stores a `Guid` as text and parses it back when reading. Queries also pass ids as `id.ToString()`. |
| **Status enum** | Stored as `StatusId` (an integer). Queries select `StatusId AS Status` so Dapper maps it onto `ApprovalStatus`. |
| **Connections** | Each call opens a new `SqliteConnection`. Microsoft.Data.Sqlite pools them, so this is cheap. |

---

## Where to add things

- **New SQL:** add a constant to `SqlQueries/SqlQueries.cs`.
- **New repository:** implement `IRepository<T>`, or an interface that extends it, and register it in `AddPersistence()`.
- **Schema changes:** `CREATE TABLE IF NOT EXISTS` does **not** change a table that already exists. For an existing database, add an `ALTER TABLE` step or recreate the database file.
