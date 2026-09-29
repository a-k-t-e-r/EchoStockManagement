namespace StockManagement.Domain.Interfaces;

public interface IReportService
{
    Task<byte[]> RenderStockLedgerPdfAsync(int storeId, DateTime from, DateTime to);
}