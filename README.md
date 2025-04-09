# Feature Management Platform

A feature flag management platform built with .NET 9, allowing you to manage feature flags across different environments.

## Project Structure

- `FMP.API` - The API project implementing the feature flag management endpoints
- `FMP.API.Tests` - The test project with comprehensive tests for the API

## Prerequisites

- [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) 
- [Azure Cosmos DB Emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator) (for non-debug mode)

## Running the API

You can use the included Makefile to run various commands:

### Debug Mode (In-Memory Data Store)

In debug mode, the API uses an in-memory data store and seeds it with example data automatically:

```bash
# Using make:
make runapi-debug

# Or manually:
cd FMP.API
dotnet run --debug
```

The API will be available at `http://localhost:5000` and the Swagger UI at `http://localhost:5000/swagger`.

### Regular Mode (Cosmos DB)

For normal operation, the API connects to Cosmos DB. Make sure you have the Cosmos DB Emulator running before starting:

1. Start the Azure Cosmos DB Emulator
2. Run the API:

```bash
# In the future, this will be available as:
# make run-cosmos

# For now:
cd FMP.API
dotnet run
```

The API will connect to the local Cosmos DB Emulator using the connection string configured in `appsettings.json`.

## Running Tests

The Makefile provides several commands for running tests:

```bash
# Run all tests
make runtests

# Run specific test categories
make testflags     # Run Feature Flag tests
make testrules     # Run Targeting Rule tests
make testenv       # Run Environment tests
make testanalytics # Run Analytics tests

# Run a specific test by name
make test name=AddTargetingRule_WithValidData_ReturnsCreatedRule
```

## Building and Cleaning

```bash
# Build the solution
make build

# Clean the solution
make clean
```

## Development Helpers

```bash
# Watch for changes and auto-restart the API
make watch

# See all available make commands
make help
```

## Configuration

All configuration is in `appsettings.json`:

- `ConnectionStrings:CosmosDb`: The connection string for Cosmos DB
- `CosmosDb:DatabaseName`: The database name to use
- `ApiKeys`: Configured API keys for authentication
- `RateLimit`: Rate limiting settings

## Authentication

The API uses API key-based authentication. Include the key in the `Authorization` header with one of the following formats:
```
Authorization: ApiKey YOUR_API_KEY
Authorization: Bearer YOUR_API_KEY
```

The default API key is `test-api-key` (configured in appsettings.json).

## API Documentation

The Swagger UI provides full documentation for all API endpoints. Access it at:

```
https://localhost:5001/swagger
```

## Running the Tests

```bash
cd FMP.API.Tests
dotnet test
```

## Cosmos DB Setup

If you want to use a real Cosmos DB instance (not the emulator), you'll need to:

1. Create an Azure Cosmos DB account
2. Create a database called "FeatureManagementDb" (or update the configuration)
3. Update the connection string in `appsettings.json`

## Understanding the Data Stores

### In-Memory Store (Debug Mode)

When running in debug mode, the API will use an in-memory store that exists only for the duration of the application's runtime. This is prefect for development and testing.

### Cosmos DB (Normal Mode)

In normal operating mode, the API connects to Cosmos DB, which provides:

- Persistence of data
- Scalability for production
- Automatic indexing
- Geo-replication

The Cosmos DB schema follows the models defined in the API, with separate containers for:

- Feature flags
- Environments
- Analytics data
