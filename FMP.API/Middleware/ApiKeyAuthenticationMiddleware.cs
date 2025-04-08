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
        // Skip auth for Swagger endpoints
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }
        
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            await WriteUnauthorizedResponse(context, "Missing Authorization header");
            return;
        }
        
        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("ApiKey ") || headerValue.Length <= 7)
        {
            await WriteUnauthorizedResponse(context, "Invalid Authorization header format");
            return;
        }
        
        var apiKey = headerValue.Substring(7).Trim();
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
