using System.Text.Json.Serialization;

namespace FMP.API.Tests.Models;

public class FeatureFlag
{
    public string? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, bool> State { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<TargetingRule>? Rules { get; set; }
}

public class FeatureFlagListResponse
{
    public List<FeatureFlag> Flags { get; set; } = new();
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

public class FeatureFlagCreateRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, bool> State { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class FeatureFlagUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
}

public class FeatureFlagStateUpdateRequest
{
    public string Environment { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class FeatureFlagStateResponse
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, bool> State { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}
