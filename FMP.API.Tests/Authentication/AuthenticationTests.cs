using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using Xunit;

namespace FMP.API.Tests.Authentication;

public class AuthenticationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    
    public AuthenticationTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Request_WithValidApiKey_Succeeds()
    {
        // Act - use the fixture's HttpClient which already has a valid API key
        var response = await _fixture.HttpClient.GetAsync("/environments");
        
        // Assert
        response.EnsureSuccessStatusCode();
    }
    
    [Fact]
    public async Task Request_WithNoApiKey_ReturnsUnauthorized()
    {
        // Arrange - create a new client without auth headers
        using var httpClient = new HttpClient 
        { 
            BaseAddress = _fixture.HttpClient.BaseAddress 
        };
        
        // Act
        var response = await httpClient.GetAsync("/environments");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task Request_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange - create a new client with invalid auth
        using var httpClient = new HttpClient 
        { 
            BaseAddress = _fixture.HttpClient.BaseAddress 
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", "invalid-key");
        
        // Act
        var response = await httpClient.GetAsync("/environments");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task Request_WithWrongAuthScheme_ReturnsUnauthorized()
    {
        // Arrange - create a new client with wrong auth scheme
        using var httpClient = new HttpClient 
        { 
            BaseAddress = _fixture.HttpClient.BaseAddress 
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-api-key");
        
        // Act
        var response = await httpClient.GetAsync("/environments");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
