using System.Text.Json;
using System.Text.Json.Serialization;

public class RegistryReader
{
    private readonly ApiRegistry _registry;
    public RegistryReader(IConfiguration configuration, IHostEnvironment environment)
    {
        var path = configuration["ApiRegistry:Path"]
            ?? throw new InvalidOperationException(
                "ApiRegistry:Path is not configured.");
        // Relative paths are resolved from the project folder, not the shell's current directory
        if(!Path.IsPathRooted(path))
            path = Path.Combine(environment.ContentRootPath, path);
        if(!File.Exists(path))
         throw new FileNotFoundException(
                $"API registry file not found: {path}");
        var json= File.ReadAllText(path);
        if(json==null)
        throw new NullReferenceException("No valid json data");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
         options.Converters.Add(new JsonStringEnumConverter());       
        _registry = JsonSerializer.Deserialize<ApiRegistry>(json,options)
        ?? throw new InvalidOperationException("Failed to serialize the json");

    }
    public ApiRegistry registry =>_registry;
}