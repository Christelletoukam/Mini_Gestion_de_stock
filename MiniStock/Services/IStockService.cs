using MiniStock.Models;

namespace MiniStock.Services;

public interface IStockService
{
    Task<IReadOnlyList<StockProduct>> GetProductsAsync();
    Task<IReadOnlyList<StockProduct>> GetAlertsAsync();
    Task AddProductAsync(string name, string? category, int unitPrice, int quantity, int minimumQuantity);
    Task UpdateProductPricingAsync(Guid productId, int unitPrice, int minimumQuantity);
    Task<IReadOnlyList<StockMovement>> GetMovementsAsync();
    Task<IReadOnlyList<StockMovement>> GetProductMovementsAsync(Guid productId);
    Task RegisterMovementAsync(Guid productId, TypeMouvementStock type, int quantity, string? reason);
    Task SaveInventoryAsync(IReadOnlyDictionary<Guid, int> physicalQuantities);
    Task<StockProfile> GetProfileAsync();
    Task<StockSummary> GetSummaryAsync();
}
