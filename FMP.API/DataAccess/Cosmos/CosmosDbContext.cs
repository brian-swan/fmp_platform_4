using Microsoft.Azure.Cosmos;

namespace FMP.API.DataAccess.Cosmos;

public class CosmosDbContext : IDisposable
{
    private readonly CosmosClient _client;
    private readonly string _databaseId;
    private Database? _database;
    private Container? _flagsContainer;
    private Container? _environmentsContainer;
    private Container? _analyticsContainer;

    public CosmosDbContext(string? connectionString, string? databaseId)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        
        if (string.IsNullOrEmpty(databaseId))
            throw new ArgumentNullException(nameof(databaseId));
        
        _client = new CosmosClient(connectionString);
        _databaseId = databaseId;
    }

    public async Task<Database> GetDatabaseAsync()
    {
        if (_database == null)
        {
            _database = await _client.CreateDatabaseIfNotExistsAsync(_databaseId);
        }
        
        return _database;
    }

    public async Task<Container> GetFlagsContainerAsync()
    {
        if (_flagsContainer == null)
        {
            var database = await GetDatabaseAsync();
            _flagsContainer = await database.CreateContainerIfNotExistsAsync("Flags", "/id");
        }
        
        return _flagsContainer;
    }

    public async Task<Container> GetEnvironmentsContainerAsync()
    {
        if (_environmentsContainer == null)
        {
            var database = await GetDatabaseAsync();
            _environmentsContainer = await database.CreateContainerIfNotExistsAsync("Environments", "/id");
        }
        
        return _environmentsContainer;
    }

    public async Task<Container> GetAnalyticsContainerAsync()
    {
        if (_analyticsContainer == null)
        {
            var database = await GetDatabaseAsync();
            _analyticsContainer = await database.CreateContainerIfNotExistsAsync("Analytics", "/flagKey");
        }
        
        return _analyticsContainer;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
