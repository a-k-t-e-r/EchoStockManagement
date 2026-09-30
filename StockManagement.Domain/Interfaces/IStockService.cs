using StockManagement.Domain.DTOs;

namespace StockManagement.Domain.Interfaces;

public interface IStockService
{
    Task<SaveResult> SaveBatchAsync(StockHeaderDto header, List<StockDetailDto> details);
    Task<decimal> GetAvailableQtyAsync(int storeId, int itemId);
    Task<List<StockLedgerRowDto>> GetLedgerAsync(int storeId, DateTime from, DateTime to);
    Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int take = 10);
}