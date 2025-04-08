using System.Net;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using FMP.API.Tests.Helpers;
using FMP.API.Tests.Models;
using Xunit;

namespace FMP.API.Tests.FeatureFlags;

public class FeatureFlagTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public FeatureFlagTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetFeatureFlags_ReturnsSuccessStatus()
    {
        // Act
        var response = await _fixture.HttpClient.GetAsync("/flags");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<FeatureFlagListResponse>(response);
        result.Should().NotBeNull();
        result!.Flags.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFeatureFlags_WithPagination_ReturnsCorrectPageSize()
    {
        // Arrange
        int limit = 5;
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/flags?limit={limit}&offset=0");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<FeatureFlagListResponse>(response);
        result.Should().NotBeNull();
        result!.Limit.Should().Be(limit);
        result.Flags.Count.Should().BeLessThanOrEqualTo(limit);
    }

    [Fact]
    public async Task GetFeatureFlag_WithValidId_ReturnsFeatureFlag()
    {
        // Arrange
        string flagId = "flag-123"; // This should be a valid ID in your test environment
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/flags/{flagId}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<FeatureFlag>(response);
        result.Should().NotBeNull();
        result!.Id.Should().Be(flagId);
    }

    [Fact]
    public async Task GetFeatureFlag_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        string invalidFlagId = "non-existent-flag";
        
        // Act
        var response = await _fixture.HttpClient.GetAsync($"/flags/{invalidFlagId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateFeatureFlag_WithValidData_ReturnsCreatedFeatureFlag()
    {
        // Arrange
        var createRequest = TestDataGenerator.GenerateFeatureFlagCreateRequest();
        var content = _fixture.CreateJsonContent(createRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/flags", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await _fixture.DeserializeResponseAsync<FeatureFlag>(response);
        result.Should().NotBeNull();
        result!.Key.Should().Be(createRequest.Key);
        result.Name.Should().Be(createRequest.Name);
        result.Description.Should().Be(createRequest.Description);
        result.State.Should().BeEquivalentTo(createRequest.State);
        result.Tags.Should().BeEquivalentTo(createRequest.Tags);
    }

    [Fact]
    public async Task CreateFeatureFlag_WithDuplicateKey_ReturnsConflict()
    {
        // Arrange
        // First, create a feature flag
        var createRequest = TestDataGenerator.GenerateFeatureFlagCreateRequest();
        var content = _fixture.CreateJsonContent(createRequest);
        await _fixture.HttpClient.PostAsync("/flags", content);
        
        // Try to create another feature flag with the same key
        content = _fixture.CreateJsonContent(createRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync("/flags", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var errorResponse = await _fixture.DeserializeResponseAsync<ErrorResponse>(response);
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Code.Should().Be("invalid_request");
    }

    [Fact]
    public async Task UpdateFeatureFlag_WithValidData_ReturnsUpdatedFeatureFlag()
    {
        // Arrange
        // First, create a feature flag
        var createRequest = TestDataGenerator.GenerateFeatureFlagCreateRequest();
        var createContent = _fixture.CreateJsonContent(createRequest);
        var createResponse = await _fixture.HttpClient.PostAsync("/flags", createContent);
        var createdFlag = await _fixture.DeserializeResponseAsync<FeatureFlag>(createResponse);
        
        // Now update the feature flag
        var updateRequest = TestDataGenerator.GenerateFeatureFlagUpdateRequest();
        var updateContent = _fixture.CreateJsonContent(updateRequest);
        
        // Act
        var response = await _fixture.HttpClient.PutAsync($"/flags/{createdFlag!.Id}", updateContent);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<FeatureFlag>(response);
        result.Should().NotBeNull();
        result!.Name.Should().Be(updateRequest.Name);
        result.Description.Should().Be(updateRequest.Description);
        result.Tags.Should().BeEquivalentTo(updateRequest.Tags);
    }

    [Fact]
    public async Task ToggleFeatureFlagState_ForValidEnvironment_ReturnsUpdatedState()
    {
        // Arrange
        // First, create a feature flag
        var createRequest = TestDataGenerator.GenerateFeatureFlagCreateRequest();
        var createContent = _fixture.CreateJsonContent(createRequest);
        var createResponse = await _fixture.HttpClient.PostAsync("/flags", createContent);
        var createdFlag = await _fixture.DeserializeResponseAsync<FeatureFlag>(createResponse);
        
        // Now toggle the state
        var toggleRequest = new FeatureFlagStateUpdateRequest
        {
            Environment = "staging",
            Enabled = true
        };
        var toggleContent = _fixture.CreateJsonContent(toggleRequest);
        
        // Act
        var response = await _fixture.HttpClient.PatchAsync($"/flags/{createdFlag!.Id}/state", toggleContent);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var result = await _fixture.DeserializeResponseAsync<FeatureFlagStateResponse>(response);
        result.Should().NotBeNull();
        result!.Id.Should().Be(createdFlag.Id);
        result.State["staging"].Should().BeTrue();
    }

    [Fact]
    public async Task DeleteFeatureFlag_WithValidId_ReturnsNoContent()
    {
        // Arrange
        // First, create a feature flag
        var createRequest = TestDataGenerator.GenerateFeatureFlagCreateRequest();
        var createContent = _fixture.CreateJsonContent(createRequest);
        var createResponse = await _fixture.HttpClient.PostAsync("/flags", createContent);
        var createdFlag = await _fixture.DeserializeResponseAsync<FeatureFlag>(createResponse);
        
        // Act
        var response = await _fixture.HttpClient.DeleteAsync($"/flags/{createdFlag!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Verify the flag is gone
        var getResponse = await _fixture.HttpClient.GetAsync($"/flags/{createdFlag.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
