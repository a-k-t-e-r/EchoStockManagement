namespace StockManagement.Domain.Entities;

public class Item
{
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string UOM { get; set; } = "PCS";
    public bool IsActive { get; set; } = true;
}