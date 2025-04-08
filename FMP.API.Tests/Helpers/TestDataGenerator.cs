using Bogus;
using FMP.API.Tests.Models;

namespace FMP.API.Tests.Helpers;

public static class TestDataGenerator
{
    private static readonly Faker Faker = new Faker();

    public static FeatureFlagCreateRequest GenerateFeatureFlagCreateRequest()
    {
        return new FeatureFlagCreateRequest
        {
            Key = $"test-flag-{Guid.NewGuid()}",
            Name = Faker.Commerce.ProductName(),
            Description = Faker.Lorem.Sentence(),
            State = new Dictionary<string, bool>
            {
                { "dev", true },
                { "staging", false },
                { "production", false }
            },
            Tags = Faker.Lorem.Words(3).ToList()
        };
    }

    public static FeatureFlagUpdateRequest GenerateFeatureFlagUpdateRequest()
    {
        return new FeatureFlagUpdateRequest
        {
            Name = Faker.Commerce.ProductName(),
            Description = Faker.Lorem.Sentence(),
            Tags = Faker.Lorem.Words(4).ToList()
        };
    }

    public static TargetingRuleCreateRequest GenerateTargetingRuleCreateRequest()
    {
        return new TargetingRuleCreateRequest
        {
            Type = "user",
            Attribute = "email",
            Operator = "ends_with",
            Values = new List<string> { "@example.com" },
            Environment = "staging"
        };
    }

    public static EnvironmentCreateRequest GenerateEnvironmentCreateRequest()
    {
        string key = Faker.Internet.DomainWord().ToLower();
        return new EnvironmentCreateRequest
        {
            Key = key,
            Name = key.First().ToString().ToUpper() + key.Substring(1),
            Description = $"{key} environment for testing"
        };
    }

    public static SdkEvaluationRequest GenerateEvaluationRequest()
    {
        return new SdkEvaluationRequest
        {
            Environment = "production",
            User = new User
            {
                Id = $"user-{Guid.NewGuid()}",
                Email = Faker.Internet.Email(),
                Groups = new List<string> { "beta-testers" },
                Country = Faker.Address.CountryCode()
            }
        };
    }

    public static ExposureRequest GenerateExposureRequest()
    {
        return new ExposureRequest
        {
            FlagKey = $"test-flag-{Faker.Random.AlphaNumeric(5)}",
            Environment = "production",
            UserId = $"user-{Guid.NewGuid()}",
            Timestamp = DateTime.UtcNow,
            ClientId = "web-app"
        };
    }
}
