using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FMP.API.Tests.Helpers;

namespace FMP.API.Tests.Fixtures;

public class ApiTestFixture : IDisposable
{
    private readonly MockApiServer _mockServer;
    private const string ApiKey = "test-api-key";
    
    public HttpClient HttpClient { get; }
    public JsonSerializerOptions JsonOptions { get; }

    public ApiTestFixture()
    {
        // Create and use a mock server instead of connecting to a remote API
        _mockServer = new MockApiServer();
        HttpClient = _mockServer.Client;
        
        // Set default headers for all requests
        HttpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("ApiKey", ApiKey);
        
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public void Dispose()
    {
        _mockServer.Dispose();
    }

    public StringContent CreateJsonContent<T>(T data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return content;
    }

    public async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        // Remove the success status code check to allow deserializing error responses
        string content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
            return default;
            
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }
}
