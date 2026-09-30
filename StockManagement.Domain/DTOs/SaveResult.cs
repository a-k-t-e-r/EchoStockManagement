namespace StockManagement.Domain.DTOs;

public class SaveResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public long? TransactionId { get; init; }

    public static SaveResult Ok(long? id = null) => new() { Success = true, TransactionId = id };
    public static SaveResult Fail(string msg) => new() { Success = false, ErrorMessage = msg };
}