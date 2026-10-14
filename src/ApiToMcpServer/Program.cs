using ApiToMcpServer.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5001");
builder.Services.AddServices();
builder.Services.AddHttpContextAccessor();

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Missing configuration value 'Jwt:Issuer'.");

var resource = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Missing configuration value 'Jwt:Audience'.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = issuer;
        options.Audience = resource;
        options.MapInboundClaims = false; 

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = resource,

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.Response.Headers["WWW-Authenticate"] =
                    "Bearer error=\"unauthorized\", " +
                    "resource_metadata=" +
                    "\"http://localhost:5001/.well-known/oauth-protected-resource\"";

                return Task.CompletedTask;
            }
        };
    });
//With Tool assembly scans the assembly with attributes of mcp server tool type
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)   // already the default for 2026-07-28, but be explicit
    .WithToolsFromAssembly();
//Not needed since we are explicitly setting the HTTP transport to be stateless
//builder.Services.AddDataProtection(); 
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IRepository<Approval>>();
    await repository.InitializeAsync();
}
app.Use(async (context, next) =>
{
    Console.WriteLine(
        $"Authorization header present: " +
        $"{context.Request.Headers.ContainsKey("Authorization")}");

    await next();
});
app.MapGet("/.well-known/oauth-protected-resource", () =>
    Results.Json(new
    {
        resource,
        authorization_servers = new[] { issuer },
        bearer_methods_supported = new[] { "header" }
    }));
// MacpMcp registers the tools 
app.MapMcp("/mcp").RequireAuthorization();

app.Run();
