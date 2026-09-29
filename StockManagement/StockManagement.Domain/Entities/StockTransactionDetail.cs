namespace StockManagement.Domain.Entities;

public class StockTransactionDetail
{
    public long DetailId { get; set; }
    public long TransactionId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsDamaged { get; set; }
    public string? Remarks { get; set; }

    public StockTransaction? Transaction { get; set; }
    public Item? Item { get; set; }
}