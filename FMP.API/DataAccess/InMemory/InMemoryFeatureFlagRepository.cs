using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;

namespace FMP.API.DataAccess.InMemory;

public class InMemoryFeatureFlagRepository : IFeatureFlagRepository
{
    private readonly Dictionary<string, FeatureFlag> _flags = new();
    private readonly IEnvironmentRepository _environmentRepository;

    public InMemoryFeatureFlagRepository(IEnvironmentRepository environmentRepository)
    {
        _environmentRepository = environmentRepository;
    }

    public Task<List<FeatureFlag>> GetAllFlagsAsync(string? projectId = null, string? environmentId = null, int limit = 20, int offset = 0)
    {
        var flags = _flags.Values.ToList();
        
        if (!string.IsNullOrEmpty(projectId))
        {
            // Implement project filtering if you have project concept
        }
        
        if (!string.IsNullOrEmpty(environmentId))
        {
            // Filter by environment
            flags = flags.Where(f => f.State.ContainsKey(environmentId)).ToList();
        }
        
        return Task.FromResult(flags.Skip(offset).Take(limit).ToList());
    }

    public Task<int> GetFlagCountAsync(string? projectId = null, string? environmentId = null)
    {
        var count = _flags.Count;
        
        if (!string.IsNullOrEmpty(environmentId))
        {
            count = _flags.Values.Count(f => f.State.ContainsKey(environmentId));
        }
        
        return Task.FromResult(count);
    }

    public Task<FeatureFlag?> GetFlagByIdAsync(string id)
    {
        _flags.TryGetValue(id, out var flag);
        return Task.FromResult(flag);
    }

    public Task<FeatureFlag?> GetFlagByKeyAsync(string key)
    {
        var flag = _flags.Values.FirstOrDefault(f => f.Key == key);
        return Task.FromResult(flag);
    }

    public async Task<FeatureFlag> CreateFlagAsync(FeatureFlag flag)
    {
        // Check if key already exists
        if (await GetFlagByKeyAsync(flag.Key) != null)
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
        
        _flags[flag.Id] = flag;
        return flag;
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
        _flags[id] = flag;
        
        return flag;
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
        _flags[id] = flag;
        
        return new FeatureFlagStateResponse
        {
            Id = flag.Id!,
            Key = flag.Key,
            State = flag.State,
            UpdatedAt = flag.UpdatedAt
        };
    }

    public async Task DeleteFlagAsync(string id)
    {
        var flag = await GetFlagByIdAsync(id);
        if (flag == null)
        {
            throw new ApiException("not_found", "Feature flag not found", 404);
        }
        
        _flags.Remove(id);
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
        _flags[flagId] = flag;
        
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
        _flags[flagId] = flag;
    }
}
