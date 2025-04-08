namespace FMP.API.Tests.Models;

public class TargetingRule
{
    public string? Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public List<string> Values { get; set; } = new();
    public string Environment { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public class TargetingRuleCreateRequest
{
    public string Type { get; set; } = string.Empty;
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public List<string> Values { get; set; } = new();
    public string Environment { get; set; } = string.Empty;
}
