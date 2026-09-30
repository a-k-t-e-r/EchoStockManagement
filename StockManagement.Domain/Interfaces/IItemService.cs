using StockManagement.Domain.Entities;

namespace StockManagement.Domain.Interfaces;

public interface IItemService
{
    Task<List<Item>> GetAllAsync();
    Task<List<Item>> SearchAsync(string term, int take = 20);
    Task<Item> CreateAsync(string itemName, string uom = "PCS");
    Task<List<Store>> GetStoresAsync();
}