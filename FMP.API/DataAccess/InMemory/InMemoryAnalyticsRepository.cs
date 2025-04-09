using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;

namespace FMP.API.DataAccess.InMemory;

public class InMemoryAnalyticsRepository : IAnalyticsRepository
{
    private readonly List<Exposure> _exposures = new();
    private readonly IFeatureFlagRepository _flagRepository;

    public InMemoryAnalyticsRepository(IFeatureFlagRepository flagRepository)
    {
        _flagRepository = flagRepository;
    }

    public Task RecordExposureAsync(Exposure exposure)
    {
        // Set ID if not provided
        if (string.IsNullOrEmpty(exposure.Id))
        {
            exposure.Id = Guid.NewGuid().ToString();
        }
        
        _exposures.Add(exposure);
        return Task.CompletedTask;
    }

    public async Task<FlagStatsResponse> GetFlagStatsAsync(string flagId, string flagKey, string environment, string period)
    {
        // Validate flag exists
        var flag = await _flagRepository.GetFlagByIdAsync(flagId);
        if (flag == null)
        {
            throw new ApiException("not_found", "Feature flag not found", 404);
        }
        
        // Parse period
        int days = ParsePeriod(period);
        var startDate = DateTime.UtcNow.Date.AddDays(-days);
        
        // Get exposures for the flag
        var exposures = _exposures
            .Where(e => e.FlagKey == flag.Key && 
                        e.Environment == environment && 
                        e.Timestamp >= startDate)
            .ToList();
        
        // Calculate breakdown by day
        var breakdown = new Dictionary<string, int>();
        for (int i = 0; i <= days; i++)
        {
            var date = startDate.AddDays(i);
            var dateStr = date.ToString("yyyy-MM-dd");
            var count = exposures.Count(e => e.Timestamp.Date == date);
            breakdown[dateStr] = count;
        }
        
        return new FlagStatsResponse
        {
            FlagId = flagId,
            FlagKey = flag.Key,
            Environment = environment,
            Period = period,
            Exposures = new ExposureStats
            {
                Total = exposures.Count,
                Breakdown = breakdown
            }
        };
    }
    
    private int ParsePeriod(string period)
    {
        if (period.EndsWith("d") && int.TryParse(period[..^1], out int days))
        {
            return days;
        }
        
        throw new ApiException("invalid_request", "Invalid period format. Use 'Nd' where N is the number of days", 400);
    }
}
