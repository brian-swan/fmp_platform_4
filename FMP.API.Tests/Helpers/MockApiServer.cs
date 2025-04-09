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
        ;

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

                            // Check if the API key is valid (for invalid key tests)
                            var headerValue = context.Request.Headers["Authorization"].ToString();
                            var apiKey = headerValue.Substring(7).Trim(); // Remove "ApiKey " prefix

                            if (apiKey != "test-api-key" && apiKey != "valid-key")
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

                        // DELETE /flags/{flagId}/rules/{ruleId}
                        app.Use(async (context, next) =>
                        {
                            if (context.Request.Method == "DELETE" && 
                                context.Request.Path.HasValue && 
                                context.Request.Path.Value.Contains("/rules/"))
                            {
                                Console.WriteLine("*** DELETE Rules Middleware Handler ***");
                                
                                // Parse the flag ID and rule ID from the path
                                var pathSegments = context.Request.Path.Value!.Trim('/').Split('/');
                                
                                if (pathSegments.Length < 4)
                                {
                                    Console.WriteLine($"Path segments length: {pathSegments.Length}");
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                    return;
                                }
                                
                                var flagId = pathSegments[1];
                                var ruleId = pathSegments[3];
                                Console.WriteLine($"Flag ID: {flagId}, Rule ID: {ruleId}");
                                
                                // Check if the flag exists
                                if (!_flags.ContainsKey(flagId))
                                {
                                    Console.WriteLine("Flag not found");
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                    return;
                                }
                                
                                // Check if the rule exists
                                if (!_rules.ContainsKey(ruleId))
                                {
                                    Console.WriteLine("Rule not found");
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                    return;
                                }
                                
                                Console.WriteLine("Flag and rule found - deleting rule");
                                
                                // Remove rule from flag
                                var flag = _flags[flagId];
                                if (flag.Rules != null)
                                {
                                    flag.Rules.RemoveAll(r => r.Id == ruleId);
                                }
                                
                                // Remove rule from dictionary
                                _rules.Remove(ruleId);
                                
                                // Return success
                                Console.WriteLine("Returning NoContent (204)");
                                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                                return;
                            }
                            
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

    private void HandleEndpoints(IApplicationBuilder app)
    {
        // Add SDK endpoints
        app.Map("/sdk", sdkApp =>
        {
            // GET /sdk/config
            sdkApp.MapWhen(context =>
                context.Request.Method == "GET" &&
                context.Request.Path.Value == "/config",
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    // Get environment parameter
                    string environment = context.Request.Query["environment"];

                    if (string.IsNullOrEmpty(environment) ||
                        !_environments.Values.Any(e => e.Key == environment))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = $"Environment '{environment}' does not exist"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    // Create flag dictionary for this environment
                    var flagStates = _flags.Values
                        .Where(f => f.State.ContainsKey(environment))
                        .ToDictionary(f => f.Key, f => f.State[environment]);

                    var config = new Models.SdkConfiguration
                    {
                        Environment = environment,
                        Flags = flagStates,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, config, _jsonOptions);
                });
            });

            // POST /sdk/evaluate
            sdkApp.MapWhen(context =>
                context.Request.Method == "POST" &&
                context.Request.Path.Value == "/evaluate",
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var request = await ReadFromJsonBodyAsync<SdkEvaluationRequest>(context);

                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    // Validate environment
                    if (string.IsNullOrEmpty(request.Environment) ||
                        !_environments.Values.Any(e => e.Key == request.Environment))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = $"Environment '{request.Environment}' does not exist"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    // Create flag dictionary with basic state and rule evaluation
                    var flagStates = new Dictionary<string, bool>();

                    foreach (var flag in _flags.Values)
                    {
                        if (!flag.State.ContainsKey(request.Environment))
                        {
                            continue;
                        }

                        // Start with the default state for the environment
                        bool isEnabled = flag.State[request.Environment];

                        // Apply targeting rules if any
                        if (flag.Rules != null && flag.Rules.Count > 0)
                        {
                            // Only consider rules for the requested environment
                            var environmentRules = flag.Rules.Where(r => r.Environment == request.Environment).ToList();

                            foreach (var rule in environmentRules)
                            {
                                // Very basic rule evaluation - in real implementation this would be more complex
                                // For this mock, we'll just enable the flag for beta-testers group
                                if (rule.Type == "user" && rule.Attribute == "email" &&
                                    request.User.Groups.Contains("beta-testers"))
                                {
                                    isEnabled = true;
                                    break;
                                }
                            }
                        }

                        flagStates[flag.Key] = isEnabled;
                    }

                    var response = new SdkEvaluationResponse
                    {
                        Environment = request.Environment,
                        Flags = flagStates,
                        EvaluatedAt = DateTime.UtcNow
                    };

                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
        });

        // Also map to v1/sdk endpoints
        app.Map("/v1/sdk", sdkApp =>
        {
            // GET /v1/sdk/config
            sdkApp.MapWhen(context =>
                context.Request.Method == "GET" &&
                context.Request.Path.Value == "/config",
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    // Same implementation as /sdk/config
                    // Get environment parameter
                    string environment = context.Request.Query["environment"];

                    if (string.IsNullOrEmpty(environment) ||
                        !_environments.Values.Any(e => e.Key == environment))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = $"Environment '{environment}' does not exist"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    // Create flag dictionary for this environment
                    var flagStates = _flags.Values
                        .Where(f => f.State.ContainsKey(environment))
                        .ToDictionary(f => f.Key, f => f.State[environment]);

                    var config = new Models.SdkConfiguration
                    {
                        Environment = environment,
                        Flags = flagStates,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, config, _jsonOptions);
                });
            });

            // POST /v1/sdk/evaluate
            sdkApp.MapWhen(context =>
                context.Request.Method == "POST" &&
                context.Request.Path.Value == "/evaluate",
                appBuilder =>
            {
                // Same implementation as /sdk/evaluate
                // ... Existing implementation for evaluate endpoint ...
                appBuilder.Run(async context =>
                {
                    var request = await ReadFromJsonBodyAsync<SdkEvaluationRequest>(context);

                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    // Validate environment
                    if (string.IsNullOrEmpty(request.Environment) ||
                        !_environments.Values.Any(e => e.Key == request.Environment))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = $"Environment '{request.Environment}' does not exist"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    // Create flag dictionary with basic state and rule evaluation
                    var flagStates = new Dictionary<string, bool>();

                    foreach (var flag in _flags.Values)
                    {
                        if (!flag.State.ContainsKey(request.Environment))
                        {
                            continue;
                        }

                        // Start with the default state for the environment
                        bool isEnabled = flag.State[request.Environment];

                        // Apply targeting rules if any
                        if (flag.Rules != null && flag.Rules.Count > 0)
                        {
                            // Only consider rules for the requested environment
                            var environmentRules = flag.Rules.Where(r => r.Environment == request.Environment).ToList();

                            foreach (var rule in environmentRules)
                            {
                                // Very basic rule evaluation - in real implementation this would be more complex
                                // For this mock, we'll just enable the flag for beta-testers group
                                if (rule.Type == "user" && rule.Attribute == "email" &&
                                    request.User.Groups.Contains("beta-testers"))
                                {
                                    isEnabled = true;
                                    break;
                                }
                            }
                        }

                        flagStates[flag.Key] = isEnabled;
                    }

                    var response = new SdkEvaluationResponse
                    {
                        Environment = request.Environment,
                        Flags = flagStates,
                        EvaluatedAt = DateTime.UtcNow
                    };

                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
                });
            });
        });

        // Add analytics endpoints
        app.Map("/analytics", analyticsApp =>
        {
            // POST /analytics/exposure
            analyticsApp.MapWhen(context =>
                context.Request.Method == "POST" &&
                context.Request.Path.Value == "/exposure",
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var request = await ReadFromJsonBodyAsync<ExposureRequest>(context);

                    if (request == null ||
                        string.IsNullOrEmpty(request.FlagKey) ||
                        string.IsNullOrEmpty(request.Environment) ||
                        string.IsNullOrEmpty(request.UserId))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Required fields missing"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                });
            });

            // GET /analytics/flags/{flagId}/stats
            analyticsApp.MapWhen(context =>
                context.Request.Method == "GET" &&
                context.Request.Path.Value != null &&
                context.Request.Path.Value.StartsWith("/flags/") &&
                context.Request.Path.Value.EndsWith("/stats"),
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    // Extract flag ID from path
                    var pathParts = context.Request.Path.Value!.Split('/');
                    if (pathParts.Length < 3)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    var flagId = pathParts[2]; // "/flags/{flagId}/stats"

                    // Check if flag exists
                    if (!_flags.ContainsKey(flagId))
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

                    // Get query parameters
                    string environment = context.Request.Query["environment"];
                    string period = context.Request.Query["period"];

                    // Validate environment
                    if (string.IsNullOrEmpty(environment) ||
                        !_environments.Values.Any(e => e.Key == environment))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Invalid environment"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    // Validate period format (should be like "7d")
                    if (string.IsNullOrEmpty(period) || !period.EndsWith("d") ||
                        !int.TryParse(period.TrimEnd('d'), out int days))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Invalid period format. Use 'Nd' where N is the number of days"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    var flag = _flags[flagId];

                    // Generate mock stats
                    var stats = new FlagStatsResponse
                    {
                        FlagId = flagId,
                        FlagKey = flag.Key,
                        Environment = environment,
                        Period = period,
                        Exposures = new ExposureStats
                        {
                            Total = days * 2000,
                            Breakdown = new Dictionary<string, int>()
                        }
                    };

                    // Add daily breakdown
                    var startDate = DateTime.UtcNow.Date.AddDays(-days);
                    for (int i = 0; i < days; i++)
                    {
                        var date = startDate.AddDays(i);
                        stats.Exposures.Breakdown[date.ToString("yyyy-MM-dd")] = 2000 + new Random().Next(-200, 300);
                    }

                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, stats, _jsonOptions);
                });
            });
        });

        // Also map endpoints for v1/analytics
        app.Map("/v1/analytics", analyticsApp =>
        {
            // POST /v1/analytics/exposure
            analyticsApp.MapWhen(context =>
                context.Request.Method == "POST" &&
                context.Request.Path.Value == "/exposure",
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var request = await ReadFromJsonBodyAsync<ExposureRequest>(context);

                    if (request == null ||
                        string.IsNullOrEmpty(request.FlagKey) ||
                        string.IsNullOrEmpty(request.Environment) ||
                        string.IsNullOrEmpty(request.UserId))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Required fields missing"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                });
            });

            // GET /v1/analytics/flags/{flagId}/stats
            analyticsApp.MapWhen(context =>
                context.Request.Method == "GET" &&
                context.Request.Path.Value != null &&
                context.Request.Path.Value.StartsWith("/flags/") &&
                context.Request.Path.Value.EndsWith("/stats"),
                appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    // Same implementation as /analytics/flags/{flagId}/stats
                    // Extract flag ID from path
                    var pathParts = context.Request.Path.Value!.Split('/');
                    if (pathParts.Length < 3)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    var flagId = pathParts[2]; // "/flags/{flagId}/stats"

                    // Check if flag exists
                    if (!_flags.ContainsKey(flagId))
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

                    // Get query parameters
                    string environment = context.Request.Query["environment"];
                    string period = context.Request.Query["period"];

                    // Validate environment
                    if (string.IsNullOrEmpty(environment) ||
                        !_environments.Values.Any(e => e.Key == environment))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Invalid environment"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    // Validate period format (should be like "7d")
                    if (string.IsNullOrEmpty(period) || !period.EndsWith("d") ||
                        !int.TryParse(period.TrimEnd('d'), out int days))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Invalid period format. Use 'Nd' where N is the number of days"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    var flag = _flags[flagId];

                    // Generate mock stats
                    var stats = new FlagStatsResponse
                    {
                        FlagId = flagId,
                        FlagKey = flag.Key,
                        Environment = environment,
                        Period = period,
                        Exposures = new ExposureStats
                        {
                            Total = days * 2000,
                            Breakdown = new Dictionary<string, int>()
                        }
                    };

                    // Add daily breakdown
                    var startDate = DateTime.UtcNow.Date.AddDays(-days);
                    for (int i = 0; i < days; i++)
                    {
                        var date = startDate.AddDays(i);
                        stats.Exposures.Breakdown[date.ToString("yyyy-MM-dd")] = 2000 + new Random().Next(-200, 300);
                    }

                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, stats, _jsonOptions);
                });
            });
        });

        // Feature flag endpoints
        app.Map("/flags", flagsApp =>
        {
            // GET /flags
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
                context.Request.Path.Value.Contains("/rules/") &&
                context.Request.Path.Value.Split('/').Length >= 3,
                appBuilder =>
            {
                Console.WriteLine("*** DELETE /flags/{flagId}/rules/{ruleId} ***");
                appBuilder.Run(async context =>
                {
                    // Parse the flag ID and rule ID from the path
                    var pathSegments = context.Request.Path.Value!.Trim('/').Split('/');

                    if (pathSegments.Length < 4)
                    {
                        Console.WriteLine($"***** {pathSegments.Length} *****");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return; // Return early with 404
                    }

                    var flagId = pathSegments[1]; // First segment is the flag ID
                    var ruleId = pathSegments[3]; // Third segment is the rule ID

                    // Check if the flag exists
                    if (!_flags.ContainsKey(flagId))
                    {
                        Console.WriteLine("***** Flag not found *****");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return; // Flag doesn't exist
                    }

                    // Check if the rule exists
                    if (!_rules.ContainsKey(ruleId))
                    {
                        Console.WriteLine("***** Rule not found *****");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return; // Rule doesn't exist
                    }

                    // Remove rule from flag
                    var flag = _flags[flagId];
                    if (flag.Rules != null)
                    {
                        Console.WriteLine("***** Removing rule from flag *****");
                        flag.Rules.RemoveAll(r => r.Id == ruleId);
                    }

                    // Remove rule from dictionary
                    Console.WriteLine("***** Removing rule from dictionary *****");
                    _rules.Remove(ruleId);

                    // Return success
                    Console.WriteLine("***** Returning NoContent *****");
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;

                });
            });

            // Add a catch-all handler for DELETE requests to debug routing
            flagsApp.Use(async (context, next) =>
            {
                if (context.Request.Method == "DELETE" && context.Request.Path.Value != null && context.Request.Path.Value.Contains("/rules/"))
                {
                    Console.WriteLine("*** CATCH-ALL DELETE Handler ***");
                    Console.WriteLine($"Path: {context.Request.Path.Value}");
                    Console.WriteLine("This means the specific handler wasn't matched!");

                    // Continue processing to see if another handler matches
                    await next();
                }
                else
                {
                    await next();
                }
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
                context.Request.Path.Value!.Contains("/rules/"),
                appBuilder =>
            {
                appBuilder.Run(context =>
                {
                    Console.WriteLine("*********");
                    Console.WriteLine("V1 DELETE rules handler");
                    Console.WriteLine("*********");

                    // For rule deletion, always return 204 No Content
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return Task.CompletedTask;
                });
            });
        });

        // Map environments endpoints
        app.Map("/environments", envApp =>
        {
            // GET /environments
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

            // POST /environments
            envApp.MapWhen(context => context.Request.Method == "POST" && !context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var request = await ReadFromJsonBodyAsync<EnvironmentCreateRequest>(context);

                    if (request == null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    // Check for duplicate key
                    if (_environments.Values.Any(e => e.Key == request.Key))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        var error = new ErrorResponse
                        {
                            Error = new ErrorDetails
                            {
                                Code = "invalid_request",
                                Message = "Environment key must be unique"
                            }
                        };
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, error, _jsonOptions);
                        return;
                    }

                    var newEnv = new EnvironmentConfig
                    {
                        Id = $"env-{Guid.NewGuid()}",
                        Key = request.Key,
                        Name = request.Name,
                        Description = request.Description,
                        CreatedAt = DateTime.UtcNow
                    };

                    _environments[newEnv.Id] = newEnv;

                    context.Response.StatusCode = (int)HttpStatusCode.Created;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, newEnv, _jsonOptions);
                });
            });

            // DELETE /environments/{id}
            envApp.MapWhen(context => context.Request.Method == "DELETE" && context.Request.Path.HasValue, appBuilder =>
            {
                appBuilder.Run(context =>
                {
                    var id = context.Request.Path.Value!.TrimStart('/');

                    if (string.IsNullOrEmpty(id) || !_environments.ContainsKey(id))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return Task.CompletedTask;
                    }

                    _environments.Remove(id);
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return Task.CompletedTask;
                });
            });
        });

        // Map v1/environments endpoints
        app.Map("/v1/environments", envApp =>
        {
            // GET /v1/environments
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

            // Other environment endpoints for v1...
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
