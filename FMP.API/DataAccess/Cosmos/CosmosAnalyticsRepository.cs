using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;
using Microsoft.Azure.Cosmos;

namespace FMP.API.DataAccess.Cosmos;

public class CosmosAnalyticsRepository : IAnalyticsRepository
{
    private readonly CosmosDbContext _context;
    private readonly IFeatureFlagRepository _flagRepository;

    public CosmosAnalyticsRepository(CosmosDbContext context, IFeatureFlagRepository flagRepository)
    {
        _context = context;
        _flagRepository = flagRepository;
    }

    public async Task RecordExposureAsync(Exposure exposure)
    {
        // Set ID if not provided
        if (string.IsNullOrEmpty(exposure.Id))
        {
            exposure.Id = Guid.NewGuid().ToString();
        }
        
        var container = await _context.GetAnalyticsContainerAsync();
        await container.CreateItemAsync(exposure, new PartitionKey(exposure.FlagKey));
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
        
        var container = await _context.GetAnalyticsContainerAsync();
        var query = new QueryDefinition(
            "SELECT COUNT(1) as count, c.timestamp.date as date " +
            "FROM c " +
            "WHERE c.flagKey = @flagKey " +
            "AND c.environment = @environment " +
            "AND c.timestamp >= @startDate " +
            "GROUP BY c.timestamp.date")
            .WithParameter("@flagKey", flag.Key)
            .WithParameter("@environment", environment)
            .WithParameter("@startDate", startDate.ToString("o"));
        
        var iterator = container.GetItemQueryIterator<DailyExposure>(query);
        var dailyExposures = new List<DailyExposure>();
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            dailyExposures.AddRange(response);
        }
        
        // Calculate total
        var total = dailyExposures.Sum(d => d.Count);
        
        // Build the breakdown dictionary
        var breakdown = new Dictionary<string, int>();
        for (int i = 0; i <= days; i++)
        {
            var date = startDate.AddDays(i);
            var dateStr = date.ToString("yyyy-MM-dd");
            var count = dailyExposures.FirstOrDefault(d => d.Date.Date == date.Date)?.Count ?? 0;
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
                Total = total,
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
    
    private class DailyExposure
    {
        public int Count { get; set; }
        public DateTime Date { get; set; }
    }
}
