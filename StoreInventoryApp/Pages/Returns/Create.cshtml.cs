using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace StoreInventoryApp.Pages.Returns
{
    public class CreateModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public CreateModel(IConfiguration configuration) => _configuration = configuration;

        [BindProperty]
        public int InvoiceID { get; set; }

        [BindProperty]
        public int ProductID { get; set; }

        [BindProperty]
        public int ReturnQuantity { get; set; }

        [BindProperty]
        public string Reason { get; set; }

        public IActionResult OnPost()
        {
            int userId = HttpContext.Session.GetInt32("UserID") ?? 0;
            int storeId = HttpContext.Session.GetInt32("StoreID") ?? 1;

            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Get Original Price for Refund Calculation
                        string priceQuery = "SELECT UnitPrice FROM InvoiceLineItems WHERE InvoiceID = @InvID AND ProductID = @ProdID";
                        SqlCommand cmdPrice = new SqlCommand(priceQuery, connection, transaction);
                        cmdPrice.Parameters.AddWithValue("@InvID", InvoiceID);
                        cmdPrice.Parameters.AddWithValue("@ProdID", ProductID);

                        object result = cmdPrice.ExecuteScalar();
                        if (result == null) throw new Exception("Original purchase not found.");

                        decimal unitPrice = (decimal)result;
                        decimal refundAmount = unitPrice * ReturnQuantity;

                        // 2. Insert Return Header
                        string retHeader = @"INSERT INTO Returns (InvoiceID, ReturnDate, TotalRefundAmount, Reason, ProcessedByUserID)
                                             VALUES (@InvID, GETDATE(), @Amt, @Reason, @User);
                                             SELECT SCOPE_IDENTITY();";
                        SqlCommand cmdHead = new SqlCommand(retHeader, connection, transaction);
                        cmdHead.Parameters.AddWithValue("@InvID", InvoiceID);
                        cmdHead.Parameters.AddWithValue("@Amt", refundAmount);
                        cmdHead.Parameters.AddWithValue("@Reason", Reason);
                        cmdHead.Parameters.AddWithValue("@User", userId);

                        int returnId = Convert.ToInt32(cmdHead.ExecuteScalar());

                        // 3. Insert Return Line Item
                        string retLine = @"INSERT INTO ReturnLineItems (ReturnID, ProductID, QuantityReturned, RefundAmount)
                                           VALUES (@RetID, @ProdID, @Qty, @Amt)";
                        SqlCommand cmdLine = new SqlCommand(retLine, connection, transaction);
                        cmdLine.Parameters.AddWithValue("@RetID", returnId);
                        cmdLine.Parameters.AddWithValue("@ProdID", ProductID);
                        cmdLine.Parameters.AddWithValue("@Qty", ReturnQuantity);
                        cmdLine.Parameters.AddWithValue("@Amt", refundAmount);
                        cmdLine.ExecuteNonQuery();

                        // 4. Restore Inventory
                        string invUpd = "UPDATE Inventory SET QuantityOnHand = QuantityOnHand + @Qty WHERE ProductID=@ProdID AND StoreID=@Store";
                        SqlCommand cmdInv = new SqlCommand(invUpd, connection, transaction);
                        cmdInv.Parameters.AddWithValue("@Qty", ReturnQuantity);
                        cmdInv.Parameters.AddWithValue("@ProdID", ProductID);
                        cmdInv.Parameters.AddWithValue("@Store", storeId);
                        cmdInv.ExecuteNonQuery();

                        // 5. Log Transaction
                        string logTrans = @"INSERT INTO InventoryTransactions (TransactionType, ProductID, Quantity, ReferenceID, CreatedByID, Notes)
                                            VALUES ('Return', @ProdID, @Qty, @Ref, @User, 'Customer Return')";
                        SqlCommand cmdLog = new SqlCommand(logTrans, connection, transaction);
                        cmdLog.Parameters.AddWithValue("@ProdID", ProductID);
                        cmdLog.Parameters.AddWithValue("@Qty", ReturnQuantity); // Positive because it's coming back
                        cmdLog.Parameters.AddWithValue("@Ref", returnId);
                        cmdLog.Parameters.AddWithValue("@User", userId);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();
                        return RedirectToPage("/Sales/Index", new { message = "Return processed." });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "Return Failed: " + ex.Message);
                        return Page();
                    }
                }
            }
        }
    }
}