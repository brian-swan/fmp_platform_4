using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FMP.API.Tests.Helpers;

namespace FMP.API.Tests.Fixtures;

public class ApiTestFixture : IDisposable
{
    private const string BaseUrl = "https://api.featureflag.example/v1";
    private const string ApiKey = "test-api-key";
    
    public HttpClient HttpClient { get; }
    public JsonSerializerOptions JsonOptions { get; }

    public ApiTestFixture()
    {
        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };
        
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
        HttpClient.Dispose();
    }

    public StringContent CreateJsonContent<T>(T data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return content;
    }

    public async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return default;
            
        string content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
            return default;
            
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }
}
