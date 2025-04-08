using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace FMP.API.Controllers;

[ApiController]
[Route("v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly IFeatureFlagRepository _flagRepository;
    
    public AnalyticsController(
        IAnalyticsRepository analyticsRepository,
        IFeatureFlagRepository flagRepository)
    {
        _analyticsRepository = analyticsRepository;
        _flagRepository = flagRepository;
    }
    
    [HttpPost("exposure")]
    public async Task<ActionResult> RecordExposure(ExposureRequest request)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(request.FlagKey) || 
            string.IsNullOrEmpty(request.Environment) || 
            string.IsNullOrEmpty(request.UserId))
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Code = "invalid_request",
                    Message = "Missing required fields"
                }
            });
        }
        
        // Create the exposure record
        var exposure = new Exposure
        {
            FlagKey = request.FlagKey,
            Environment = request.Environment,
            UserId = request.UserId,
            Timestamp = request.Timestamp != default ? request.Timestamp : DateTime.UtcNow,
            ClientId = !string.IsNullOrEmpty(request.ClientId) ? request.ClientId : "unknown"
        };
        
        await _analyticsRepository.RecordExposureAsync(exposure);
        
        return NoContent();
    }
    
    [HttpGet("flags/{flagId}/stats")]
    public async Task<ActionResult<FlagStatsResponse>> GetFlagStats(
        string flagId, 
        [FromQuery] string environment,
        [FromQuery] string period = "7d")
    {
        // Validate flag exists
        var flag = await _flagRepository.GetFlagByIdAsync(flagId);
        if (flag == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Code = "not_found",
                    Message = "Feature flag not found"
                }
            });
        }
        
        try
        {
            var stats = await _analyticsRepository.GetFlagStatsAsync(flagId, flag.Key, environment, period);
            return Ok(stats);
        }
        catch (ApiException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    Details = ex.Details
                }
            });
        }
    }
}
