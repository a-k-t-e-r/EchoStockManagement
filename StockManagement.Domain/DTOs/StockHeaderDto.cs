namespace StockManagement.Domain.DTOs;

public class StockHeaderDto
{
    public int StoreId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string TransactionType { get; set; } = "RECEIPT";
    public string ReferenceNo { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "user";
}