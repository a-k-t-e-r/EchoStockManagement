using System.ComponentModel.DataAnnotations;

namespace StockManagement.Domain.DTOs;

public class StockDetailDto
{
    public long DetailId { get; set; }

    [Required(ErrorMessage = "Item is required.")]
    public int? ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public bool IsDamaged { get; set; }
    public string? Remarks { get; set; }

    // Per-row validation message for the UI only
    public string? RowError { get; set; }
}