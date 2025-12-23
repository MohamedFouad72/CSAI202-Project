using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;

namespace StoreInventoryApp.Pages.Dashboard
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public IndexModel(IConfiguration configuration) => _configuration = configuration;

        // --- ADDED MISSING PROPERTY ---
        public string StoreName { get; set; }

        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int DailySalesCount { get; set; }
        public decimal DailyRevenue { get; set; }
        public int ActiveCustomers { get; set; }

        public void OnGet()
        {
            // --- POPULATE STORE NAME ---
            // It tries to grab the name from the session (set during login), 
            // otherwise defaults to "Store Overview"
            StoreName = HttpContext.Session.GetString("StoreName") ?? "Store Overview";

            DbHelper db = new DbHelper(_configuration);

            // 1. Total Products
            TotalProducts = (int)db.ExecuteScalar("SELECT COUNT(*) FROM Products");

            // 2. Low Stock
            string lowStockQuery = @"SELECT COUNT(*) FROM Inventory i 
                                     JOIN Products p ON i.ProductID = p.ProductID 
                                     WHERE i.QuantityOnHand <= p.ReorderLevel";
            LowStockCount = (int)db.ExecuteScalar(lowStockQuery);

            // 3. Daily Sales & Revenue
            string salesQuery = @"SELECT COUNT(*) FROM Invoices 
                                  WHERE InvoiceType='Sale' AND CAST(InvoiceDate AS DATE) = CAST(GETDATE() AS DATE)";
            DailySalesCount = (int)db.ExecuteScalar(salesQuery);

            string revQuery = @"SELECT ISNULL(SUM(TotalAmount), 0) FROM Invoices 
                                WHERE InvoiceType='Sale' AND CAST(InvoiceDate AS DATE) = CAST(GETDATE() AS DATE)";
            DailyRevenue = (decimal)db.ExecuteScalar(revQuery);

            // 4. Active Customers
            ActiveCustomers = (int)db.ExecuteScalar("SELECT COUNT(*) FROM Customers WHERE IsActive = 1");
        }
    }
}