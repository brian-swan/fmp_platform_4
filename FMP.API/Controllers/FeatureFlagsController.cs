using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace FMP.API.Controllers;

[ApiController]
[Route("v1/flags")]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagRepository _repository;
    
    public FeatureFlagsController(IFeatureFlagRepository repository)
    {
        _repository = repository;
    }
    
    [HttpGet]
    public async Task<ActionResult<FeatureFlagListResponse>> GetFeatureFlags(
        [FromQuery] string? project_id,
        [FromQuery] string? environment_id,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0)
    {
        var flags = await _repository.GetAllFlagsAsync(project_id, environment_id, limit, offset);
        var total = await _repository.GetFlagCountAsync(project_id, environment_id);
        
        return Ok(new FeatureFlagListResponse
        {
            Flags = flags,
            Total = total,
            Limit = limit,
            Offset = offset
        });
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<FeatureFlag>> GetFeatureFlag(string id)
    {
        var flag = await _repository.GetFlagByIdAsync(id);
        if (flag == null)
        {
            return NotFound();
        }
        
        return Ok(flag);
    }
    
    [HttpPost]
    public async Task<ActionResult<FeatureFlag>> CreateFeatureFlag(FeatureFlagCreateRequest request)
    {
        var flag = new FeatureFlag
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            State = request.State,
            Tags = request.Tags
        };
        
        var createdFlag = await _repository.CreateFlagAsync(flag);
        
        return CreatedAtAction(nameof(GetFeatureFlag), new { id = createdFlag.Id }, createdFlag);
    }
    
    [HttpPut("{id}")]
    public async Task<ActionResult<FeatureFlag>> UpdateFeatureFlag(string id, FeatureFlagUpdateRequest request)
    {
        var updatedFlag = await _repository.UpdateFlagAsync(id, request);
        return Ok(updatedFlag);
    }
    
    [HttpPatch("{id}/state")]
    public async Task<ActionResult<FeatureFlagStateResponse>> UpdateFeatureFlagState(
        string id, FeatureFlagStateUpdateRequest request)
    {
        var response = await _repository.UpdateFlagStateAsync(id, request.Environment, request.Enabled);
        return Ok(response);
    }
    
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteFeatureFlag(string id)
    {
        await _repository.DeleteFlagAsync(id);
        return NoContent();
    }
    
    [HttpPost("{flagId}/rules")]
    public async Task<ActionResult<TargetingRule>> AddTargetingRule(string flagId, TargetingRuleCreateRequest request)
    {
        var rule = new TargetingRule
        {
            Type = request.Type,
            Attribute = request.Attribute,
            Operator = request.Operator,
            Values = request.Values,
            Environment = request.Environment
        };
        
        var createdRule = await _repository.AddTargetingRuleAsync(flagId, rule);
        
        return CreatedAtAction(
            nameof(GetFeatureFlag), 
            new { id = flagId }, 
            createdRule);
    }
    
    [HttpDelete("{flagId}/rules/{ruleId}")]
    public async Task<ActionResult> DeleteTargetingRule(string flagId, string ruleId)
    {
        await _repository.DeleteTargetingRuleAsync(flagId, ruleId);
        return NoContent();
    }
}
