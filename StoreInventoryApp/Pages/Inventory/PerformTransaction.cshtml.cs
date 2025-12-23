using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace StoreInventoryApp.Pages.Inventory
{
    public class PerformTransactionModel : BasePageModel
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;

        [BindProperty]
        [Required(ErrorMessage = "Please select a product")]
        public int ProductID { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [BindProperty]
        public string Notes { get; set; }

        public DataTable Products { get; set; } = new DataTable();
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public int? CreatedTransactionId { get; set; }

        public PerformTransactionModel(DbHelper db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public IActionResult OnGet()
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            LoadProducts();
            return Page();
        }

        public IActionResult OnPost()
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            LoadProducts();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                ErrorMessage = "User not logged in.";
                return Page();
            }

            try
            {
                // Get store ID and current quantities from inventory for this product
                string getInventoryQuery = @"
                    SELECT TOP 1 
                        StoreID,
                        ISNULL(QuantityStored, 0) as QuantityStored, 
                        ISNULL(QuantityOnHand, 0) as QuantityOnHand 
                    FROM Inventory 
                    WHERE ProductID = @ProductID 
                    ORDER BY StoreID";

                var inventoryParams = new SqlParameter[]
                {
                    new SqlParameter("@ProductID", ProductID)
                };

                var inventoryResult = _db.ExecuteQuery(getInventoryQuery, inventoryParams);

                if (inventoryResult.Rows.Count == 0)
                {
                    ErrorMessage = "No inventory found for this product in any store.";
                    return Page();
                }

                int storeId = Convert.ToInt32(inventoryResult.Rows[0]["StoreID"]);
                int storedQty = Convert.ToInt32(inventoryResult.Rows[0]["QuantityStored"]);
                int onHandQty = Convert.ToInt32(inventoryResult.Rows[0]["QuantityOnHand"]);

                if (Quantity > storedQty)
                {
                    ErrorMessage = $"Quantity exceeds warehouse (stored) amount. Only {storedQty} available in warehouse.";
                    return Page();
                }

                // Get available batch for this product/store
                int? batchId = null;
                string batchQuery = @"
                    SELECT TOP 1 BatchID 
                    FROM Batches 
                    WHERE ProductID = @ProductID 
                        AND StoreID = @StoreID 
                        AND Status = 'Active' 
                        AND RemainingQuantity >= @Qty
                    ORDER BY ExpiryDate";

                var batchParams = new SqlParameter[]
                {
                    new SqlParameter("@ProductID", ProductID),
                    new SqlParameter("@StoreID", storeId),
                    new SqlParameter("@Qty", Quantity)
                };

                var batchResult = _db.ExecuteScalar(batchQuery, batchParams);
                if (batchResult != null && batchResult != DBNull.Value)
                {
                    batchId = Convert.ToInt32(batchResult);
                }

                // BEGIN TRANSACTION LOGIC
                using (var connection = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Update Inventory - Decrease stored, increase on hand
                            string updateInvQuery = @"
                                UPDATE Inventory 
                                SET QuantityStored = QuantityStored - @Qty,
                                    QuantityOnHand = QuantityOnHand + @Qty,
                                    LastUpdated = GETDATE()
                                WHERE ProductID = @ProductID AND StoreID = @StoreID";

                            using (var updateCmd = new SqlCommand(updateInvQuery, connection, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@Qty", Quantity);
                                updateCmd.Parameters.AddWithValue("@ProductID", ProductID);
                                updateCmd.Parameters.AddWithValue("@StoreID", storeId);
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                if (rowsAffected == 0)
                                {
                                    transaction.Rollback();
                                    ErrorMessage = "Failed to update inventory. Inventory record not found.";
                                    return Page();
                                }
                            }

                            // 2. Update Batch if exists
                            if (batchId.HasValue)
                            {
                                string updateBatchQuery = @"
                                    UPDATE Batches 
                                    SET RemainingQuantity = RemainingQuantity - @Qty
                                    WHERE BatchID = @BatchID";

                                using (var batchUpdateCmd = new SqlCommand(updateBatchQuery, connection, transaction))
                                {
                                    batchUpdateCmd.Parameters.AddWithValue("@Qty", Quantity);
                                    batchUpdateCmd.Parameters.AddWithValue("@BatchID", batchId.Value);
                                    batchUpdateCmd.ExecuteNonQuery();
                                }
                            }

                            // 3. Create Inventory Transaction
                            string insertTransQuery = @"
                                INSERT INTO InventoryTransactions 
                                (TransactionType, ProductID, BatchID, Quantity, CreatedByID, Notes, CreatedAt)
                                OUTPUT INSERTED.TransactionID
                                VALUES (@TransactionType, @ProductID, @BatchID, @Qty, @UserID, @Notes, GETDATE())";

                            int transactionId = 0;
                            using (var transCmd = new SqlCommand(insertTransQuery, connection, transaction))
                            {
                                transCmd.Parameters.AddWithValue("@TransactionType", "Transfer");
                                transCmd.Parameters.AddWithValue("@ProductID", ProductID);
                                transCmd.Parameters.AddWithValue("@BatchID", batchId.HasValue ? (object)batchId.Value : DBNull.Value);
                                transCmd.Parameters.AddWithValue("@Qty", Quantity);
                                transCmd.Parameters.AddWithValue("@UserID", currentUserId.Value);
                                transCmd.Parameters.AddWithValue("@Notes", string.IsNullOrEmpty(Notes) ?
                                    $"Moved {Quantity} items from warehouse to shelf" : Notes);

                                transactionId = Convert.ToInt32(transCmd.ExecuteScalar());
                                CreatedTransactionId = transactionId;
                            }

                            transaction.Commit();

                            // Calculate new quantities
                            int newStoredQty = storedQty - Quantity;
                            int newOnHandQty = onHandQty + Quantity;

                            SuccessMessage = $"{Quantity} items moved from warehouse to shelf successfully! " +
                                          $"New quantities: Shelf={newOnHandQty}, Warehouse={newStoredQty}. " +
                                          $"Transaction ID: {transactionId}";

                            // Clear form
                            ProductID = 0;
                            Quantity = 0;
                            Notes = "";
                            ModelState.Clear();

                            // Reload products
                            LoadProducts();

                            return Page();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"Transaction failed: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                if (ex.InnerException != null)
                {
                    ErrorMessage += $" | Inner: {ex.InnerException.Message}";
                }
                return Page();
            }
        }

        public JsonResult OnGetGetQuantities(int productId)
        {
            try
            {
                // Get inventory data for this product
                string query = @"
                    SELECT TOP 1 
                        StoreID,
                        ISNULL(QuantityOnHand, 0) as QuantityOnHand, 
                        ISNULL(QuantityStored, 0) as QuantityStored,
                        (SELECT StoreName FROM Stores WHERE StoreID = i.StoreID) as StoreName
                    FROM Inventory i
                    WHERE ProductID = @ProductID 
                    ORDER BY StoreID";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@ProductID", productId)
                };

                var result = _db.ExecuteQuery(query, parameters);

                if (result.Rows.Count == 0)
                {
                    return new JsonResult(new
                    {
                        quantityOnHand = 0,
                        quantityStored = 0,
                        storeName = "No inventory found",
                        success = true
                    });
                }

                return new JsonResult(new
                {
                    quantityOnHand = Convert.ToInt32(result.Rows[0]["QuantityOnHand"]),
                    quantityStored = Convert.ToInt32(result.Rows[0]["QuantityStored"]),
                    storeId = Convert.ToInt32(result.Rows[0]["StoreID"]),
                    storeName = result.Rows[0]["StoreName"].ToString(),
                    success = true
                });
            }
            catch (Exception)
            {
                return new JsonResult(new
                {
                    quantityOnHand = 0,
                    quantityStored = 0,
                    success = false
                });
            }
        }

        private void LoadProducts()
        {
            // Load products that have warehouse stock
            string query = @"
                SELECT DISTINCT 
                    p.ProductID, 
                    p.ProductName, 
                    c.CategoryName,
                    ISNULL(p.Barcode, 'N/A') as Barcode,
                    s.StoreName,
                    i.StoreID
                FROM Products p
                LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                LEFT JOIN Inventory i ON p.ProductID = i.ProductID
                LEFT JOIN Stores s ON i.StoreID = s.StoreID
                WHERE ISNULL(i.QuantityStored, 0) > 0
                ORDER BY p.ProductName";

            Products = _db.ExecuteQuery(query);
        }
    }
}