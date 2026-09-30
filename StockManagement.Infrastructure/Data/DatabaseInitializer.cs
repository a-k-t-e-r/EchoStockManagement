using System.Reflection;
using Microsoft.Data.SqlClient;

namespace StockManagement.Infrastructure.Data;

public static class DatabaseInitializer
{
    private const string ResourceName = "StockManagement.Infrastructure.Sql.schema.sql";

    public static async Task InitializeAsync(string connectionString, CancellationToken ct = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        var dbName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync(ct);
            await using var createDb = new SqlCommand($"IF DB_ID('{dbName}') IS NULL CREATE DATABASE [{dbName}];",
                                                      master);
            await createDb.ExecuteNonQueryAsync(ct);
        }

        // Run schema.sql (idempotent)
        var sql = ReadEmbedded(ResourceName);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 })
            await cmd.ExecuteNonQueryAsync(ct);

        // 3. Seed
        await SeedAsync(conn, ct);
    }

    private static async Task SeedAsync(SqlConnection conn, CancellationToken ct)
    {
        var storeCount = (int)(await new SqlCommand("SELECT COUNT(*) FROM dbo.Stores", conn)
            .ExecuteScalarAsync(ct))!;
        if (storeCount == 0)
        {
            await new SqlCommand(@"
                INSERT INTO dbo.Stores (StoreCode, StoreName) VALUES
                ('S001','Main Warehouse'),
                ('S002','Downtown Branch');", conn).ExecuteNonQueryAsync(ct);
        }

        var itemCount = (int)(await new SqlCommand("SELECT COUNT(*) FROM dbo.Items", conn)
            .ExecuteScalarAsync(ct))!;
        if (itemCount == 0)
        {
            await new SqlCommand(@"
                INSERT INTO dbo.Items (ItemCode, ItemName, UOM) VALUES
                ('ITM001','A4 Paper Ream','REAM'),
                ('ITM002','Blue Pen','PCS'),
                ('ITM003','Toner Cartridge','PCS'),
                ('ITM004','Stapler','PCS');", conn).ExecuteNonQueryAsync(ct);
        }

        await new SqlCommand(@"
            INSERT INTO dbo.StockBalances (StoreId, ItemId, OnHandQty)
            SELECT s.StoreId, i.ItemId, 100
            FROM dbo.Stores s CROSS JOIN dbo.Items i
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.StockBalances b
                WHERE b.StoreId = s.StoreId AND b.ItemId = i.ItemId);", conn)
            .ExecuteNonQueryAsync(ct);
    }

    private static string ReadEmbedded(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();

        using var stream = asm.GetManifestResourceStream(resourceName)
            ??
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found. " +
                                                $"Available: {string.Join(", ", asm.GetManifestResourceNames())}");
        
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}