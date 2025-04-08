using FMP.API.DataAccess.Interfaces;
using FMP.API.DataAccess.Cosmos;
using FMP.API.DataAccess.InMemory;
using FMP.API.Middleware;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options => 
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Add swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Feature Management Platform API", 
        Version = "v1",
        Description = "API for managing feature flags across environments"
    });
    
    // Configure API key authentication in Swagger
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API key needed to access the endpoints (ApiKey YOUR_API_KEY)",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new string[] {}
        }
    });
});

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
app.UseSwagger();
app.UseSwaggerUI();

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

app.Run();
