using FMP.API.Models;

namespace FMP.API.DataAccess.Interfaces;

public interface IFeatureFlagRepository
{
    Task<List<FeatureFlag>> GetAllFlagsAsync(string? projectId = null, string? environmentId = null, int limit = 20, int offset = 0);
    Task<int> GetFlagCountAsync(string? projectId = null, string? environmentId = null);
    Task<FeatureFlag?> GetFlagByIdAsync(string id);
    Task<FeatureFlag?> GetFlagByKeyAsync(string key);
    Task<FeatureFlag> CreateFlagAsync(FeatureFlag flag);
    Task<FeatureFlag> UpdateFlagAsync(string id, FeatureFlagUpdateRequest updateRequest);
    Task<FeatureFlagStateResponse> UpdateFlagStateAsync(string id, string environment, bool enabled);
    Task DeleteFlagAsync(string id);
    
    // Targeting rules
    Task<TargetingRule> AddTargetingRuleAsync(string flagId, TargetingRule rule);
    Task DeleteTargetingRuleAsync(string flagId, string ruleId);
}
