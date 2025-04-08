using System.Net;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using FMP.API.Tests.Helpers;
using FMP.API.Tests.Models;
using Xunit;

namespace FMP.API.Tests.Analytics;

public class AnalyticsTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public AnalyticsTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordFlagExposure_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var exposureRequest = TestDataGenerator.GenerateExposureRequest();
        var content = _fixture.CreateJsonContent(exposureRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/analytics/exposure", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RecordFlagExposure_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var exposureRequest = new ExposureRequest(); // Empty request
        var content = _fixture.CreateJsonContent(exposureRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/analytics/exposure", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetFlagUsageStats_WithValidParams_ReturnsStats()
    {
        // Arrange
        string flagId = "flag-123"; // This should be a valid ID in your test environment
        string environment = "production";
        string period = "7d";
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/analytics/flags/{flagId}/stats?environment={environment}&period={period}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<FlagStatsResponse>(response);
        result.Should().NotBeNull();
        result!.FlagId.Should().Be(flagId);
        result.Environment.Should().Be(environment);
        result.Period.Should().Be(period);
        result.Exposures.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFlagUsageStats_WithInvalidFlagId_ReturnsNotFound()
    {
        // Arrange
        string invalidFlagId = "non-existent-flag";
        string environment = "production";
        string period = "7d";
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/analytics/flags/{invalidFlagId}/stats?environment={environment}&period={period}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFlagUsageStats_WithInvalidPeriod_ReturnsBadRequest()
    {
        // Arrange
        string flagId = "flag-123"; // This should be a valid ID in your test environment
        string environment = "production";
        string invalidPeriod = "invalid";
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/analytics/flags/{flagId}/stats?environment={environment}&period={invalidPeriod}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
