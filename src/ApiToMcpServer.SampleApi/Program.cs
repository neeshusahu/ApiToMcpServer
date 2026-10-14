using System.Collections.Concurrent;

// In-memory User Management API matching ApiToMcpCli/openapi.yaml.
// It is the target the MCP server calls through registry.json (baseUrl http://localhost:5080).
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");
var app = builder.Build();

var users = new ConcurrentDictionary<string, User>();
var organizations = new ConcurrentDictionary<string, Organization>();

foreach (var (id, name, email) in new[]
{
    ("u_1", "Ada Lovelace", "ada@example.com"),
    ("u_2", "Alan Turing", "alan@example.com"),
    ("u_3", "Grace Hopper", "grace@example.com"),
})
    users[id] = new User(id, name, email);

foreach (var (id, name) in new[] { ("org_1", "Analytical Engines Ltd"), ("org_2", "Bletchley Park") })
    organizations[id] = new Organization(id, name);

// Cursor is the offset of the next item, as a string
static Page<T> Paginate<T>(IEnumerable<T> items, string? cursor, int? limit) where T : IHasId
{
    var ordered = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
    var offset = int.TryParse(cursor, out var c) ? c : 0;
    var size = limit is > 0 ? limit.Value : 20;

    var data = ordered.Skip(offset).Take(size).ToList();
    var next = offset + data.Count;
    var hasNext = next < ordered.Count;
    return new Page<T>(data, hasNext ? next.ToString() : null, hasNext);
}

// ---------- users ----------

app.MapGet("/users", (string? cursor, int? limit) =>
    Results.Ok(Paginate(users.Values, cursor, limit)));

app.MapPost("/users", (UserInput input) =>
{
    var user = new User($"u_{Guid.NewGuid():N}"[..10], input.Name ?? "", input.Email ?? "");
    users[user.Id] = user;
    return Results.Created($"/users/{user.Id}", user);
});

app.MapGet("/users/{id}", (string id) =>
    users.TryGetValue(id, out var user) ? Results.Ok(user) : Results.NotFound());

app.MapPut("/users/{id}", (string id, UserInput input) =>
{
    if (!users.TryGetValue(id, out var existing))
        return Results.NotFound();

    var updated = existing with { Name = input.Name ?? existing.Name, Email = input.Email ?? existing.Email };
    users[id] = updated;
    return Results.Ok(updated);
});

app.MapDelete("/users/{id}", (string id) =>
    users.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound());

// ---------- organizations ----------

app.MapGet("/organizations", (string? cursor, int? limit) =>
    Results.Ok(Paginate(organizations.Values, cursor, limit)));

app.MapGet("/organizations/{id}", (string id) =>
    organizations.TryGetValue(id, out var org) ? Results.Ok(org) : Results.NotFound());

app.MapDelete("/organizations/{id}", (string id) =>
    organizations.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound());

app.Run();

interface IHasId { string Id { get; } }

record User(string Id, string Name, string Email) : IHasId;
record UserInput(string? Name, string? Email);
record Organization(string Id, string Name) : IHasId;
record Page<T>(List<T> Data, string? NextCursor, bool HasNext);
