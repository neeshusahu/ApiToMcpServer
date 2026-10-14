using ApiToMcpServer.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WorkOS;
using User = ApiToMcpServer.Persistence.Model.User;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddPersistence();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
var issuer = builder.Configuration["WorkOS:Issuer"]!;
var clientId = builder.Configuration["WorkOS:ClientId"];
var apiKey = builder.Configuration["WorkOS:ApiKey"];

var jwksUrl = $"https://api.workos.com/sso/jwks/{clientId}";

// Load WorkOS public signing keys.
using var httpClient = new HttpClient();
var jwksJson = await httpClient.GetStringAsync(jwksUrl);
var jwks = new JsonWebKeySet(jwksJson);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        //The built-in helpers User.Identity.Name, User.IsInRole(...) 
        // and [Authorize(Roles = "...")] look for the long claim names by default. With mapping off they won't find anything. If you start using them, for example to allow only users with the WorkOS role approver, tell the handler which short claims to use:
        options.MapInboundClaims = false; // keep "sub" as "sub" instead of ClaimTypes.NameIdentifier
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidateAudience = false,
            IssuerSigningKeyResolver = (_, _, keyId, _) =>
                jwks.GetSigningKeys()
                    .Where(key => key.KeyId == keyId)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton(_ =>
    new WorkOSClient(new WorkOSOptions
    {
        ApiKey = builder.Configuration["WorkOS:ApiKey"]!,
        ClientId = builder.Configuration["WorkOS:ClientId"]!
    }));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Api v1"));
}

app.UseHttpsRedirection();
app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();


app.MapPost("/approval/{id}", async (Guid id, int approvalStatus, IRepository<Approval> repository, IUserRepository userRepository, WorkOSClient workOSClient, ClaimsPrincipal user) =>
{
    try
    {
        if (user == null)
        {
            return Results.Unauthorized();
        }
        var workOsUserId = user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(workOsUserId))
            return Results.Unauthorized();

        var profile = await workOSClient.UserManagement
            .GetAsync(workOsUserId);


        var currentUser = new User
        {
            WorkOSId = profile.Id,
            Email = profile.Email,
            FirstName = profile?.FirstName,
            LastName = profile?.LastName
        };
        var updatedUser = await userRepository.GetOrCreateUserAsync(currentUser);
        await repository.UpdateAsync(new Approval
        {
            Id = id,
            Status = (ApprovalStatus)approvalStatus,
            UpdatedBy = updatedUser?.Id,
            UpdatedAt = DateTime.UtcNow
        });
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }

})
.WithName("UpdateApprovalStatus").RequireAuthorization();

app.MapGet("/approval/{id}", async (Guid id, IRepository<Approval> repository) =>
{
    try
    {
        var approval = await repository.GetByIdAsync(id);
        if (approval == null)
        {
            return Results.NotFound();
        }
        return Results.Ok(approval);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("GetApprovalById").RequireAuthorization();

app.MapGet("/approvals", async (IRepository<Approval> repository) =>
{
    try
    {
        var approvals = await repository.GetAllAsync();
        return Results.Ok(approvals);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("GetAllApprovals").RequireAuthorization();

app.Run();


