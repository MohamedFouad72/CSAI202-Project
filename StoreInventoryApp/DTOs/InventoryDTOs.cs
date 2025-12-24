namespace StoreInventoryApp.DTOs
{
    // Store DTO
    public class StoreDto
    {
        public int StoreID { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
    }

    // Category DTO
    public class CategoryDto
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    // Inventory Item DTO
    public class InventoryItemDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int QuantityOnHand { get; set; }
        public int QuantityStored { get; set; }
        public int ReorderLevel { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StockStatus { get; set; } = string.Empty;
    }

    // Category Inventory DTO
    public class CategoryInventoryDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
        public int LowStockCount { get; set; }
    }

    // Inventory Report Result
    public class InventoryReportResult
    {
        public decimal TotalInventoryValue { get; set; }
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int CriticalStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public List<InventoryItemDto> InventoryItems { get; set; } = new();
        public List<CategoryInventoryDto> CategoryBreakdown { get; set; } = new();
    }
}