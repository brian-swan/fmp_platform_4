using System.Net;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using Xunit;

namespace FMP.API.Tests.RateLimit;

public class RateLimitTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public RateLimitTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Request_WithinRateLimit_IncludesRateLimitHeaders()
    {
        // Act
        var response = await _fixture.HttpClient.GetAsync("/environments");

        // Assert
        response.EnsureSuccessStatusCode();
        
        response.Headers.Should().ContainKey("X-RateLimit-Limit");
        response.Headers.Should().ContainKey("X-RateLimit-Remaining");
        response.Headers.Should().ContainKey("X-RateLimit-Reset");
        
        var limit = int.Parse(response.Headers.GetValues("X-RateLimit-Limit").First());
        var remaining = int.Parse(response.Headers.GetValues("X-RateLimit-Remaining").First());
        
        limit.Should().Be(100);
        remaining.Should().BeLessThan(100);
    }

    [Fact(Skip = "This test may cause rate limit issues for other tests")]
    public async Task Request_ExceedingRateLimit_ReturnsTooManyRequests()
    {
        // Arrange - Make 101 requests to exceed rate limit
        for (int i = 0; i < 101; i++)
        {
            await _fixture.HttpClient.GetAsync("/environments");
        }
        
        // Act
        var response = await _fixture.HttpClient.GetAsync("/environments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        
        // Ensure reset time is provided
        response.Headers.Should().ContainKey("X-RateLimit-Reset");
    }
}
