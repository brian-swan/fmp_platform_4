using System.Net;
using FluentAssertions;
using FMP.API.Tests.Fixtures;
using FMP.API.Tests.Helpers;
using FMP.API.Tests.Models;
using Xunit;

namespace FMP.API.Tests.TargetingRules;

public class TargetingRuleTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public TargetingRuleTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddTargetingRule_WithValidData_ReturnsCreatedRule()
    {
        // Arrange
        string flagId = "flag-123"; // This should be a valid ID in your test environment
        var ruleRequest = TestDataGenerator.GenerateTargetingRuleCreateRequest();
        var content = _fixture.CreateJsonContent(ruleRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync($"/flags/{flagId}/rules", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await _fixture.DeserializeResponseAsync<TargetingRule>(response);
        result.Should().NotBeNull();
        result!.Type.Should().Be(ruleRequest.Type);
        result.Attribute.Should().Be(ruleRequest.Attribute);
        result.Operator.Should().Be(ruleRequest.Operator);
        result.Values.Should().BeEquivalentTo(ruleRequest.Values);
        result.Environment.Should().Be(ruleRequest.Environment);
    }

    [Fact]
    public async Task AddTargetingRule_WithInvalidFlagId_ReturnsNotFound()
    {
        // Arrange
        string invalidFlagId = "non-existent-flag";
        var ruleRequest = TestDataGenerator.GenerateTargetingRuleCreateRequest();
        var content = _fixture.CreateJsonContent(ruleRequest);
        
        // Act
        var response = await _fixture.HttpClient.PostAsync($"/flags/{invalidFlagId}/rules", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTargetingRule_WithValidIds_ReturnsNoContent()
    {
        // Arrange
        // First, we need to create a targeting rule
        string flagId = "flag-123"; // This should be a valid ID in your test environment
        var ruleRequest = TestDataGenerator.GenerateTargetingRuleCreateRequest();
        var content = _fixture.CreateJsonContent(ruleRequest);
        var createResponse = await _fixture.HttpClient.PostAsync($"/flags/{flagId}/rules", content);
        var createdRule = await _fixture.DeserializeResponseAsync<TargetingRule>(createResponse);
        //var f = await _fixture.HttpClient.GetAsync($"/flags/flag-123");
        //Console.WriteLine(f.ToString());

        // Act
        var response = await _fixture.HttpClient.DeleteAsync($"/flags/{flagId}/rules/{createdRule!.Id}");
        Console.WriteLine(createdRule.Id);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTargetingRule_WithInvalidRuleId_ReturnsNotFound()
    {
        // Arrange
        string flagId = "flag-123"; // This should be a valid ID in your test environment
        string invalidRuleId = "non-existent-rule";
       
        // Act
        var response = await _fixture.HttpClient.DeleteAsync($"/flags/{flagId}/rules/{invalidRuleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
