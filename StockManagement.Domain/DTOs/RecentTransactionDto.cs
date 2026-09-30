namespace StockManagement.Domain.DTOs;

public class RecentTransactionDto
{
    public long TransactionId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string ReferenceNo { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public int LineCount { get; set; }
}