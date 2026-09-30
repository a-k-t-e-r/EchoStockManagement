using System.Data;
using Microsoft.Data.SqlClient;
using StockManagement.Domain.Interfaces;

namespace StockManagement.Infrastructure.Data;

public class SqlConnectionFactory(string connectionStr) : IDbConnectionFactory
{
    private readonly string _connectionStr = connectionStr;

    public async Task<IDbConnection> CreateAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionStr);
        await connection.OpenAsync(ct);

        return connection;
    }
}