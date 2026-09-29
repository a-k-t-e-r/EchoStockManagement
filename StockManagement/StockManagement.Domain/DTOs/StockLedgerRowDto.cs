namespace StockManagement.Domain.DTOs;

public class StockLedgerRowDto
{
    public DateTime TransactionDate { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal InQty { get; set; }
    public decimal OutQty { get; set; }
    public decimal Balance { get; set; }
    public string StoreName { get; set; } = string.Empty;
}