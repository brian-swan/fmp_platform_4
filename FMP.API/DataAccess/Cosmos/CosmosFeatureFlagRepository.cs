using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace FMP.API.DataAccess.Cosmos;

public class CosmosFeatureFlagRepository : IFeatureFlagRepository
{
    private readonly CosmosDbContext _context;
    private readonly IEnvironmentRepository _environmentRepository;

    public CosmosFeatureFlagRepository(CosmosDbContext context, IEnvironmentRepository environmentRepository)
    {
        _context = context;
        _environmentRepository = environmentRepository;
    }

    public async Task<List<FeatureFlag>> GetAllFlagsAsync(string? projectId = null, string? environmentId = null, int limit = 20, int offset = 0)
    {
        var container = await _context.GetFlagsContainerAsync();
        var query = container.GetItemLinqQueryable<FeatureFlag>().AsQueryable();
        
        if (!string.IsNullOrEmpty(environmentId))
        {
            // Filter by environment - note this is more complex in Cosmos as it's a dict property
            // This is a simplified approach - may need optimization
            query = query.Where(f => f.State.Keys.Contains(environmentId));
        }
        
        var iterator = query.Skip(offset).Take(limit).ToFeedIterator();
        var results = new List<FeatureFlag>();
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        
        return results;
    }

    public async Task<int> GetFlagCountAsync(string? projectId = null, string? environmentId = null)
    {
        var container = await _context.GetFlagsContainerAsync();
        
        // For a precise count, you'd typically need to run a query
        // Note: In a production scenario with large datasets, consider using techniques
        // like approximate counts or caching
        var countQuery = "SELECT VALUE COUNT(1) FROM c";
        
        if (!string.IsNullOrEmpty(environmentId))
        {
            // SQL query that checks for environment key in the State dictionary
            countQuery = $"SELECT VALUE COUNT(1) FROM c WHERE EXISTS(SELECT VALUE k FROM k IN OBJECT_KEYS(c.state) WHERE k = '{environmentId}')";
        }
        
        var queryDefinition = new QueryDefinition(countQuery);
        var iterator = container.GetItemQueryIterator<int>(queryDefinition);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.Resource.FirstOrDefault();
        }
        
        return 0;
    }

    public async Task<FeatureFlag?> GetFlagByIdAsync(string id)
    {
        try
        {
            var container = await _context.GetFlagsContainerAsync();
            var response = await container.ReadItemAsync<FeatureFlag>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<FeatureFlag?> GetFlagByKeyAsync(string key)
    {
        var container = await _context.GetFlagsContainerAsync();
        var query = new QueryDefinition("SELECT * FROM c WHERE c.key = @key")
            .WithParameter("@key", key);
        
        var iterator = container.GetItemQueryIterator<FeatureFlag>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        
        return null;
    }

    public async Task<FeatureFlag> CreateFlagAsync(FeatureFlag flag)
    {
        // Check if key already exists
        var existingFlag = await GetFlagByKeyAsync(flag.Key);
        if (existingFlag != null)
        {
            throw new ApiException("invalid_request", "Flag key must be unique", 409,
                new Dictionary<string, object> { { "field", "key" }, { "constraint", "unique" } });
        }
        
        // Ensure environments exist
        foreach (var env in flag.State.Keys)
        {
            if (!await _environmentRepository.EnvironmentExistsAsync(env))
            {
                throw new ApiException("invalid_request", $"Environment '{env}' does not exist", 400);
            }
        }
        
        // Set timestamps and ID
        flag.Id = Guid.NewGuid().ToString();
        flag.CreatedAt = DateTime.UtcNow;
        flag.UpdatedAt = flag.CreatedAt;
        
        var container = await _context.GetFlagsContainerAsync();
        var response = await container.CreateItemAsync(flag, new PartitionKey(flag.Id));
        return response.Resource;
    }

    public async Task<FeatureFlag> UpdateFlagAsync(string id, FeatureFlagUpdateRequest updateRequest)
    {
        var flag = await GetFlagByIdAsync(id);
        if (flag == null)
        {
            throw new ApiException("not_found", "Feature flag not found", 404);
        }
        
        if (updateRequest.Name != null)
            flag.Name = updateRequest.Name;
        
        if (updateRequest.Description != null)
            flag.Description = updateRequest.Description;
        
        if (updateRequest.Tags != null)
            flag.Tags = updateRequest.Tags;
        
        flag.UpdatedAt = DateTime.UtcNow;
        
        var container = await _context.GetFlagsContainerAsync();
        var response = await container.ReplaceItemAsync(flag, id, new PartitionKey(id));
        return response.Resource;
    }

    public async Task<FeatureFlagStateResponse> UpdateFlagStateAsync(string id, string environment, bool enabled)
    {
        var flag = await GetFlagByIdAsync(id);
        if (flag == null)
        {
            throw new ApiException("not_found", "Feature flag not found", 404);
        }
        
        // Verify the environment exists
        if (!await _environmentRepository.EnvironmentExistsAsync(environment))
        {
            throw new ApiException("invalid_request", $"Environment '{environment}' does not exist", 400);
        }
        
        flag.State[environment] = enabled;
        flag.UpdatedAt = DateTime.UtcNow;
        
        var container = await _context.GetFlagsContainerAsync();
        var response = await container.ReplaceItemAsync(flag, id, new PartitionKey(id));
        
        return new FeatureFlagStateResponse
        {
            Id = response.Resource.Id!,
            Key = response.Resource.Key,
            State = response.Resource.State,
            UpdatedAt = response.Resource.UpdatedAt
        };
    }

    public async Task DeleteFlagAsync(string id)
    {
        try
        {
            var container = await _context.GetFlagsContainerAsync();
            await container.DeleteItemAsync<FeatureFlag>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new ApiException("not_found", "Feature flag not found", 404);
        }
    }

    public async Task<TargetingRule> AddTargetingRuleAsync(string flagId, TargetingRule rule)
    {
        var flag = await GetFlagByIdAsync(flagId);
        if (flag == null)
        {
            throw new ApiException("not_found", "Feature flag not found", 404);
        }
        
        // Validate the environment
        if (!await _environmentRepository.EnvironmentExistsAsync(rule.Environment))
        {
            throw new ApiException("invalid_request", $"Environment '{rule.Environment}' does not exist", 400);
        }
        
        // Initialize rules collection if null
        flag.Rules ??= new List<TargetingRule>();
        
        // Set rule ID and timestamp
        rule.Id = Guid.NewGuid().ToString();
        rule.CreatedAt = DateTime.UtcNow;
        
        flag.Rules.Add(rule);
        flag.UpdatedAt = DateTime.UtcNow;
        
        var container = await _context.GetFlagsContainerAsync();
        await container.ReplaceItemAsync(flag, flagId, new PartitionKey(flagId));
        
        return rule;
    }

    public async Task DeleteTargetingRuleAsync(string flagId, string ruleId)
    {
        var flag = await GetFlagByIdAsync(flagId);
        if (flag == null)
        {
            throw new ApiException("not_found", "Feature flag not found", 404);
        }
        
        if (flag.Rules == null || !flag.Rules.Any(r => r.Id == ruleId))
        {
            throw new ApiException("not_found", "Targeting rule not found", 404);
        }
        
        flag.Rules.RemoveAll(r => r.Id == ruleId);
        flag.UpdatedAt = DateTime.UtcNow;
        
        var container = await _context.GetFlagsContainerAsync();
        await container.ReplaceItemAsync(flag, flagId, new PartitionKey(flagId));
    }
}
