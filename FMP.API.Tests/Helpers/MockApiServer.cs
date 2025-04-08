using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace FMP.API.Tests.Helpers;

public class MockApiServer : IDisposable
{
    private readonly IHost _host;
    public HttpClient Client { get; }
    public int Port { get; }

    public MockApiServer()
    {
        Port = GetAvailablePort();
        
        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseUrls($"http://localhost:{Port}")
                    .UseKestrel()
                    .Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            // Mock authentication
                            if (!context.Request.Headers.ContainsKey("Authorization") || 
                                !context.Request.Headers["Authorization"].ToString().StartsWith("ApiKey "))
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                
                                // Standard JSON serialization
                                var errorResponse = new
                                {
                                    error = new
                                    {
                                        code = "unauthorized",
                                        message = "API key is missing or invalid"
                                    }
                                };
                                
                                context.Response.ContentType = "application/json";
                                var jsonString = JsonSerializer.Serialize(errorResponse);
                                await context.Response.WriteAsync(jsonString);
                                return;
                            }
                            
                            // Add rate limit headers
                            context.Response.Headers.Add("X-RateLimit-Limit", "100");
                            context.Response.Headers.Add("X-RateLimit-Remaining", "95");
                            context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString());
                            
                            await next();
                        });
                        
                        // Configure routes to mock API behavior
                        ConfigureRoutes(app);
                    });
            })
            .Build();

        _host.Start();
        
        Client = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{Port}/v1/")
        };
        
        // Set default authentication
        Client.DefaultRequestHeaders.Add("Authorization", "ApiKey test-api-key");
    }

    private void ConfigureRoutes(IApplicationBuilder app)
    {
        // Example route setup - expand as needed for tests
        app.UseRouting();
        
        app.UseEndpoints(endpoints =>
        {
            // Feature flags endpoints
            endpoints.MapGet("/v1/flags", async context =>
            {
                // Fix JSON response using standard serialization
                var response = new
                {
                    flags = new[]
                    {
                        new
                        {
                            id = "flag-123",
                            key = "test-flag",
                            name = "Test Flag",
                            description = "Flag for testing",
                            created_at = DateTime.UtcNow.AddDays(-1),
                            updated_at = DateTime.UtcNow,
                            state = new Dictionary<string, bool>
                            {
                                { "dev", true },
                                { "staging", true },
                                { "production", false }
                            },
                            tags = new[] { "test", "example" }
                        }
                    },
                    total = 1,
                    limit = 20,
                    offset = 0
                };
                
                context.Response.ContentType = "application/json";
                var jsonString = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(jsonString);
            });
            
            // Add more mock endpoints as needed for specific tests
        });
    }

    private int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
    }
}
