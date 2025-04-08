using System.Net;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using FMP.API.Tests.Helpers;
using FMP.API.Tests.Models;
using Xunit;

namespace FMP.API.Tests.Integration;

public class EndToEndTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public EndToEndTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteFeatureFlagLifecycle()
    {
        // 1. Create a feature flag
        var createRequest = TestDataGenerator.GenerateFeatureFlagCreateRequest();
        var createContent = _fixture.CreateJsonContent(createRequest);
        var createResponse = await _fixture.HttpClient.PostAsync("/flags", createContent);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var flag = await _fixture.DeserializeResponseAsync<FeatureFlag>(createResponse);
        flag.Should().NotBeNull();
        
        // 2. Add a targeting rule
        var ruleRequest = TestDataGenerator.GenerateTargetingRuleCreateRequest();
        var ruleContent = _fixture.CreateJsonContent(ruleRequest);
        var ruleResponse = await _fixture.HttpClient.PostAsync($"/flags/{flag!.Id}/rules", ruleContent);
        ruleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var rule = await _fixture.DeserializeResponseAsync<TargetingRule>(ruleResponse);
        rule.Should().NotBeNull();
        
        // 3. Toggle flag state
        var toggleRequest = new FeatureFlagStateUpdateRequest
        {
            Environment = "production",
            Enabled = true
        };
        var toggleContent = _fixture.CreateJsonContent(toggleRequest);
        var toggleResponse = await _fixture.HttpClient.PatchAsync($"/flags/{flag.Id}/state", toggleContent);
        toggleResponse.EnsureSuccessStatusCode();
        
        // 4. Verify flag with SDK evaluation
        var evalRequest = new SdkEvaluationRequest
        {
            Environment = "production",
            User = new User
            {
                Id = "user-123",
                Email = "test@company.com",
                Groups = new List<string> { "beta-testers" },
                Country = "US"
            }
        };
        var evalContent = _fixture.CreateJsonContent(evalRequest);
        var evalResponse = await _fixture.HttpClient.PostAsync("/sdk/evaluate", evalContent);
        evalResponse.EnsureSuccessStatusCode();
        
        var evalResult = await _fixture.DeserializeResponseAsync<SdkEvaluationResponse>(evalResponse);
        evalResult.Should().NotBeNull();
        evalResult!.Flags.Should().ContainKey(flag.Key);
        evalResult.Flags[flag.Key].Should().BeTrue();
        
        // 5. Record exposure
        var exposureRequest = new ExposureRequest
        {
            FlagKey = flag.Key,
            Environment = "production",
            UserId = "user-123",
            Timestamp = DateTime.UtcNow,
            ClientId = "test-client"
        };
        var exposureContent = _fixture.CreateJsonContent(exposureRequest);
        var exposureResponse = await _fixture.HttpClient.PostAsync("/analytics/exposure", exposureContent);
        exposureResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // 6. Get usage stats
        var statsResponse = await _fixture.HttpClient.GetAsync($"/analytics/flags/{flag.Id}/stats?environment=production&period=7d");
        statsResponse.EnsureSuccessStatusCode();
        
        var stats = await _fixture.DeserializeResponseAsync<FlagStatsResponse>(statsResponse);
        stats.Should().NotBeNull();
        stats!.FlagId.Should().Be(flag.Id);
        
        // 7. Delete the flag
        var deleteResponse = await _fixture.HttpClient.DeleteAsync($"/flags/{flag.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // 8. Verify it's gone
        var getResponse = await _fixture.HttpClient.GetAsync($"/flags/{flag.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
