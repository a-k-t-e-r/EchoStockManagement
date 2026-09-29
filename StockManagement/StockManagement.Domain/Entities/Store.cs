namespace StockManagement.Domain.Entities;

public class Store
{
    public int StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}