using System.Text.Json;
using FMP.API.Models;

namespace FMP.API.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    
    public ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for Swagger
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }
        
        // Check for the API key in the header
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            await WriteUnauthorizedResponse(context, "Missing Authorization header");
            return;
        }
        
        string apiKey = null;
        var headerValue = authHeader.ToString();
        
        // Support different formats: "ApiKey KEY", "Bearer KEY", or just "KEY"
        if (headerValue.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = headerValue.Substring(7).Trim();
        }
        else if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = headerValue.Substring(7).Trim();
        }
        else
        {
            // Try using the raw value as the API key
            apiKey = headerValue.Trim();
        }
        
        if (string.IsNullOrEmpty(apiKey))
        {
            await WriteUnauthorizedResponse(context, "Invalid Authorization header format");
            return;
        }
        
        var validApiKeys = _configuration.GetSection("ApiKeys").GetChildren().ToDictionary(x => x.Key, x => x.Value);
        
        if (!validApiKeys.ContainsKey(apiKey))
        {
            await WriteUnauthorizedResponse(context, "Invalid API key");
            return;
        }
        
        await _next(context);
    }
    
    private async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        
        var error = new ErrorResponse
        {
            Error = new ErrorDetails
            {
                Code = "unauthorized",
                Message = message
            }
        };
        
        await JsonSerializer.SerializeAsync(context.Response.Body, error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
