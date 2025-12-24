using StoreInventoryApp.DTOs;
using System.Data;

namespace StoreInventoryApp.Services
{
    public interface IInventoryService
    {
        Task<List<StoreDto>> GetStoresAsync();
        Task<List<CategoryDto>> GetCategoriesAsync();
        Task<InventoryReportResult> GenerateInventoryReportAsync(int? storeId, int? categoryId, string stockStatus);
        Task<DataTable> GetInventoryLevelsAsync();
        Task<int> GetLowStockCountAsync(int? storeId);
        Task<int> GetTotalProductsCountAsync();
    }
}