using System.Net;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using FMP.API.Tests.Helpers;
using FMP.API.Tests.Models;
using Xunit;

namespace FMP.API.Tests.SdkConfiguration;

public class SdkConfigurationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public SdkConfigurationTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetClientSdkConfiguration_ForValidEnvironment_ReturnsConfiguration()
    {
        // Arrange
        string environment = "production";
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/sdk/config?environment={environment}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<Models.SdkConfiguration>(response);
        result.Should().NotBeNull();
        result!.Environment.Should().Be(environment);
        result.Flags.Should().NotBeNull();
    }

    [Fact]
    public async Task GetClientSdkConfiguration_ForInvalidEnvironment_ReturnsBadRequest()
    {
        // Arrange
        string invalidEnvironment = "non-existent-env";
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/sdk/config?environment={invalidEnvironment}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EvaluateFlagsForUser_WithValidData_ReturnsEvaluatedFlags()
    {
        // Arrange
        var evaluationRequest = TestDataGenerator.GenerateEvaluationRequest();
        var content = _fixture.CreateJsonContent(evaluationRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/sdk/evaluate", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<SdkEvaluationResponse>(response);
        result.Should().NotBeNull();
        result!.Environment.Should().Be(evaluationRequest.Environment);
        result.Flags.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateFlagsForUser_WithInvalidEnvironment_ReturnsBadRequest()
    {
        // Arrange
        var evaluationRequest = TestDataGenerator.GenerateEvaluationRequest();
        evaluationRequest.Environment = "non-existent-env";
        var content = _fixture.CreateJsonContent(evaluationRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/sdk/evaluate", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
