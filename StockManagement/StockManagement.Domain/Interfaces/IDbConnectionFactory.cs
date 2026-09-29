using System.Data;

namespace StockManagement.Domain.Interfaces;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateAsync(CancellationToken ct = default);
}