namespace OsrsTracker.Tests.Integration;

// Sharing one factory across all integration test classes means:
// 1. Migrations only run once (no concurrent-create race condition)
// 2. Tests in the collection run sequentially (safe for a shared DB)
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory> { }
