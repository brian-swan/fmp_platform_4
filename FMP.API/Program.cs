using FMP.API.DataAccess.Interfaces;
using FMP.API.DataAccess.Cosmos;
using FMP.API.DataAccess.InMemory;
using FMP.API.Middleware;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using FMP.API.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options => 
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Replace the existing Swagger registration with our custom method
builder.Services.AddSwaggerWithApiKey();

// Conditionally register the appropriate data access services
#if DEBUG
    // In debug mode, use in-memory repositories with example data
    builder.Services.AddSingleton<IFeatureFlagRepository, InMemoryFeatureFlagRepository>();
    builder.Services.AddSingleton<IEnvironmentRepository, InMemoryEnvironmentRepository>();
    builder.Services.AddSingleton<IAnalyticsRepository, InMemoryAnalyticsRepository>();
    builder.Services.AddSingleton<IExampleDataSeeder, ExampleDataSeeder>();
#else
    // In non-debug mode, use Cosmos DB repositories
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb");
    var databaseName = builder.Configuration["CosmosDb:DatabaseName"];
    
    builder.Services.AddSingleton<CosmosDbContext>(sp => 
        new CosmosDbContext(cosmosConnectionString, databaseName));
    
    builder.Services.AddScoped<IFeatureFlagRepository, CosmosFeatureFlagRepository>();
    builder.Services.AddScoped<IEnvironmentRepository, CosmosEnvironmentRepository>();
    builder.Services.AddScoped<IAnalyticsRepository, CosmosAnalyticsRepository>();
#endif

// Add rate limiting configuration
builder.Services.AddMemoryCache();
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

var app = builder.Build();

// Configure the HTTP request pipeline

// Replace the existing Swagger UI configuration with our custom method
app.UseSwaggerWithApiKey();

app.UseHttpsRedirection();

// Add custom middleware
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapControllers();

#if DEBUG
// Seed example data when in debug mode
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IExampleDataSeeder>();
    seeder.SeedExampleData().Wait();
}
#endif

// Properly handle debug mode
if (args.Contains("--debug"))
{
    Console.WriteLine("Running in debug mode with in-memory data store");
    Console.WriteLine($"Application started. Listening on {app.Urls.FirstOrDefault() ?? "default URL"}");
    Console.WriteLine("Press Ctrl+C to shut down.");
    
    // Prevent immediate shutdown in console mode
    await app.RunAsync();
}
else
{
    // Normal run without console waiting
    app.Run();
}