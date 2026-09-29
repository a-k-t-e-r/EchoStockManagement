namespace StockManagement.Domain.Entities;

public class StockBalance
{
    public int StoreId { get; set; }
    public int ItemId { get; set; }
    public decimal OnHandQty { get; set; }
    public byte[]? RowVersion { get; set; }
}