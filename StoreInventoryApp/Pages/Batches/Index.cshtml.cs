using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StoreInventoryApp.Pages.Batches
{
    public class IndexModel : BasePageModel
    {
        private readonly IConfiguration _configuration;

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<BatchDto> Batches { get; set; } = new List<BatchDto>();
        public List<ProductDto> Products { get; set; } = new List<ProductDto>();
        public List<StoreDto> Stores { get; set; } = new List<StoreDto>();

        [BindProperty(SupportsGet = true)]
        public int? FilterProductId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FilterStoreId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterStatus { get; set; }

        public int ExpiringSoonCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check authentication
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            await LoadFiltersAsync();
            await LoadBatchesAsync();
            await CheckExpiringBatchesAsync();

            return Page();
        }

        private async Task LoadFiltersAsync()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Load products for filter
                string productQuery = "SELECT ProductID, ProductName FROM Products ORDER BY ProductName";
                using (SqlCommand command = new SqlCommand(productQuery, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Products.Add(new ProductDto
                        {
                            ProductID = reader.GetInt32(0),
                            ProductName = reader.GetString(1)
                        });
                    }
                }

                // Load stores for filter
                string storeQuery = "SELECT StoreID, StoreName FROM Stores ORDER BY StoreName";
                using (SqlCommand command = new SqlCommand(storeQuery, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Stores.Add(new StoreDto
                        {
                            StoreID = reader.GetInt32(0),
                            StoreName = reader.GetString(1)
                        });
                    }
                }
            }
        }

        private async Task LoadBatchesAsync()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Build query with filters
                string query = @"
                    SELECT 
                        b.BatchID, b.BatchNumber, b.ProductionDate, b.ExpiryDate,
                        b.ReceivedQuantity, b.RemainingQuantity, b.ReceivedDate, b.Status,
                        p.ProductName, s.StoreName
                    FROM Batches b
                    INNER JOIN Products p ON b.ProductID = p.ProductID
                    INNER JOIN Stores s ON b.StoreID = s.StoreID
                    WHERE 1=1";

                if (FilterProductId.HasValue)
                    query += " AND b.ProductID = @ProductID";
                if (FilterStoreId.HasValue)
                    query += " AND b.StoreID = @StoreID";
                if (!string.IsNullOrWhiteSpace(FilterStatus))
                    query += " AND b.Status = @Status";

                // FEFO: Order by expiry date first (First Expiry First Out)
                query += " ORDER BY b.ExpiryDate ASC, b.ReceivedDate DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (FilterProductId.HasValue)
                        command.Parameters.AddWithValue("@ProductID", FilterProductId.Value);
                    if (FilterStoreId.HasValue)
                        command.Parameters.AddWithValue("@StoreID", FilterStoreId.Value);
                    if (!string.IsNullOrWhiteSpace(FilterStatus))
                        command.Parameters.AddWithValue("@Status", FilterStatus);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Batches.Add(new BatchDto
                            {
                                BatchID = reader.GetInt32(0),
                                BatchNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                                ProductionDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                                ExpiryDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                                ReceivedQuantity = reader.GetInt32(4),
                                RemainingQuantity = reader.GetInt32(5),
                                ReceivedDate = reader.GetDateTime(6),
                                Status = reader.IsDBNull(7) ? "Active" : reader.GetString(7),
                                ProductName = reader.GetString(8),
                                StoreName = reader.GetString(9)
                            });
                        }
                    }
                }
            }
        }

        private async Task CheckExpiringBatchesAsync()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Count batches expiring within 30 days
                string query = @"
                    SELECT COUNT(*) 
                    FROM Batches 
                    WHERE ExpiryDate IS NOT NULL 
                    AND ExpiryDate <= DATEADD(day, 30, GETDATE())
                    AND ExpiryDate >= GETDATE()
                    AND Status = 'Active'";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    ExpiringSoonCount = (int)await command.ExecuteScalarAsync();
                }

                // Auto-generate alerts for expiring batches
                if (ExpiringSoonCount > 0)
                {
                    await GenerateExpiryAlertsAsync(connection);
                }
            }
        }

        private async Task GenerateExpiryAlertsAsync(SqlConnection connection)
        {
            int? currentUserId = GetCurrentUserId();

            // Generate alerts for batches expiring within 30 days
            // Only create alert if one doesn't already exist for this batch in the last day
            string query = @"
                INSERT INTO Alerts (ProductID, StoreID, AlertType, Message, IsRead, CreatedAt, CreatedByUserID)
                SELECT DISTINCT 
                    b.ProductID, 
                    b.StoreID,
                    'Expiry Warning',
                    'Batch ' + ISNULL(b.BatchNumber, 'N/A') + ' expires on ' + CONVERT(VARCHAR, b.ExpiryDate, 107),
                    0,
                    GETDATE(),
                    @UserID
                FROM Batches b
                WHERE b.ExpiryDate IS NOT NULL 
                AND b.ExpiryDate <= DATEADD(day, 30, GETDATE())
                AND b.ExpiryDate >= GETDATE()
                AND b.Status = 'Active'
                AND NOT EXISTS (
                    SELECT 1 FROM Alerts a
                    WHERE a.ProductID = b.ProductID
                    AND a.StoreID = b.StoreID
                    AND a.AlertType = 'Expiry Warning'
                    AND a.Message LIKE '%' + ISNULL(b.BatchNumber, 'N/A') + '%'
                    AND a.CreatedAt >= DATEADD(day, -1, GETDATE())
                )";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@UserID", (object)currentUserId ?? DBNull.Value);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<IActionResult> OnPostMarkExpiredAsync(int batchId)
        {
            // Check authentication
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string updateQuery = "UPDATE Batches SET Status = 'Expired' WHERE BatchID = @BatchID";
                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@BatchID", batchId);
                        await command.ExecuteNonQueryAsync();
                    }

                    TempData["SuccessMessage"] = "Batch marked as expired successfully.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error marking batch as expired: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkDepletedAsync(int batchId)
        {
            // Check authentication
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Only mark as depleted if RemainingQuantity is actually 0
                    string updateQuery = @"
                        UPDATE Batches 
                        SET Status = 'Depleted' 
                        WHERE BatchID = @BatchID AND RemainingQuantity = 0";

                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@BatchID", batchId);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                            TempData["SuccessMessage"] = "Batch marked as depleted successfully.";
                        else
                            TempData["ErrorMessage"] = "Cannot mark batch as depleted - quantity is not zero.";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error marking batch as depleted: " + ex.Message;
            }

            return RedirectToPage();
        }
    }

    // DTO Classes
    public class BatchDto
    {
        public int BatchID { get; set; }
        public string BatchNumber { get; set; }
        public DateTime? ProductionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int ReceivedQuantity { get; set; }
        public int RemainingQuantity { get; set; }
        public DateTime ReceivedDate { get; set; }
        public string Status { get; set; }
        public string ProductName { get; set; }
        public string StoreName { get; set; }
    }

    public class ProductDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
    }

    public class StoreDto
    {
        public int StoreID { get; set; }
        public string StoreName { get; set; }
    }
}