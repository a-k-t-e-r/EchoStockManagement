namespace StockManagement.Domain.Entities;

public class StockTransaction
{
    public long TransactionId { get; set; }
    public int StoreId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = "RECEIPT";
    public string ReferenceNo { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "system";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[]? RowVersion { get; set; }

    public Store? Store { get; set; }
    public List<StockTransactionDetail> Details { get; set; } = new();
}