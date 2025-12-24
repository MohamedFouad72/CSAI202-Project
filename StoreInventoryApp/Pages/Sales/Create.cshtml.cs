using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;
#nullable disable
namespace StoreInventoryApp.Pages.Sales
{
    public class CreateModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public string ConnectionString { get; private set; }

        public DataTable ProductsList { get; set; }
        public DataTable CustomersList { get; set; }

        [BindProperty]
        public int? CustomerID { get; set; }

        [BindProperty]
        public string PaymentMethod { get; set; }

        [BindProperty]
        public List<CartItem> CartItems { get; set; }

        public class CartItem
        {
            public int ProductID { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        public CreateModel(IConfiguration configuration)
        {
            _configuration = configuration;
            ConnectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        public void OnGet()
        {
            DbHelper db = new DbHelper(_configuration);
            ProductsList = db.ExecuteQuery("SELECT * FROM Products WHERE IsDeleted = 0");
            CustomersList = db.ExecuteQuery("SELECT CustomerID, FullName FROM Customers WHERE IsActive = 1");
        }

        public IActionResult OnPost()
        {
            if (CartItems == null || CartItems.Count == 0)
            {
                ModelState.AddModelError("", "Cart is empty.");
                OnGet();
                return Page();
            }

            int userId = HttpContext.Session.GetInt32("UserID") ?? 1;
            int storeId = HttpContext.Session.GetInt32("StoreID") ?? 1;

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Calculate Totals
                        decimal totalAmount = CartItems.Sum(x => x.Quantity * x.UnitPrice);
                        decimal totalTax = totalAmount * 0.14m;

                        // 2. Insert Invoice Header
                        string invoiceQuery = @"
                            INSERT INTO Invoices (InvoiceNumber, InvoiceType, InvoiceDate, PaymentMethod, UserID, StoreID, CustomerID, TotalAmount, TotalTax) 
                            VALUES (@InvNum, 'Sale', GETDATE(), @PayMethod, @UserID, @StoreID, @CustID, @Total, @Tax);
                            SELECT SCOPE_IDENTITY();";

                        string invoiceNumber = "INV-" + DateTime.Now.Ticks.ToString().Substring(10);

                        SqlCommand cmdInv = new SqlCommand(invoiceQuery, connection, transaction);
                        cmdInv.Parameters.AddWithValue("@InvNum", invoiceNumber);
                        cmdInv.Parameters.AddWithValue("@PayMethod", PaymentMethod);
                        cmdInv.Parameters.AddWithValue("@UserID", userId);
                        cmdInv.Parameters.AddWithValue("@StoreID", storeId);
                        cmdInv.Parameters.AddWithValue("@CustID", (object)CustomerID ?? DBNull.Value);
                        cmdInv.Parameters.AddWithValue("@Total", totalAmount);
                        cmdInv.Parameters.AddWithValue("@Tax", totalTax);

                        int invoiceId = Convert.ToInt32(cmdInv.ExecuteScalar());

                        // 3. Process Line Items & Update Inventory
                        foreach (var item in CartItems)
                        {
                            // A. Insert Line Item
                            string lineQuery = @"INSERT INTO InvoiceLineItems (InvoiceID, ProductID, Quantity, UnitPrice, Subtotal) 
                                                 VALUES (@InvID, @ProdID, @Qty, @Price, @Sub)";
                            SqlCommand cmdLine = new SqlCommand(lineQuery, connection, transaction);
                            cmdLine.Parameters.AddWithValue("@InvID", invoiceId);
                            cmdLine.Parameters.AddWithValue("@ProdID", item.ProductID);
                            cmdLine.Parameters.AddWithValue("@Qty", item.Quantity);
                            cmdLine.Parameters.AddWithValue("@Price", item.UnitPrice);
                            cmdLine.Parameters.AddWithValue("@Sub", item.Quantity * item.UnitPrice);
                            cmdLine.ExecuteNonQuery();

                            // B. Update Inventory
                            string invUpdate = @"UPDATE Inventory SET QuantityOnHand = QuantityOnHand - @Qty 
                                                 WHERE ProductID = @ProdID AND StoreID = @StoreID";
                            SqlCommand cmdStock = new SqlCommand(invUpdate, connection, transaction);
                            cmdStock.Parameters.AddWithValue("@Qty", item.Quantity);
                            cmdStock.Parameters.AddWithValue("@ProdID", item.ProductID);
                            cmdStock.Parameters.AddWithValue("@StoreID", storeId);
                            cmdStock.ExecuteNonQuery();

                            // C. Transaction Log
                            string transQuery = @"INSERT INTO InventoryTransactions (TransactionType, ProductID, Quantity, ReferenceID, CreatedByID, Notes)
                                                  VALUES ('Sale', @ProdID, @Qty, @RefID, @User, 'POS Sale')";
                            SqlCommand cmdTrans = new SqlCommand(transQuery, connection, transaction);
                            cmdTrans.Parameters.AddWithValue("@ProdID", item.ProductID);
                            cmdTrans.Parameters.AddWithValue("@Qty", -item.Quantity);
                            cmdTrans.Parameters.AddWithValue("@RefID", invoiceId);
                            cmdTrans.Parameters.AddWithValue("@User", userId);
                            cmdTrans.ExecuteNonQuery();
                        }

                        // 4. Update Loyalty Points
                        if (CustomerID.HasValue)
                        {
                            int points = (int)(totalAmount / 10);
                            string custUpdate = "UPDATE Customers SET LoyaltyPoints = LoyaltyPoints + @Pts WHERE CustomerID = @CustID";
                            SqlCommand cmdCust = new SqlCommand(custUpdate, connection, transaction);
                            cmdCust.Parameters.AddWithValue("@Pts", points);
                            cmdCust.Parameters.AddWithValue("@CustID", CustomerID);
                            cmdCust.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return RedirectToPage("/Sales/Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "Transaction Failed: " + ex.Message);
                        OnGet();
                        return Page();
                    }
                }
            }
        }
    }
}