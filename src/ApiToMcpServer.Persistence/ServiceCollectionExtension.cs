using Microsoft.Extensions.DependencyInjection;

namespace ApiToMcpServer.Persistence;

public static class PersistenceServiceCollectionExtension
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRepository<Approval>, ApprovalRepository>();
        // Register your repositories here
        // Example: services.AddScoped<IRepository<YourEntity>, YourEntityRepository>();
        return services;
    }
}
