using StockManagement.Domain.DTOs;
using StockManagement.Domain.Interfaces;
using StockManagement.Infrastructure.Data;
using StockManagement.Infrastructure.Services;

public class StockServiceTransactionTests : IAsyncLifetime
{
    private readonly string _dbName = "StockTest_" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _cs;
    private readonly IDbConnectionFactory _factory;

    public StockServiceTransactionTests()
    {
        _cs = $"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True";
        _factory = new SqlConnectionFactory(_cs);
    }

    public async Task InitializeAsync() => await DatabaseInitializer.InitializeAsync(_cs);
    public async Task DisposeAsync() { /* drop DB */ }

    [Fact]
    public async Task Issue_MoreThanAvailable_RollsBackEverything()
    {
        var svc = new StockService(_factory);

        var before = await svc.GetAvailableQtyAsync(1, 1);

        var result = await svc.SaveBatchAsync(
            new StockHeaderDto { StoreId = 1, TransactionType = "ISSUE" },
            new List<StockDetailDto>
            {
                new() { ItemId = 1, Quantity = 1 },     // valid
                new() { ItemId = 2, Quantity = 99999 }  // invalid — should roll back the whole thing
            });

        Assert.False(result.Success);
        // The key assertion: item 1's balance is unchanged, proving atomicity
        Assert.Equal(before, await svc.GetAvailableQtyAsync(1, 1));
    }

    [Fact]
    public async Task Concurrent_Issues_SecondOneFailsWithConcurrencyError()
    {
        var svc1 = new StockService(_factory);
        var svc2 = new StockService(_factory);

        // Both try to issue 80 units of an item that only has 100
        var task1 = svc1.SaveBatchAsync(
            new StockHeaderDto { StoreId = 1, TransactionType = "ISSUE" },
            new List<StockDetailDto> { new() { ItemId = 1, Quantity = 80 } });

        var task2 = svc2.SaveBatchAsync(
            new StockHeaderDto { StoreId = 1, TransactionType = "ISSUE" },
            new List<StockDetailDto> { new() { ItemId = 1, Quantity = 80 } });

        var results = await Task.WhenAll(task1, task2);

        // Exactly one succeeds
        Assert.Single(results, r => r.Success);
        Assert.Single(results, r => !r.Success);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("modified by another user")
                                   || r.ErrorMessage!.Contains("Insufficient"));
    }
}