using Microsoft.Data.SqlClient;
using StockManagement.Domain.DTOs;
using StockManagement.Domain.Interfaces;
using StockManagement.Infrastructure.Data;
using StockManagement.Infrastructure.Services;
using Xunit;

namespace StockManagement.Tests.Integration;

[Trait("Category", "Integration")]
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

    public async Task DisposeAsync()
    {
        await using var conn = new SqlConnection(
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            $"IF DB_ID('{_dbName}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{_dbName}]; END", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Issue_MoreThanAvailable_RollsBackEverything()
    {
        var svc = new StockService(_factory);
        var before = await svc.GetAvailableQtyAsync(1, 1);

        var result = await svc.SaveBatchAsync(
            new StockHeaderDto { StoreId = 1, TransactionType = "ISSUE" },
            new List<StockDetailDto>
            {
                new() { ItemId = 1, Quantity = 1 },
                new() { ItemId = 2, Quantity = 999_999 }
            });

        Assert.False(result.Success);
        Assert.Equal(before, await svc.GetAvailableQtyAsync(1, 1));
    }

    [Fact]
    public async Task Receipt_IncreasesBalance()
    {
        var svc = new StockService(_factory);
        var before = await svc.GetAvailableQtyAsync(1, 1);

        var result = await svc.SaveBatchAsync(
            new StockHeaderDto { StoreId = 1, TransactionType = "RECEIPT", ReferenceNo = "CI-001" },
            new List<StockDetailDto> { new() { ItemId = 1, Quantity = 7 } });

        Assert.True(result.Success);
        Assert.Equal(before + 7m, await svc.GetAvailableQtyAsync(1, 1));
    }
}