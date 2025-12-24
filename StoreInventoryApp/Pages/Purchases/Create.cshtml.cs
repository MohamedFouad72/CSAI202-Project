using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Text.Json;

namespace StoreInventoryApp.Pages.Purchases
{
    public class CreateModel : BasePageModel
    {
        private readonly IConfiguration _configuration;

        public CreateModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Data for dropdowns
        public List<SupplierDto> Suppliers { get; set; } = new List<SupplierDto>();
        public List<StoreDto> Stores { get; set; } = new List<StoreDto>();
        public List<ProductDto> Products { get; set; } = new List<ProductDto>();

        public async Task<IActionResult> OnGetAsync()
        {
            // Check authentication
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            // Load data for dropdowns
            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Load suppliers
                string supplierQuery = "SELECT SupplierID, SupplierName FROM Suppliers ORDER BY SupplierName";
                using (SqlCommand command = new SqlCommand(supplierQuery, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Suppliers.Add(new SupplierDto
                        {
                            SupplierID = reader.GetInt32(0),
                            SupplierName = reader.GetString(1)
                        });
                    }
                }

                // Load stores
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

                // Load products
                string productQuery = "SELECT ProductID, ProductName, UnitCost FROM Products ORDER BY ProductName";
                using (SqlCommand command = new SqlCommand(productQuery, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Products.Add(new ProductDto
                        {
                            ProductID = reader.GetInt32(0),
                            ProductName = reader.GetString(1),
                            UnitCost = reader.GetDecimal(2)
                        });
                    }
                }
            }
        }

        public async Task<IActionResult> OnPostAsync(int SupplierID, int StoreID, string PaymentMethod, string PurchaseItemsJson)
        {
            // Check authentication
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            // Validate input
            if (SupplierID <= 0 || StoreID <= 0 || string.IsNullOrWhiteSpace(PurchaseItemsJson))
            {
                ModelState.AddModelError("", "Invalid purchase order data. Please select supplier, store, and add items.");
                await LoadDataAsync();
                return Page();
            }

            // Parse purchase items from JSON
            List<PurchaseItem> items;
            try
            {
                items = JsonSerializer.Deserialize<List<PurchaseItem>>(PurchaseItemsJson);
                if (items == null || items.Count == 0)
                {
                    ModelState.AddModelError("", "Please add at least one item to the purchase order.");
                    await LoadDataAsync();
                    return Page();
                }
            }
            catch
            {
                ModelState.AddModelError("", "Invalid purchase items data.");
                await LoadDataAsync();
                return Page();
            }

            // Process purchase order with transaction
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int? currentUserId = GetCurrentUserId();
                        if (!currentUserId.HasValue)
                        {
                            throw new Exception("User not authenticated");
                        }

                        // Calculate total amount
                        decimal totalAmount = items.Sum(item => item.Quantity * item.UnitCost);

                        // Generate invoice number
                        string invoiceNumber = "PO-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                        // 1. INSERT Purchase Invoice
                        string insertInvoiceQuery = @"
                            INSERT INTO Invoices (InvoiceNumber, InvoiceType, PaymentMethod, UserID, StoreID, SupplierID, TotalAmount, InvoiceDate)
                            OUTPUT INSERTED.InvoiceID
                            VALUES (@InvoiceNumber, 'Purchase', @PaymentMethod, @UserID, @StoreID, @SupplierID, @TotalAmount, GETDATE())";

                        int invoiceId;
                        using (SqlCommand command = new SqlCommand(insertInvoiceQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);
                            command.Parameters.AddWithValue("@PaymentMethod", PaymentMethod ?? "Cash");
                            command.Parameters.AddWithValue("@UserID", currentUserId.Value);
                            command.Parameters.AddWithValue("@StoreID", StoreID);
                            command.Parameters.AddWithValue("@SupplierID", SupplierID);
                            command.Parameters.AddWithValue("@TotalAmount", totalAmount);

                            invoiceId = (int)await command.ExecuteScalarAsync();
                        }

                        // 2. For each item: Insert line item, create batch, update inventory, record transaction
                        foreach (var item in items)
                        {
                            decimal subtotal = item.Quantity * item.UnitCost;

                            // 2a. Insert Invoice Line Item
                            string insertLineItemQuery = @"
                                INSERT INTO InvoiceLineItems (InvoiceID, ProductID, Quantity, UnitPrice, Subtotal, CostAtSale)
                                VALUES (@InvoiceID, @ProductID, @Quantity, @UnitPrice, @Subtotal, @CostAtSale)";

                            using (SqlCommand command = new SqlCommand(insertLineItemQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@InvoiceID", invoiceId);
                                command.Parameters.AddWithValue("@ProductID", int.Parse(item.ProductId));
                                command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                command.Parameters.AddWithValue("@UnitPrice", item.UnitCost);
                                command.Parameters.AddWithValue("@Subtotal", subtotal);
                                command.Parameters.AddWithValue("@CostAtSale", item.UnitCost);
                                await command.ExecuteNonQueryAsync();
                            }

                            // 2b. Create Batch
                            string insertBatchQuery = @"
                                INSERT INTO Batches (ProductID, StoreID, BatchNumber, ReceivedQuantity, RemainingQuantity, SupplierInvoiceID, ReceivedDate, Status)
                                OUTPUT INSERTED.BatchID
                                VALUES (@ProductID, @StoreID, @BatchNumber, @Quantity, @Quantity, @InvoiceID, GETDATE(), 'Active')";

                            int batchId;
                            string batchNumber = "BATCH-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + item.ProductId;

                            using (SqlCommand command = new SqlCommand(insertBatchQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ProductID", int.Parse(item.ProductId));
                                command.Parameters.AddWithValue("@StoreID", StoreID);
                                command.Parameters.AddWithValue("@BatchNumber", batchNumber);
                                command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                command.Parameters.AddWithValue("@InvoiceID", invoiceId);
                                batchId = (int)await command.ExecuteScalarAsync();
                            }

                            // 2c. Update or Insert Inventory
                            string updateInventoryQuery = @"
                                IF EXISTS (SELECT 1 FROM Inventory WHERE ProductID = @ProductID AND StoreID = @StoreID)
                                BEGIN
                                    UPDATE Inventory 
                                    SET QuantityOnHand = QuantityOnHand + @Quantity,
                                        LastUpdated = GETDATE()
                                    WHERE ProductID = @ProductID AND StoreID = @StoreID
                                END
                                ELSE
                                BEGIN
                                    INSERT INTO Inventory (ProductID, StoreID, QuantityOnHand, LastUpdated)
                                    VALUES (@ProductID, @StoreID, @Quantity, GETDATE())
                                END";

                            using (SqlCommand command = new SqlCommand(updateInventoryQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ProductID", int.Parse(item.ProductId));
                                command.Parameters.AddWithValue("@StoreID", StoreID);
                                command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                await command.ExecuteNonQueryAsync();
                            }

                            // 2d. Insert Inventory Transaction
                            string insertTransactionQuery = @"
                                INSERT INTO InventoryTransactions (TransactionType, ProductID, BatchID, Quantity, ReferenceID, CreatedByID, CreatedAt, Notes)
                                VALUES ('Receipt', @ProductID, @BatchID, @Quantity, @ReferenceID, @CreatedByID, GETDATE(), @Notes)";

                            using (SqlCommand command = new SqlCommand(insertTransactionQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ProductID", int.Parse(item.ProductId));
                                command.Parameters.AddWithValue("@BatchID", batchId);
                                command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                command.Parameters.AddWithValue("@ReferenceID", invoiceId);
                                command.Parameters.AddWithValue("@CreatedByID", currentUserId.Value);
                                command.Parameters.AddWithValue("@Notes", $"Purchase from supplier - Invoice #{invoiceNumber}");
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        // Commit transaction
                        transaction.Commit();
                        TempData["SuccessMessage"] = $"Purchase order {invoiceNumber} created successfully! Total: ${totalAmount:F2}";
                        return RedirectToPage("/Inventory/Index");
                    }
                    catch (Exception ex)
                    {
                        // Rollback transaction on error
                        transaction.Rollback();
                        ModelState.AddModelError("", "Error creating purchase order: " + ex.Message);
                        await LoadDataAsync();
                        return Page();
                    }
                }
            }
        }
    }

    // DTO Classes
    public class SupplierDto
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
    }

    public class StoreDto
    {
        public int StoreID { get; set; }
        public string StoreName { get; set; }
    }

    public class ProductDto
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class PurchaseItem
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }
}