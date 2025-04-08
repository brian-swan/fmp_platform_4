namespace FMP.API.Tests.Models;

// Renamed to EnvironmentConfig to avoid ambiguity with System.Environment
public class EnvironmentConfig
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class EnvironmentListResponse
{
    public List<EnvironmentConfig> Environments { get; set; } = new();
}

public class EnvironmentCreateRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
