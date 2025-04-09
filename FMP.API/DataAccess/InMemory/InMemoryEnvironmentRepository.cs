using FMP.API.DataAccess.Interfaces;
using Environment = FMP.API.Models.Environment;

namespace FMP.API.DataAccess.InMemory;

public class InMemoryEnvironmentRepository : IEnvironmentRepository
{
    private readonly Dictionary<string, Environment> _environments = new();

    public Task<List<Environment>> GetAllEnvironmentsAsync()
    {
        return Task.FromResult(_environments.Values.ToList());
    }

    public Task<Environment?> GetEnvironmentByIdAsync(string id)
    {
        _environments.TryGetValue(id, out var environment);
        return Task.FromResult(environment);
    }

    public Task<Environment?> GetEnvironmentByKeyAsync(string key)
    {
        var environment = _environments.Values.FirstOrDefault(e => e.Key == key);
        return Task.FromResult(environment);
    }

    public async Task<Environment> CreateEnvironmentAsync(Environment environment)
    {
        // Check if key already exists
        if (await GetEnvironmentByKeyAsync(environment.Key) != null)
        {
            throw new ApiException("invalid_request", "Environment key must be unique", 409,
                new Dictionary<string, object> { { "field", "key" }, { "constraint", "unique" } });
        }
        
        // Set ID and timestamp
        environment.Id = Guid.NewGuid().ToString();
        environment.CreatedAt = DateTime.UtcNow;
        
        _environments[environment.Id] = environment;
        return environment;
    }

    public async Task DeleteEnvironmentAsync(string id)
    {
        var environment = await GetEnvironmentByIdAsync(id);
        if (environment == null)
        {
            throw new ApiException("not_found", "Environment not found", 404);
        }
        
        _environments.Remove(id);
    }

    public async Task<bool> EnvironmentExistsAsync(string key)
    {
        return await GetEnvironmentByKeyAsync(key) != null;
    }
}
