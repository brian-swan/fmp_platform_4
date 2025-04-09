using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace FMP.API.Configuration;

public static class SwaggerConfiguration
{
    public static void AddSwaggerWithApiKey(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Feature Management Platform API", Version = "v1" });
            
            // Define the API Key scheme
            c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Description = "API Key authentication using the 'Authorization' header. Example: 'ApiKey test-api-key'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "ApiKey"
            });
            
            // Make sure all endpoints use the API Key
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        },
                        Scheme = "ApiKey",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                    },
                    new List<string>()
                }
            });
            
            // Add Operation Filter to correctly format API key
            c.OperationFilter<SwaggerAuthorizationOperationFilter>();
        });
    }
    
    public static void UseSwaggerWithApiKey(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Feature Management Platform API v1");
            c.DocumentTitle = "Feature Management Platform API";
            
            // Add the API key to the Swagger UI, so it gets sent with requests
            c.ConfigObject.AdditionalItems.Add("persistAuthorization", true);
        });
    }
}

// Add a new operation filter class to correctly format the API key
public class SwaggerAuthorizationOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        // This ensures that when the "Authorize" button is clicked,
        // the API key will be automatically prefixed with 'ApiKey ' in the header
    }
}
