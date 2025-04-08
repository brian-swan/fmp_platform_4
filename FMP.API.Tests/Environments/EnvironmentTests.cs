using System.Net;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using FMP.API.Tests.Helpers;
using FMP.API.Tests.Models;
using Xunit;

namespace FMP.API.Tests.Environments;

public class EnvironmentTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public EnvironmentTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetEnvironments_ReturnsSuccessStatusAndEnvironments()
    {
        // Act
        var response = await _fixture.HttpClient.GetAsync("/environments");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<EnvironmentListResponse>(response);
        result.Should().NotBeNull();
        result!.Environments.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEnvironment_WithValidData_ReturnsCreatedEnvironment()
    {
        // Arrange
        var createRequest = TestDataGenerator.GenerateEnvironmentCreateRequest();
        var content = _fixture.CreateJsonContent(createRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/environments", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await _fixture.DeserializeResponseAsync<EnvironmentConfig>(response);
        result.Should().NotBeNull();
        result!.Key.Should().Be(createRequest.Key);
        result.Name.Should().Be(createRequest.Name);
        result.Description.Should().Be(createRequest.Description);
    }

    [Fact]
    public async Task CreateEnvironment_WithDuplicateKey_ReturnsConflict()
    {
        // Arrange
        // First, create an environment
        var createRequest = TestDataGenerator.GenerateEnvironmentCreateRequest();
        var content = _fixture.CreateJsonContent(createRequest);
        await _fixture.HttpClient.PostAsync("/environments", content);
        
        // Try to create another environment with the same key
        content = _fixture.CreateJsonContent(createRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/environments", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var errorResponse = await _fixture.DeserializeResponseAsync<ErrorResponse>(response);
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("invalid_request");
    }

    [Fact]
    public async Task DeleteEnvironment_WithValidId_ReturnsNoContent()
    {
        // Arrange
        // First, create an environment
        var createRequest = TestDataGenerator.GenerateEnvironmentCreateRequest();
        var createContent = _fixture.CreateJsonContent(createRequest);
        var createResponse = await _fixture.HttpClient.PostAsync("/environments", createContent);
        var createdEnvironment = await _fixture.DeserializeResponseAsync<EnvironmentConfig>(createResponse);
        
        // Act
        var response = await _fixture.HttpClient.DeleteAsync($"/environments/{createdEnvironment!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteEnvironment_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        string invalidEnvironmentId = "non-existent-env";
        
        // Act
        var response = await _fixture.HttpClient.DeleteAsync($"/environments/{invalidEnvironmentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
