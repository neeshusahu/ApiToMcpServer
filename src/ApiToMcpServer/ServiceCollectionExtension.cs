using ApiToMcpServer.Persistence;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<RegistryReader>();
        // Typed client: ApiExecutor gets an HttpClient pointed at the registry's baseUrl
        services.AddHttpClient<ApiExecutor>((serviceprovider, client) =>
        {
            var apiRegistry= serviceprovider.GetRequiredService<RegistryReader>();
            client.BaseAddress=new Uri(apiRegistry.registry.BaseUrl);
        });
         services.AddPersistence();
         services.AddTransient<WorkOSUserInfoClient>();
         services.AddTransient<IUserService, UserService>();
        //Registration of decorator service
        services.AddScoped<IApiExecutor>(sp=>
        new ApprovalApiExecutor(
            sp.GetRequiredService<ApiExecutor>(),
            sp.GetRequiredService<IRepository<Approval>>(),
            sp.GetRequiredService<IConfiguration>()
        ));
       
        return services;
    }
}