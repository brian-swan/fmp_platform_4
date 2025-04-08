using Microsoft.Azure.Cosmos;

namespace FMP.API.Tests.Fixtures;

/// <summary>
/// This fixture helps with Cosmos DB integration tests.
/// Note: Requires the Cosmos DB Emulator to be installed locally.
/// </summary>
public class CosmosDbEmulatorFixture : IDisposable
{
    private const string EmulatorEndpoint = "https://localhost:8081";
    private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseId = "FeatureManagementDb";
    
    public CosmosClient Client { get; }
    public Database Database { get; }
    
    public Container FlagsContainer { get; }
    public Container EnvironmentsContainer { get; }
    public Container AnalyticsContainer { get; }
    
    public CosmosDbEmulatorFixture()
    {
        // Create client and database
        Client = new CosmosClient(EmulatorEndpoint, EmulatorKey);
        Database = Client.CreateDatabaseIfNotExistsAsync(DatabaseId).GetAwaiter().GetResult();
        
        // Create containers
        FlagsContainer = Database.CreateContainerIfNotExistsAsync("Flags", "/id").GetAwaiter().GetResult();
        EnvironmentsContainer = Database.CreateContainerIfNotExistsAsync("Environments", "/id").GetAwaiter().GetResult();
        AnalyticsContainer = Database.CreateContainerIfNotExistsAsync("Analytics", "/flagKey").GetAwaiter().GetResult();
        
        // Clean up any test data from previous runs
        CleanUpTestData().GetAwaiter().GetResult();
    }
    
    private async Task CleanUpTestData()
    {
        // Delete any test data with prefix "test-" from previous test runs
        
        // Clean flags container
        var flagsQuery = new QueryDefinition("SELECT * FROM c WHERE STARTSWITH(c.key, 'test-')");
        var flagsIterator = FlagsContainer.GetItemQueryIterator<dynamic>(flagsQuery);
        
        while (flagsIterator.HasMoreResults)
        {
            var results = await flagsIterator.ReadNextAsync();
            foreach (var item in results)
            {
                await FlagsContainer.DeleteItemAsync<dynamic>(item.id.ToString(), new PartitionKey(item.id.ToString()));
            }
        }
        
        // Similar cleanup can be done for other containers
    }
    
    public void Dispose()
    {
        Client.Dispose();
    }
}
