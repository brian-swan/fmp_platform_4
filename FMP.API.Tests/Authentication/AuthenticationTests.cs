using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using Xunit;

namespace FMP.API.Tests.Authentication;

public class AuthenticationTests
{
    private const string BaseUrl = "https://api.featureflag.example/v1";
    
    [Fact]
    public async Task Request_WithValidApiKey_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", "test-api-key");
        
        // Act
        var response = await httpClient.GetAsync("/environments");
        
        // Assert
        response.EnsureSuccessStatusCode();
    }
    
    [Fact]
    public async Task Request_WithNoApiKey_ReturnsUnauthorized()
    {
        // Arrange
        using var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        
        // Act
        var response = await httpClient.GetAsync("/environments");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task Request_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        using var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", "invalid-key");
        
        // Act
        var response = await httpClient.GetAsync("/environments");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task Request_WithWrongAuthScheme_ReturnsUnauthorized()
    {
        // Arrange
        using var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-api-key");
        
        // Act
        var response = await httpClient.GetAsync("/environments");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
