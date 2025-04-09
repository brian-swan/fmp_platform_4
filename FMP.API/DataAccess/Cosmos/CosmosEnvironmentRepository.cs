using FMP.API.DataAccess.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Net;
// Use a more specific type alias to avoid ambiguity
using EnvironmentModel = FMP.API.Models.Environment;

namespace FMP.API.DataAccess.Cosmos;

public class CosmosEnvironmentRepository : IEnvironmentRepository
{
    private readonly CosmosDbContext _context;

    public CosmosEnvironmentRepository(CosmosDbContext context)
    {
        _context = context;
    }

    public async Task<List<EnvironmentModel>> GetAllEnvironmentsAsync()
    {
        var container = await _context.GetEnvironmentsContainerAsync();
        var query = container.GetItemLinqQueryable<EnvironmentModel>().AsQueryable();
        var iterator = query.ToFeedIterator();
        
        var results = new List<EnvironmentModel>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        
        return results;
    }

    public async Task<EnvironmentModel?> GetEnvironmentByIdAsync(string id)
    {
        try
        {
            var container = await _context.GetEnvironmentsContainerAsync();
            var response = await container.ReadItemAsync<EnvironmentModel>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<EnvironmentModel?> GetEnvironmentByKeyAsync(string key)
    {
        var container = await _context.GetEnvironmentsContainerAsync();
        var query = new QueryDefinition("SELECT * FROM c WHERE c.key = @key")
            .WithParameter("@key", key);
        
        var iterator = container.GetItemQueryIterator<EnvironmentModel>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        
        return null;
    }

    public async Task<EnvironmentModel> CreateEnvironmentAsync(EnvironmentModel environment)
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
            await container.DeleteItemAsync<EnvironmentModel>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ApiException("not_found", "Environment not found", 404);
        }
    }

    public async Task<bool> EnvironmentExistsAsync(string key)
    {
        return await GetEnvironmentByKeyAsync(key) != null;
    }
}
