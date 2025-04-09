using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace FMP.API.Controllers;

[ApiController]
[Route("v1/sdk")]
public class SdkController : ControllerBase
{
    private readonly IFeatureFlagRepository _flagRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    
    public SdkController(
        IFeatureFlagRepository flagRepository,
        IEnvironmentRepository environmentRepository)
    {
        _flagRepository = flagRepository;
        _environmentRepository = environmentRepository;
    }
    
    [HttpGet("config")]
    public async Task<ActionResult<SdkConfiguration>> GetClientConfig([FromQuery] string environment)
    {
        // Validate environment exists
        var environmentExists = await _environmentRepository.EnvironmentExistsAsync(environment);
        if (!environmentExists)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Code = "invalid_request",
                    Message = $"Environment '{environment}' does not exist"
                }
            });
        }
        
        // Get all flags
        var flags = await _flagRepository.GetAllFlagsAsync(environmentId: environment);
        
        // Create flag dictionary with state for this environment
        var flagStates = flags
            .Where(f => f.State.ContainsKey(environment))
            .ToDictionary(f => f.Key, f => f.State[environment]);
        
        return Ok(new SdkConfiguration
        {
            Environment = environment,
            Flags = flagStates,
            UpdatedAt = DateTime.UtcNow
        });
    }
    
    [HttpPost("evaluate")]
    public async Task<ActionResult<SdkEvaluationResponse>> EvaluateFlags(SdkEvaluationRequest request)
    {
        // Validate environment exists
        var environmentExists = await _environmentRepository.EnvironmentExistsAsync(request.Environment);
        if (!environmentExists)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Code = "invalid_request",
                    Message = $"Environment '{request.Environment}' does not exist"
                }
            });
        }
        
        // Get all flags
        var flags = await _flagRepository.GetAllFlagsAsync(environmentId: request.Environment);
        
        // Create flag dictionary with state for this environment, applying targeting rules
        var flagStates = new Dictionary<string, bool>();
        
        foreach (var flag in flags)
        {
            if (!flag.State.ContainsKey(request.Environment))
            {
                continue;
            }
            
            // Start with the default state for the environment
            bool isEnabled = flag.State[request.Environment];
            
            // Apply targeting rules if any
            if (flag.Rules != null && flag.Rules.Count > 0)
            {
                // Only consider rules for the requested environment
                var environmentRules = flag.Rules.Where(r => r.Environment == request.Environment).ToList();
                
                foreach (var rule in environmentRules)
                {
                    // Check if rule applies to this user
                    if (RuleAppliesToUser(rule, request.User))
                    {
                        isEnabled = true;
                        break;
                    }
                }
            }
            
            flagStates[flag.Key] = isEnabled;
        }
        
        return Ok(new SdkEvaluationResponse
        {
            Environment = request.Environment,
            Flags = flagStates,
            EvaluatedAt = DateTime.UtcNow
        });
    }
    
    private bool RuleAppliesToUser(TargetingRule rule, User user)
    {
        // Simple rule evaluation based on the rule type and attribute
        if (rule.Type == "user")
        {
            switch (rule.Attribute)
            {
                case "id":
                    return EvaluateCondition(user.Id, rule.Operator, rule.Values);
                
                case "email":
                    return EvaluateCondition(user.Email, rule.Operator, rule.Values);
                
                case "country":
                    return EvaluateCondition(user.Country, rule.Operator, rule.Values);
            }
        }
        else if (rule.Type == "group" && rule.Attribute == "name")
        {
            foreach (var group in user.Groups)
            {
                if (EvaluateCondition(group, rule.Operator, rule.Values))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private bool EvaluateCondition(string value, string op, List<string> compareValues)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        
        switch (op)
        {
            case "equals":
                return compareValues.Contains(value);
            
            case "not_equals":
                return !compareValues.Contains(value);
            
            case "starts_with":
                return compareValues.Any(v => value.StartsWith(v));
            
            case "ends_with":
                return compareValues.Any(v => value.EndsWith(v));
            
            case "contains":
                return compareValues.Any(v => value.Contains(v));
            
            case "not_contains":
                return compareValues.All(v => !value.Contains(v));
            
            default:
                return false;
        }
    }
}
