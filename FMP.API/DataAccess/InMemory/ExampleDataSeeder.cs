using FMP.API.DataAccess.Interfaces;
using FMP.API.Models;
using Environment = FMP.API.Models.Environment;

namespace FMP.API.DataAccess.InMemory;

public class ExampleDataSeeder : IExampleDataSeeder
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IFeatureFlagRepository _featureFlagRepository;
    private readonly IAnalyticsRepository _analyticsRepository;

    public ExampleDataSeeder(
        IEnvironmentRepository environmentRepository,
        IFeatureFlagRepository featureFlagRepository,
        IAnalyticsRepository analyticsRepository)
    {
        _environmentRepository = environmentRepository;
        _featureFlagRepository = featureFlagRepository;
        _analyticsRepository = analyticsRepository;
    }

    public async Task SeedExampleData()
    {
        // Create default environments
        var environments = new List<Environment>
        {
            new() { Key = "dev", Name = "Development", Description = "Development environment" },
            new() { Key = "staging", Name = "Staging", Description = "Staging/QA environment" },
            new() { Key = "production", Name = "Production", Description = "Production environment" }
        };
        
        foreach (var env in environments)
        {
            await _environmentRepository.CreateEnvironmentAsync(env);
        }
        
        // Create example feature flags
        var flags = new List<FeatureFlag>
        {
            new()
            {
                Key = "new-checkout-flow",
                Name = "New Checkout Flow",
                Description = "Enables the new checkout experience",
                State = new Dictionary<string, bool>
                {
                    { "dev", true },
                    { "staging", true },
                    { "production", false }
                },
                Tags = new List<string> { "checkout", "beta" },
                Rules = new List<TargetingRule>
                {
                    new()
                    {
                        Type = "user",
                        Attribute = "email",
                        Operator = "ends_with",
                        Values = new List<string> { "@company.com" },
                        Environment = "staging"
                    }
                }
            },
            new()
            {
                Key = "dark-mode",
                Name = "Dark Mode",
                Description = "Enables dark mode UI",
                State = new Dictionary<string, bool>
                {
                    { "dev", true },
                    { "staging", true },
                    { "production", true }
                },
                Tags = new List<string> { "ui", "theme" }
            },
            new()
            {
                Key = "recommendation-engine",
                Name = "Recommendation Engine",
                Description = "Enables the new ML-based recommendation engine",
                State = new Dictionary<string, bool>
                {
                    { "dev", true },
                    { "staging", true },
                    { "production", true }
                },
                Tags = new List<string> { "ai", "recommendations" }
            }
        };
        
        foreach (var flag in flags)
        {
            await _featureFlagRepository.CreateFlagAsync(flag);
            
            // Add some exposure data
            for (int i = 0; i < 10; i++)
            {
                var exposure = new Exposure
                {
                    FlagKey = flag.Key,
                    Environment = "production",
                    UserId = $"user-{Guid.NewGuid()}",
                    Timestamp = DateTime.UtcNow.AddHours(-i),
                    ClientId = "web-app"
                };
                await _analyticsRepository.RecordExposureAsync(exposure);
            }
        }
    }
}
