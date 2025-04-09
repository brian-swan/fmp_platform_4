using System.Text.Json;
using FMP.API.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace FMP.API.Middleware;

public class RateLimitOptions
{
    public int LimitPerMinute { get; set; } = 100;
    public bool EnableRateLimiting { get; set; } = true;
}

public interface IRateLimitService
{
    bool IsRateLimited(string clientId, out int remaining, out DateTimeOffset reset);
}

public class RateLimitService : IRateLimitService
{
    private readonly IMemoryCache _cache;
    private readonly RateLimitOptions _options;
    
    public RateLimitService(IMemoryCache cache, IOptions<RateLimitOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }
    
    public bool IsRateLimited(string clientId, out int remaining, out DateTimeOffset reset)
    {
        if (!_options.EnableRateLimiting)
        {
            remaining = _options.LimitPerMinute;
            reset = DateTimeOffset.UtcNow.AddMinutes(1);
            return false;
        }
        
        var key = $"ratelimit:{clientId}";
        var resetTime = DateTimeOffset.UtcNow.AddMinutes(1).Truncate(TimeSpan.FromMinutes(1));
        
        _cache.TryGetValue<RateLimitInfo>(key, out var rateLimitInfo);
        
        if (rateLimitInfo == null || rateLimitInfo.Reset < DateTimeOffset.UtcNow)
        {
            rateLimitInfo = new RateLimitInfo
            {
                Count = 0,
                Reset = resetTime
            };
        }
        
        rateLimitInfo.Count++;
        
        // Update the cache
        _cache.Set(key, rateLimitInfo, resetTime);
        
        remaining = Math.Max(0, _options.LimitPerMinute - rateLimitInfo.Count);
        reset = rateLimitInfo.Reset;
        
        return rateLimitInfo.Count > _options.LimitPerMinute;
    }
    
    private class RateLimitInfo
    {
        public int Count { get; set; }
        public DateTimeOffset Reset { get; set; }
    }
}

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;
    private readonly RateLimitOptions _options;
    
    public RateLimitMiddleware(RequestDelegate next, IRateLimitService rateLimitService, IOptions<RateLimitOptions> options)
    {
        _next = next;
        _rateLimitService = rateLimitService;
        _options = options.Value;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for Swagger endpoints
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }
        
        // Use API key as client ID
        string clientId = "anonymous";
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerValue = authHeader.ToString();
            if (headerValue.StartsWith("ApiKey ") && headerValue.Length > 7)
            {
                clientId = headerValue.Substring(7).Trim();
            }
        }
        
        // Check if client is rate limited
        bool isRateLimited = _rateLimitService.IsRateLimited(clientId, out int remaining, out DateTimeOffset reset);
        
        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = _options.LimitPerMinute.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = reset.ToUnixTimeSeconds().ToString();
        
        if (isRateLimited)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";
            
            var error = new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Code = "rate_limit_exceeded",
                    Message = "Rate limit exceeded. Try again later."
                }
            };
            
            var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await context.Response.WriteAsync(json);
            
            return;
        }
        
        await _next(context);
    }
}

public static class DateTimeOffsetExtensions
{
    public static DateTimeOffset Truncate(this DateTimeOffset dateTimeOffset, TimeSpan timeSpan)
    {
        return new DateTimeOffset(
            dateTimeOffset.UtcTicks - (dateTimeOffset.UtcTicks % timeSpan.Ticks),
            TimeSpan.Zero);
    }
}
