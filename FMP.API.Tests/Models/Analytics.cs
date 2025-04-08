namespace FMP.API.Tests.Models;

public class ExposureRequest
{
    public string FlagKey { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ClientId { get; set; } = string.Empty;
}

public class FlagStatsResponse
{
    public string FlagId { get; set; } = string.Empty;
    public string FlagKey { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public ExposureStats Exposures { get; set; } = new();
}

public class ExposureStats
{
    public int Total { get; set; }
    public Dictionary<string, int> Breakdown { get; set; } = new();
}
