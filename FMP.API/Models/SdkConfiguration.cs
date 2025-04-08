namespace FMP.API.Models;

public class SdkConfiguration
{
    public string Environment { get; set; } = string.Empty;
    public Dictionary<string, bool> Flags { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class SdkEvaluationRequest
{
    public string Environment { get; set; } = string.Empty;
    public User User { get; set; } = new();
}

public class SdkEvaluationResponse
{
    public string Environment { get; set; } = string.Empty;
    public Dictionary<string, bool> Flags { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
}

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Groups { get; set; } = new();
    public string Country { get; set; } = string.Empty;
}
