using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using FMP.API.Tests.Models;

namespace FMP.API.Tests.Helpers;

public class MockApiServer : IDisposable
{
    private readonly IHost _host;
    private readonly Dictionary<string, FeatureFlag> _flags = new();
    private readonly Dictionary<string, TargetingRule> _rules = new();
    private readonly Dictionary<string, EnvironmentConfig> _environments = new();
    private readonly JsonSerializerOptions _jsonOptions;
    
    public HttpClient Client { get; }
    public int Port { get; }

    public MockApiServer()
    {
        Port = GetAvailablePort();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        
        // Add default environments - needed for tests
        var defaultEnvironments = new[]
        {
            new EnvironmentConfig 
            { 
                Id = "env-dev", 
                Key = "dev", 
                Name = "Development", 
                Description = "Development environment", 
                CreatedAt = DateTime.UtcNow.AddDays(-30) 
            },
            new EnvironmentConfig 
            { 
                Id = "env-staging", 
                Key = "staging", 
                Name = "Staging", 
                Description = "Staging environment", 
                CreatedAt = DateTime.UtcNow.AddDays(-30) 
            },
            new EnvironmentConfig 
            { 
                Id = "env-production", 
                Key = "production", 
                Name = "Production", 
                Description = "Production environment", 
                CreatedAt = DateTime.UtcNow.AddDays(-30) 
            }
        };
        
        foreach (var env in defaultEnvironments)
        {
            _environments[env.Id] = env;
        }
        
        // Add a test flag
        var testFlag = new FeatureFlag
        {
            Id = "flag-123",
            Key = "test-flag",
            Name = "Test Flag",
            Description = "Flag for testing",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            State = new Dictionary<string, bool>
            {
                { "dev", true },
                { "staging", true },
                { "production", false }
            },
            Tags = new List<string> { "test", "example" }
        };
        _flags[testFlag.Id] = testFlag;
        
        // Create a new service collection and register the routing services
        var services = new ServiceCollection();
        services.AddRouting();
        
        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseUrls($"http://localhost:{Port}")
                    .UseKestrel()
                    .ConfigureServices(serviceCollection => 
                    {
                        // Copy all services from our pre-configured collection
                        foreach (var service in services)
                        {
                            serviceCollection.Add(service);
                        }
                    })
                    .Configure(app =>
                    {
                        // Enable buffering so request body can be read multiple times
                        app.Use(async (context, next) =>
                        {
                            context.Request.EnableBuffering();
                            await next();
                        });
                        
                        app.Use(async (context, next) =>
                        {
                            // Mock authentication
                            if (!context.Request.Headers.ContainsKey("Authorization") || 
                                !context.Request.Headers["Authorization"].ToString().StartsWith("ApiKey "))
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                
                                var errorResponse = new
                                {
                                    error = new
                                    {
                                        code = "unauthorized",
                                        message = "API key is missing or invalid"
                                    }
                                };
                                
                                context.Response.ContentType = "application/json";
                                var jsonString = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                                await context.Response.WriteAsync(jsonString);
                                return;
                            }
                            
                            // Add rate limit headers
                            context.Response.Headers.Add("X-RateLimit-Limit", "100");
                            context.Response.Headers.Add("X-RateLimit-Remaining", "95");
                            context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString());
                            
                            await next();
                        });
                        
                        // Configure routes
                        HandleEndpoints(app);
                    });
            })
            .Build();

        _host.Start();
        
        // Set up client
        Client = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{Port}/")
        };
        
        // Set default authentication
        Client.DefaultRequestHeaders.Add("Authorization", "ApiKey test-api-key");
    }

    private async Task<T?> ReadFromJsonBodyAsync<T>(HttpContext context)
    {
        try
        {
            context.Request.Body.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(context.Request.Body, _jsonOptions);
        }
        catch (Exception)
        {
            return default;
        }
        finally
        {
            // Reset position for potential subsequent reads
            context.Request.Body.Position = 0;
        }
    }

    // New method for handling endpoints without UseRouting/UseEndpoints
    private void HandleEndpoints(IApplicationBuilder app)
    {
        // Feature flag endpoints
        app.Map("/flags", flagsApp =>
        {
            // GET /flags
            flagsApp.MapWhen(context => context.Request.Method == "GET" && !context.Request.Path.HasValue, async appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    int limit = 20;
                    int offset = 0;
                    
                    if (context.Request.Query.TryGetValue("limit", out var limitStr) && 
                        int.TryParse(limitStr, out var parsedLimit))
                    {
                        limit = parsedLimit;
                    }
                    
                    if (context.Request.Query.TryGetValue("offset", out var offsetStr) && 
                        int.TryParse(offsetStr, out var parsedOffset))
                    {
                        offset = parsedOffset;
                    }
                    
                    var response = new FeatureFlagListResponse
                    {
                        Flags = _flags.Values.Skip(offset).Take(limit).ToList(),
                        Total = _flags.Count,
                        Limit = limit,
                        Offset = offset
                    };
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
            
            // GET /flags/{id}
            flagsApp.MapWhen(context => context.Request.Method == "GET" && context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var id = context.Request.Path.Value!.TrimStart('/');
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "not_found",
                                Message = "Feature flag not found"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }
                    
                    var flag = _flags[id];
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, flag, _jsonOptions);
                });
            });
            
            // POST /flags
            flagsApp.MapWhen(context => context.Request.Method == "POST" && !context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var request = await ReadFromJsonBodyAsync<FeatureFlagCreateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    // Check for duplicate key
                    if (_flags.Values.Any(f => f.Key == request.Key))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Flag key must be unique"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }
                    
                    var newFlag = new FeatureFlag
                    {
                        Id = $"flag-{Guid.NewGuid()}",
                        Key = request.Key,
                        Name = request.Name,
                        Description = request.Description,
                        State = request.State,
                        Tags = request.Tags,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    _flags[newFlag.Id] = newFlag;
                    
                    context.Response.StatusCode = (int)HttpStatusCode.Created;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, newFlag, _jsonOptions);
                });
            });
            
            // PUT /flags/{id}
            flagsApp.MapWhen(context => context.Request.Method == "PUT" && context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var id = context.Request.Path.Value!.TrimStart('/');
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }
                    
                    var request = await ReadFromJsonBodyAsync<FeatureFlagUpdateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    var flag = _flags[id];
                    
                    if (request.Name != null)
                        flag.Name = request.Name;
                    
                    if (request.Description != null)
                        flag.Description = request.Description;
                    
                    if (request.Tags != null)
                        flag.Tags = request.Tags;
                    
                    flag.UpdatedAt = DateTime.UtcNow;
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, flag, _jsonOptions);
                });
            });
            
            // PATCH /flags/{id}/state
            flagsApp.MapWhen(context => 
                context.Request.Method == "PATCH" && 
                context.Request.Path.HasValue && 
                context.Request.Path.Value.EndsWith("/state"), 
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var pathParts = context.Request.Path.Value!.Split('/');
                    var id = pathParts[1]; // "/id/state" format
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }
                    
                    var request = await ReadFromJsonBodyAsync<FeatureFlagStateUpdateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    var flag = _flags[id];
                    flag.State[request.Environment] = request.Enabled;
                    flag.UpdatedAt = DateTime.UtcNow;
                    
                    var response = new FeatureFlagStateResponse
                    {
                        Id = flag.Id!,
                        Key = flag.Key,
                        State = flag.State,
                        UpdatedAt = flag.UpdatedAt
                    };
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
            
            // DELETE /flags/{id}
            flagsApp.MapWhen(context => context.Request.Method == "DELETE" && context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(context =>
                {
                    var id = context.Request.Path.Value!.TrimStart('/');
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return Task.CompletedTask;
                    }
                    
                    _flags.Remove(id);
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return Task.CompletedTask;
                });
            });

            // POST /flags/{flagId}/rules
            flagsApp.MapWhen(context => 
                context.Request.Method == "POST" && 
                context.Request.Path.HasValue && 
                context.Request.Path.Value.EndsWith("/rules"), 
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var pathParts = context.Request.Path.Value!.Split('/');
                    var flagId = pathParts[1]; // "/flagId/rules" format
                    
                    if (string.IsNullOrEmpty(flagId) || !_flags.ContainsKey(flagId))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }
                    
                    var request = await ReadFromJsonBodyAsync<TargetingRuleCreateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    var newRule = new TargetingRule
                    {
                        Id = $"rule-{Guid.NewGuid()}",
                        Type = request.Type,
                        Attribute = request.Attribute,
                        Operator = request.Operator,
                        Values = request.Values,
                        Environment = request.Environment,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    // Add rule to the flag
                    var flag = _flags[flagId];
                    flag.Rules ??= new List<TargetingRule>();
                    flag.Rules.Add(newRule);
                    
                    // Also store separately for easier lookup
                    _rules[newRule.Id] = newRule;
                    
                    context.Response.StatusCode = (int)HttpStatusCode.Created;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, newRule, _jsonOptions);
                });
            });
            
            // DELETE /flags/{flagId}/rules/{ruleId}
            flagsApp.MapWhen(context => 
                context.Request.Method == "DELETE" && 
                context.Request.Path.HasValue && 
                context.Request.Path.Value.Contains("/rules/"), 
                appBuilder =>
            {
                appBuilder.Run(context =>
                {
                    var pathParts = context.Request.Path.Value!.Split('/');
                    var flagId = pathParts[1]; // "/flagId/rules/ruleId" format
                    var ruleId = pathParts[3];
                    
                    if (string.IsNullOrEmpty(flagId) || !_flags.ContainsKey(flagId) ||
                        string.IsNullOrEmpty(ruleId) || !_rules.ContainsKey(ruleId))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return Task.CompletedTask;
                    }
                    
                    // Remove rule from the flag
                    var flag = _flags[flagId];
                    if (flag.Rules != null)
                    {
                        flag.Rules.RemoveAll(r => r.Id == ruleId);
                    }
                    
                    // Remove from rules dictionary
                    _rules.Remove(ruleId);
                    
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return Task.CompletedTask;
                });
            });
        });
        
        // Also map to v1/flags for consistent API paths
        app.Map("/v1/flags", flagsApp => 
        {
            // GET /v1/flags
            flagsApp.MapWhen(context => context.Request.Method == "GET" && !context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    int limit = 20;
                    int offset = 0;
                    
                    if (context.Request.Query.TryGetValue("limit", out var limitStr) && 
                        int.TryParse(limitStr, out var parsedLimit))
                    {
                        limit = parsedLimit;
                    }
                    
                    if (context.Request.Query.TryGetValue("offset", out var offsetStr) && 
                        int.TryParse(offsetStr, out var parsedOffset))
                    {
                        offset = parsedOffset;
                    }
                    
                    var response = new FeatureFlagListResponse
                    {
                        Flags = _flags.Values.Skip(offset).Take(limit).ToList(),
                        Total = _flags.Count,
                        Limit = limit,
                        Offset = offset
                    };
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
            
            // GET /v1/flags/{id}
            flagsApp.MapWhen(context => context.Request.Method == "GET" && context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var id = context.Request.Path.Value!.TrimStart('/');
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "not_found",
                                Message = "Feature flag not found"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }
                    
                    var flag = _flags[id];
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, flag, _jsonOptions);
                });
            });
            
            // POST /v1/flags
            flagsApp.MapWhen(context => context.Request.Method == "POST" && !context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var request = await ReadFromJsonBodyAsync<FeatureFlagCreateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    // Check for duplicate key
                    if (_flags.Values.Any(f => f.Key == request.Key))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Flag key must be unique"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }
                    
                    var newFlag = new FeatureFlag
                    {
                        Id = $"flag-{Guid.NewGuid()}",
                        Key = request.Key,
                        Name = request.Name,
                        Description = request.Description,
                        State = request.State,
                        Tags = request.Tags,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    _flags[newFlag.Id] = newFlag;
                    
                    context.Response.StatusCode = (int)HttpStatusCode.Created;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, newFlag, _jsonOptions);
                });
            });
            
            // PUT /v1/flags/{id}
            flagsApp.MapWhen(context => context.Request.Method == "PUT" && context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var id = context.Request.Path.Value!.TrimStart('/');
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }
                    
                    var request = await ReadFromJsonBodyAsync<FeatureFlagUpdateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    var flag = _flags[id];
                    
                    if (request.Name != null)
                        flag.Name = request.Name;
                    
                    if (request.Description != null)
                        flag.Description = request.Description;
                    
                    if (request.Tags != null)
                        flag.Tags = request.Tags;
                    
                    flag.UpdatedAt = DateTime.UtcNow;
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, flag, _jsonOptions);
                });
            });
            
            // PATCH /v1/flags/{id}/state
            flagsApp.MapWhen(context => 
                context.Request.Method == "PATCH" && 
                context.Request.Path.HasValue && 
                context.Request.Path.Value.EndsWith("/state"), 
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var pathParts = context.Request.Path.Value!.Split('/');
                    var id = pathParts[1]; // "/id/state" format
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }
                    
                    var request = await ReadFromJsonBodyAsync<FeatureFlagStateUpdateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    var flag = _flags[id];
                    flag.State[request.Environment] = request.Enabled;
                    flag.UpdatedAt = DateTime.UtcNow;
                    
                    var response = new FeatureFlagStateResponse
                    {
                        Id = flag.Id!,
                        Key = flag.Key,
                        State = flag.State,
                        UpdatedAt = flag.UpdatedAt
                    };
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
            
            // DELETE /v1/flags/{id}
            flagsApp.MapWhen(context => context.Request.Method == "DELETE" && context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(context =>
                {
                    var id = context.Request.Path.Value!.TrimStart('/');
                    
                    if (string.IsNullOrEmpty(id) || !_flags.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return Task.CompletedTask;
                    }
                    
                    _flags.Remove(id);
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return Task.CompletedTask;
                });
            });
            
            // POST /v1/flags/{flagId}/rules
            flagsApp.MapWhen(context => 
                context.Request.Method == "POST" && 
                context.Request.Path.HasValue && 
                context.Request.Path.Value.EndsWith("/rules"), 
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var pathParts = context.Request.Path.Value!.Split('/');
                    var flagId = pathParts[1]; // "/flagId/rules" format
                    
                    if (string.IsNullOrEmpty(flagId) || !_flags.ContainsKey(flagId))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }
                    
                    var request = await ReadFromJsonBodyAsync<TargetingRuleCreateRequest>(context);
                    
                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    
                    var newRule = new TargetingRule
                    {
                        Id = $"rule-{Guid.NewGuid()}",
                        Type = request.Type,
                        Attribute = request.Attribute,
                        Operator = request.Operator,
                        Values = request.Values,
                        Environment = request.Environment,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    // Add rule to the flag
                    var flag = _flags[flagId];
                    flag.Rules ??= new List<TargetingRule>();
                    flag.Rules.Add(newRule);
                    
                    // Also store separately for easier lookup
                    _rules[newRule.Id] = newRule;
                    
                    context.Response.StatusCode = (int)HttpStatusCode.Created;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, newRule, _jsonOptions);
                });
            });
            
            // DELETE /v1/flags/{flagId}/rules/{ruleId}
            flagsApp.MapWhen(context => 
                context.Request.Method == "DELETE" && 
                context.Request.Path.HasValue && 
                context.Request.Path.Value.Contains("/rules/"), 
                appBuilder =>
            {
                appBuilder.Run(context =>
                {
                    var pathParts = context.Request.Path.Value!.Split('/');
                    var flagId = pathParts[1]; // "/flagId/rules/ruleId" format
                    var ruleId = pathParts[3];
                    
                    if (string.IsNullOrEmpty(flagId) || !_flags.ContainsKey(flagId) ||
                        string.IsNullOrEmpty(ruleId) || !_rules.ContainsKey(ruleId))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return Task.CompletedTask;
                    }
                    
                    // Remove rule from the flag
                    var flag = _flags[flagId];
                    if (flag.Rules != null)
                    {
                        flag.Rules.RemoveAll(r => r.Id == ruleId);
                    }
                    
                    // Remove from rules dictionary
                    _rules.Remove(ruleId);
                    
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return Task.CompletedTask;
                });
            });
        });
        
        // Map environments endpoints (simplified)
        app.Map("/environments", envApp =>
        {
            envApp.MapWhen(context => context.Request.Method == "GET" && !context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var response = new EnvironmentListResponse
                    {
                        Environments = _environments.Values.ToList()
                    };
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
        });
        
        app.Map("/v1/environments", envApp =>
        {
            envApp.MapWhen(context => context.Request.Method == "GET" && !context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var response = new EnvironmentListResponse
                    {
                        Environments = _environments.Values.ToList()
                    };
                    
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
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
