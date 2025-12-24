using Microsoft.Data.SqlClient;
using StoreInventoryApp.DTOs;
using System.Data;

namespace StoreInventoryApp.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IConfiguration _configuration;

        public InventoryService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string ConnectionString => _configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");

        public async Task<List<StoreDto>> GetStoresAsync()
        {
            var stores = new List<StoreDto>();
            
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = "SELECT StoreID, StoreName, StoreType FROM Stores ORDER BY StoreName";
            
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                stores.Add(new StoreDto
                {
                    StoreID = reader.GetInt32(0),
                    StoreName = reader.GetString(1),
                    StoreType = reader.IsDBNull(2) ? "" : reader.GetString(2)
                });
            }
            
            return stores;
        }

        public async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            var categories = new List<CategoryDto>();
            
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = "SELECT CategoryID, CategoryName FROM Categories ORDER BY CategoryName";
            
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                categories.Add(new CategoryDto
                {
                    CategoryID = reader.GetInt32(0),
                    CategoryName = reader.GetString(1)
                });
            }
            
            return categories;
        }

        public async Task<DataTable> GetInventoryLevelsAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = @"
                SELECT 
                    p.ProductID, p.ProductName, c.CategoryName,
                    ISNULL(i.QuantityOnHand, 0) as QuantityOnHand, 
                    ISNULL(i.QuantityStored, 0) as QuantityStored,
                    p.ReorderLevel,
                    CASE 
                        WHEN ISNULL(i.QuantityOnHand, 0) <= p.ReorderLevel THEN 'Critical'
                        WHEN ISNULL(i.QuantityOnHand, 0) <= (p.ReorderLevel * 1.5) THEN 'Low'
                        ELSE 'OK'
                    END AS StockStatus
                FROM Products p
                LEFT JOIN Inventory i ON p.ProductID = i.ProductID
                JOIN Categories c ON p.CategoryID = c.CategoryID
                ORDER BY StockStatus, p.ProductName";
            
            await using var command = new SqlCommand(query, connection);
            using var adapter = new SqlDataAdapter(command);
            
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            return dataTable;
        }

        public async Task<InventoryReportResult> GenerateInventoryReportAsync(int? storeId, int? categoryId, string stockStatus)
        {
            var result = new InventoryReportResult
            {
                InventoryItems = new List<InventoryItemDto>(),
                CategoryBreakdown = new List<CategoryInventoryDto>()
            };
            
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            // Get summary stats
            await GetSummaryStatsAsync(connection, result, storeId, categoryId);
            
            // Get inventory items
            await GetInventoryItemsAsync(connection, result, storeId, categoryId, stockStatus);
            
            // Get category breakdown
            await GetCategoryBreakdownAsync(connection, result, storeId, categoryId);
            
            return result;
        }

        private async Task GetSummaryStatsAsync(SqlConnection connection, InventoryReportResult result, int? storeId, int? categoryId)
        {
            var query = @"
                SELECT 
                    COUNT(DISTINCT p.ProductID) AS TotalProducts,
                    SUM(i.QuantityOnHand * p.UnitPrice) AS TotalValue,
                    SUM(CASE WHEN i.QuantityOnHand <= p.ReorderLevel AND i.QuantityOnHand > 0 THEN 1 ELSE 0 END) AS LowStock,
                    SUM(CASE WHEN i.QuantityOnHand <= (p.ReorderLevel * 0.5) AND i.QuantityOnHand > 0 THEN 1 ELSE 0 END) AS CriticalStock,
                    SUM(CASE WHEN i.QuantityOnHand = 0 THEN 1 ELSE 0 END) AS OutOfStock
                FROM Products p
                LEFT JOIN Inventory i ON p.ProductID = i.ProductID
                WHERE 1=1
                  AND (@StoreID IS NULL OR i.StoreID = @StoreID)
                  AND (@CategoryID IS NULL OR p.CategoryID = @CategoryID)";
            
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@StoreID", storeId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CategoryID", categoryId ?? (object)DBNull.Value);
            
            await using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                result.TotalProducts = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                result.TotalInventoryValue = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                result.LowStockCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                result.CriticalStockCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                result.OutOfStockCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            }
        }

        private async Task GetInventoryItemsAsync(SqlConnection connection, InventoryReportResult result, int? storeId, int? categoryId, string stockStatus)
        {
            var query = @"
                SELECT 
                    p.ProductID,
                    p.ProductName,
                    c.CategoryName,
                    ISNULL(i.QuantityOnHand, 0) AS QuantityOnHand,
                    ISNULL(i.QuantityStored, 0) AS QuantityStored,
                    p.ReorderLevel,
                    p.UnitPrice,
                    (ISNULL(i.QuantityOnHand, 0) * p.UnitPrice) AS TotalValue,
                    s.StoreName,
                    CASE 
                        WHEN ISNULL(i.QuantityOnHand, 0) = 0 THEN 'Out of Stock'
                        WHEN ISNULL(i.QuantityOnHand, 0) <= (p.ReorderLevel * 0.5) THEN 'Critical'
                        WHEN ISNULL(i.QuantityOnHand, 0) <= p.ReorderLevel THEN 'Low'
                        ELSE 'OK'
                    END AS StockStatus
                FROM Products p
                LEFT JOIN Inventory i ON p.ProductID = i.ProductID
                LEFT JOIN Stores s ON i.StoreID = s.StoreID
                JOIN Categories c ON p.CategoryID = c.CategoryID
                WHERE 1=1
                  AND (@StoreID IS NULL OR i.StoreID = @StoreID)
                  AND (@CategoryID IS NULL OR p.CategoryID = @CategoryID)";
            
            // Apply stock status filter
            if (!string.IsNullOrEmpty(stockStatus) && stockStatus != "All")
            {
                query += stockStatus switch
                {
                    "Low" => " AND ISNULL(i.QuantityOnHand, 0) <= p.ReorderLevel AND ISNULL(i.QuantityOnHand, 0) > 0",
                    "Critical" => " AND ISNULL(i.QuantityOnHand, 0) <= (p.ReorderLevel * 0.5) AND ISNULL(i.QuantityOnHand, 0) > 0",
                    "OK" => " AND ISNULL(i.QuantityOnHand, 0) > p.ReorderLevel",
                    "OutOfStock" => " AND ISNULL(i.QuantityOnHand, 0) = 0",
                    _ => ""
                };
            }
            
            query += " ORDER BY StockStatus, p.ProductName";
            
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@StoreID", storeId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CategoryID", categoryId ?? (object)DBNull.Value);
            
            await using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                result.InventoryItems.Add(new InventoryItemDto
                {
                    ProductID = reader.GetInt32(0),
                    ProductName = reader.GetString(1),
                    CategoryName = reader.GetString(2),
                    QuantityOnHand = reader.GetInt32(3),
                    QuantityStored = reader.GetInt32(4),
                    ReorderLevel = reader.GetInt32(5),
                    UnitPrice = reader.GetDecimal(6),
                    TotalValue = reader.GetDecimal(7),
                    StoreName = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                    StockStatus = reader.GetString(9)
                });
            }
        }

        private async Task GetCategoryBreakdownAsync(SqlConnection connection, InventoryReportResult result, int? storeId, int? categoryId)
        {
            var query = @"
                SELECT 
                    c.CategoryName,
                    COUNT(DISTINCT p.ProductID) AS ProductCount,
                    SUM(ISNULL(i.QuantityOnHand, 0)) AS TotalQuantity,
                    SUM(ISNULL(i.QuantityOnHand, 0) * p.UnitPrice) AS TotalValue,
                    SUM(CASE WHEN ISNULL(i.QuantityOnHand, 0) <= p.ReorderLevel THEN 1 ELSE 0 END) AS LowStockCount
                FROM Categories c
                LEFT JOIN Products p ON c.CategoryID = p.CategoryID
                LEFT JOIN Inventory i ON p.ProductID = i.ProductID
                WHERE 1=1
                  AND (@StoreID IS NULL OR i.StoreID = @StoreID)
                  AND (@CategoryID IS NULL OR c.CategoryID = @CategoryID)
                GROUP BY c.CategoryName
                HAVING SUM(ISNULL(i.QuantityOnHand, 0)) > 0
                ORDER BY TotalValue DESC";
            
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@StoreID", storeId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CategoryID", categoryId ?? (object)DBNull.Value);
            
            await using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                result.CategoryBreakdown.Add(new CategoryInventoryDto
                {
                    CategoryName = reader.GetString(0),
                    ProductCount = reader.GetInt32(1),
                    TotalQuantity = reader.GetInt32(2),
                    TotalValue = reader.GetDecimal(3),
                    LowStockCount = reader.GetInt32(4)
                });
            }
        }

        public async Task<int> GetLowStockCountAsync(int? storeId)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = @"SELECT COUNT(*) FROM Inventory i 
                         JOIN Products p ON i.ProductID = p.ProductID 
                         WHERE i.QuantityOnHand <= p.ReorderLevel";
            
            if (storeId.HasValue)
            {
                query += " AND i.StoreID = @StoreID";
            }
            
            await using var command = new SqlCommand(query, connection);
            
            if (storeId.HasValue)
            {
                command.Parameters.AddWithValue("@StoreID", storeId.Value);
            }
            
            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public async Task<int> GetTotalProductsCountAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = "SELECT COUNT(*) FROM Products";
            
            await using var command = new SqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }
}