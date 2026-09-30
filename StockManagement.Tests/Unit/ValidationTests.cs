    using System.ComponentModel.DataAnnotations;
using StockManagement.Domain.DTOs;
using Xunit;

public class ValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Quantity_MustBePositive(decimal qty)
    {
        var dto = new StockDetailDto { ItemId = 1, Quantity = qty };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StockDetailDto.Quantity)));
    }
}