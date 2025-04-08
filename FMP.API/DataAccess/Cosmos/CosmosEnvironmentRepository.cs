using FMP.API.DataAccess.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Environment = FMP.API.Models.Environment;

namespace FMP.API.DataAccess.Cosmos;

public class CosmosEnvironmentRepository : IEnvironmentRepository
{
    private readonly CosmosDbContext _context;

    public CosmosEnvironmentRepository(CosmosDbContext context)
    {
        _context = context;
    }

    public async Task<List<Environment>> GetAllEnvironmentsAsync()
    {
        var container = await _context.GetEnvironmentsContainerAsync();
        var query = container.GetItemLinqQueryable<Environment>().AsQueryable();
        var iterator = query.ToFeedIterator();
        
        var results = new List<Environment>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        
        return results;
    }

    public async Task<Environment?> GetEnvironmentByIdAsync(string id)
    {
        try
        {
            var container = await _context.GetEnvironmentsContainerAsync();
            var response = await container.ReadItemAsync<Environment>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Environment?> GetEnvironmentByKeyAsync(string key)
    {
        var container = await _context.GetEnvironmentsContainerAsync();
        var query = new QueryDefinition("SELECT * FROM c WHERE c.key = @key")
            .WithParameter("@key", key);
        
        var iterator = container.GetItemQueryIterator<Environment>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        
        return null;
    }

    public async Task<Environment> CreateEnvironmentAsync(Environment environment)
    {
        // Check if key already exists
        var existingEnv = await GetEnvironmentByKeyAsync(environment.Key);
        if (existingEnv != null)
        {
            throw new ApiException("invalid_request", "Environment key must be unique", 409,
                new Dictionary<string, object> { { "field", "key" }, { "constraint", "unique" } });
        }
        
        // Set ID and timestamp
        environment.Id = Guid.NewGuid().ToString();
        environment.CreatedAt = DateTime.UtcNow;
        
        var container = await _context.GetEnvironmentsContainerAsync();
        var response = await container.CreateItemAsync(environment, new PartitionKey(environment.Id));
        return response.Resource;
    }

    public async Task DeleteEnvironmentAsync(string id)
    {
        try
        {
            var container = await _context.GetEnvironmentsContainerAsync();
            await container.DeleteItemAsync<Environment>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new ApiException("not_found", "Environment not found", 404);
        }
    }

    public async Task<bool> EnvironmentExistsAsync(string key)
    {
        return await GetEnvironmentByKeyAsync(key) != null;
    }
}
