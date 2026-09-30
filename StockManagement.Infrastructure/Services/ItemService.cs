using Microsoft.Data.SqlClient;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Interfaces;

namespace StockManagement.Infrastructure.Services;

public class ItemService(IDbConnectionFactory factory) : IItemService
{
    private readonly IDbConnectionFactory _factory = factory;

    public async Task<List<Item>> GetAllAsync()
    {
        using var connection = await _factory.CreateAsync();
        using var command = (SqlCommand)connection.CreateCommand();
        command.CommandText = @"
            SELECT ItemId, ItemCode, ItemName, UOM, IsActive
            FROM dbo.Items
            WHERE IsActive = 1
            ORDER BY ItemName;";

        var list = new List<Item>();
        using var r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));

        return list;
    }

    public async Task<List<Item>> SearchAsync(string term, int take = 20)
    {
        using var conn = await _factory.CreateAsync();
        using var cmd = (SqlCommand)conn.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP (@take) ItemId, ItemCode, ItemName, UOM, IsActive
            FROM dbo.Items
            WHERE IsActive = 1
              AND (@term = '' OR ItemName LIKE '%' + @term + '%' OR ItemCode LIKE '%' + @term + '%')
            ORDER BY ItemName;";
        cmd.Parameters.AddWithValue("@take", take);
        cmd.Parameters.AddWithValue("@term", term ?? "");

        var list = new List<Item>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));

        return list;
    }

    public async Task<List<Store>> GetStoresAsync()
    {
        using var conn = await _factory.CreateAsync();
        using var cmd = (SqlCommand)conn.CreateCommand();
        cmd.CommandText = @"
            SELECT StoreId, StoreCode, StoreName, IsActive
            FROM dbo.Stores
            WHERE IsActive = 1
            ORDER BY StoreName;";

        var list = new List<Store>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Store
            {
                StoreId = r.GetInt32(0),
                StoreCode = r.GetString(1),
                StoreName = r.GetString(2),
                IsActive = r.GetBoolean(3)
            });
        }

        return list;
    }

    public async Task<Item> CreateAsync(string itemName, string uom = "PCS")
    {
        using var conn = (SqlConnection)await _factory.CreateAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            var code = "ITM" + DateTime.UtcNow.Ticks.ToString()[^6..];

            Item created;
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"
                    INSERT INTO dbo.Items (ItemCode, ItemName, UOM, IsActive)
                    OUTPUT INSERTED.ItemId, INSERTED.ItemCode, INSERTED.ItemName, INSERTED.UOM, INSERTED.IsActive
                    VALUES (@code, @name, @uom, 1);";
                ins.Parameters.AddWithValue("@code", code);
                ins.Parameters.AddWithValue("@name", itemName);
                ins.Parameters.AddWithValue("@uom", uom);

                using var r = await ins.ExecuteReaderAsync();
                if (!await r.ReadAsync()) throw new InvalidOperationException("Insert failed.");
                created = Map(r);
            }

            // Give every store a zero balance for the new item
            using (var bal = conn.CreateCommand())
            {
                bal.Transaction = tx;
                bal.CommandText = @"
                    INSERT INTO dbo.StockBalances (StoreId, ItemId, OnHandQty)
                    SELECT StoreId, @itemId, 0 FROM dbo.Stores;";
                bal.Parameters.AddWithValue("@itemId", created.ItemId);
                await bal.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return created;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static Item Map(SqlDataReader r) => new()
    {
        ItemId = r.GetInt32(0),
        ItemCode = r.GetString(1),
        ItemName = r.GetString(2),
        UOM = r.GetString(3),
        IsActive = r.GetBoolean(4)
    };
}