using Microsoft.Data.SqlClient;
using StockManagement.Domain.DTOs;
using StockManagement.Domain.Interfaces;
using System.Data;

namespace StockManagement.Infrastructure.Services;

public class StockService : IStockService
{
    private readonly IDbConnectionFactory _factory;

    public StockService(IDbConnectionFactory factory) => _factory = factory;

    // TRANSACTIONAL BATCH SAVE — all rows succeed or all roll back
    public async Task<SaveResult> SaveBatchAsync(StockHeaderDto header, List<StockDetailDto> details)
    {
        if (details.Count == 0) return SaveResult.Fail("At least one detail row is required.");
        if (header.StoreId <= 0) return SaveResult.Fail("Store must be selected.");
        if (details.Any(d => d.ItemId is null)) return SaveResult.Fail("Every row must have an item.");
        if (details.Any(d => d.Quantity <= 0)) return SaveResult.Fail("Every quantity must be positive.");

        using var conn = (SqlConnection)await _factory.CreateAsync();

        // SERIALIZABLE prevents another session from reading the same balance rows
        // between our validation and our UPDATE.
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            // 1. Validate: all items exist
            var itemIds = details.Select(d => d.ItemId!.Value).Distinct().ToList();
            using (var check = conn.CreateCommand())
            {
                check.Transaction = tx;
                var paramList = string.Join(",", itemIds.Select((_, i) => "@p" + i));
                check.CommandText = $"SELECT COUNT(*) FROM dbo.Items WHERE ItemId IN ({paramList});";
                for (int i = 0; i < itemIds.Count; i++)
                    check.Parameters.AddWithValue("@p" + i, itemIds[i]);

                var found = (int)(await check.ExecuteScalarAsync())!;
                if (found != itemIds.Count)
                {
                    tx.Rollback();
                    return SaveResult.Fail("One or more items no longer exist.");
                }
            }

            // 2. Validate: ISSUE cannot exceed available
            if (header.TransactionType == "ISSUE")
            {
                foreach (var g in details.GroupBy(d => d.ItemId!.Value))
                {
                    var available = await GetAvailableQtyInternalAsync(conn, tx, header.StoreId, g.Key);
                    var required = g.Sum(x => x.Quantity);
                    if (available < required)
                    {
                        tx.Rollback();
                        return SaveResult.Fail(
                            $"Insufficient stock for item {g.Key}. Available {available}, required {required}.");
                    }
                }
            }

            // 3. Insert header
            long transactionId;
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"
                    INSERT INTO dbo.StockTransactions
                        (StoreId, TransactionDate, TransactionType, ReferenceNo, CreatedBy, CreatedAt)
                    OUTPUT INSERTED.TransactionId
                    VALUES (@storeId, @date, @type, @ref, @user, SYSUTCDATETIME());";
                ins.Parameters.AddWithValue("@storeId", header.StoreId);
                ins.Parameters.AddWithValue("@date", header.TransactionDate);
                ins.Parameters.AddWithValue("@type", header.TransactionType);
                ins.Parameters.AddWithValue("@ref", header.ReferenceNo ?? "");
                ins.Parameters.AddWithValue("@user", header.CreatedBy ?? "user");

                transactionId = (long)(await ins.ExecuteScalarAsync())!;
            }

            // 4. Insert details
            foreach (var d in details)
            {
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
                    INSERT INTO dbo.StockTransactionDetails
                        (TransactionId, ItemId, Quantity, UnitCost, ExpiryDate, IsDamaged, Remarks)
                    VALUES (@tid, @iid, @qty, @cost, @exp, @dmg, @rem);";
                ins.Parameters.AddWithValue("@tid", transactionId);
                ins.Parameters.AddWithValue("@iid", d.ItemId!.Value);
                ins.Parameters.AddWithValue("@qty", d.Quantity);
                ins.Parameters.AddWithValue("@cost", d.UnitCost);
                ins.Parameters.AddWithValue("@exp", (object?)d.ExpiryDate ?? DBNull.Value);
                ins.Parameters.AddWithValue("@dmg", d.IsDamaged);
                ins.Parameters.AddWithValue("@rem", (object?)d.Remarks ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync();
            }

            // 5. Update balances (optimistic concurrency via RowVersion)
            var sign = header.TransactionType == "ISSUE" ? -1m : 1m;

            foreach (var g in details.GroupBy(d => d.ItemId!.Value))
            {
                var delta = sign * g.Sum(x => x.Quantity);
                var ok = await ApplyBalanceDeltaAsync(conn, tx, header.StoreId, g.Key, delta);
                if (!ok)
                {
                    tx.Rollback();
                    return SaveResult.Fail(
                        "Stock was modified by another user. Please reload and retry.");
                }
            }

            tx.Commit();
            return SaveResult.Ok(transactionId);
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { /* swallow */ }
            return SaveResult.Fail(ex.Message);
        }
    }

    // OPTIMISTIC CONCURRENCY: UPDATE ... WHERE RowVersion = @original
    private static async Task<bool> ApplyBalanceDeltaAsync(
        SqlConnection conn, SqlTransaction tx, int storeId, int itemId, decimal delta)
    {
        byte[]? originalRowVersion;
        decimal currentQty;

        // UPDLOCK + serializable transaction means we hold the row until commit
        using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = @"
                SELECT OnHandQty, RowVersion
                FROM dbo.StockBalances WITH (UPDLOCK)
                WHERE StoreId = @s AND ItemId = @i;";
            read.Parameters.AddWithValue("@s", storeId);
            read.Parameters.AddWithValue("@i", itemId);

            using var r = await read.ExecuteReaderAsync();
            if (!await r.ReadAsync())
            {
                r.Close();
                // Create a balance row if none exists
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
                    INSERT INTO dbo.StockBalances (StoreId, ItemId, OnHandQty)
                    VALUES (@s, @i, @q);";
                ins.Parameters.AddWithValue("@s", storeId);
                ins.Parameters.AddWithValue("@i", itemId);
                ins.Parameters.AddWithValue("@q", delta);
                await ins.ExecuteNonQueryAsync();
                
                return true;
            }

            currentQty = r.GetDecimal(0);
            originalRowVersion = (byte[])r["RowVersion"];
        }

        var newQty = currentQty + delta;
        if (newQty < 0) return false;

        using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = @"
            UPDATE dbo.StockBalances
            SET OnHandQty = @q
            WHERE StoreId = @s AND ItemId = @i AND RowVersion = @rv;";
        upd.Parameters.AddWithValue("@q", newQty);
        upd.Parameters.AddWithValue("@s", storeId);
        upd.Parameters.AddWithValue("@i", itemId);
        upd.Parameters.AddWithValue("@rv", originalRowVersion);

        var affected = await upd.ExecuteNonQueryAsync();
        return affected == 1;
    }

    private static async Task<decimal> GetAvailableQtyInternalAsync(
        SqlConnection conn, SqlTransaction tx, int storeId, int itemId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT OnHandQty FROM dbo.StockBalances WHERE StoreId=@s AND ItemId=@i;";
        cmd.Parameters.AddWithValue("@s", storeId);
        cmd.Parameters.AddWithValue("@i", itemId);
        var result = await cmd.ExecuteScalarAsync();

        return result is null or DBNull ? 0m : Convert.ToDecimal(result);
    }

    // QUERIES
    public async Task<decimal> GetAvailableQtyAsync(int storeId, int itemId)
    {
        using var conn = await _factory.CreateAsync();
        using var cmd = (SqlCommand)conn.CreateCommand();
        cmd.CommandText = "SELECT OnHandQty FROM dbo.StockBalances WHERE StoreId=@s AND ItemId=@i;";
        cmd.Parameters.AddWithValue("@s", storeId);
        cmd.Parameters.AddWithValue("@i", itemId);
        var result = await cmd.ExecuteScalarAsync();
        return result is null or DBNull ? 0m : Convert.ToDecimal(result);
    }

    public async Task<List<StockLedgerRowDto>> GetLedgerAsync(int storeId, DateTime from, DateTime to)
    {
        using var conn = (SqlConnection)await _factory.CreateAsync();

        var storeName = "Store " + storeId;
        using (var sn = conn.CreateCommand())
        {
            sn.CommandText = "SELECT StoreName FROM dbo.Stores WHERE StoreId=@s;";
            sn.Parameters.AddWithValue("@s", storeId);
            var n = await sn.ExecuteScalarAsync();
            if (n is string s) storeName = s;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT t.TransactionDate, t.ReferenceNo, t.TransactionType,
                   i.ItemCode, i.ItemName, d.Quantity
            FROM dbo.StockTransactionDetails d
            INNER JOIN dbo.StockTransactions t ON t.TransactionId = d.TransactionId
            INNER JOIN dbo.Items i ON i.ItemId = d.ItemId
            WHERE t.StoreId = @s
              AND t.TransactionDate >= @from
              AND t.TransactionDate <  DATEADD(DAY, 1, @to)
            ORDER BY t.TransactionDate, t.TransactionId;";
        cmd.Parameters.AddWithValue("@s", storeId);
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);

        var list = new List<StockLedgerRowDto>();
        decimal running = 0m;

        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var type = r.GetString(2);
            var qty = r.GetDecimal(5);
            var inQty = type == "RECEIPT" ? qty : 0m;
            var outQty = type == "ISSUE" ? qty : 0m;
            running += inQty - outQty;

            list.Add(new StockLedgerRowDto
            {
                TransactionDate = r.GetDateTime(0),
                ReferenceNo = r.GetString(1),
                TransactionType = type,
                ItemCode = r.GetString(3),
                ItemName = r.GetString(4),
                InQty = inQty,
                OutQty = outQty,
                Balance = running,
                StoreName = storeName
            });
        }
        return list;
    }

    public async Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int take = 10)
    {
        using var conn = await _factory.CreateAsync();
        using var cmd = (SqlCommand)conn.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP (@take)
                t.TransactionId, t.TransactionDate, t.TransactionType, t.ReferenceNo,
                s.StoreName, t.CreatedBy,
                (SELECT COUNT(*) FROM dbo.StockTransactionDetails d WHERE d.TransactionId = t.TransactionId) AS LineCount
            FROM dbo.StockTransactions t
            INNER JOIN dbo.Stores s ON s.StoreId = t.StoreId
            ORDER BY t.TransactionId DESC;";
        cmd.Parameters.AddWithValue("@take", take);

        var list = new List<RecentTransactionDto>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new RecentTransactionDto
            {
                TransactionId = r.GetInt64(0),
                TransactionDate = r.GetDateTime(1),
                TransactionType = r.GetString(2),
                ReferenceNo = r.GetString(3),
                StoreName = r.GetString(4),
                CreatedBy = r.GetString(5),
                LineCount = r.GetInt32(6)
            });
        }
        return list;
    }
}