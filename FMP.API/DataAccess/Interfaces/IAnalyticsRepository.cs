using FMP.API.Models;

namespace FMP.API.DataAccess.Interfaces;

public interface IAnalyticsRepository
{
    Task RecordExposureAsync(Exposure exposure);
    Task<FlagStatsResponse> GetFlagStatsAsync(string flagId, string flagKey, string environment, string period);
}
