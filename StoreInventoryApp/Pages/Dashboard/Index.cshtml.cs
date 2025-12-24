using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Dashboard
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int CustomerCount { get; set; }
        public int TodaySalesCount { get; set; }
        public string StoreName { get; set; } = "HQ (Admin View)";
        
        public List<RecentSale> RecentSales { get; set; } = new();

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            int? storeId = HttpContext.Session.GetInt32("StoreID");

            // Get all statistics
            TotalProducts = GetTotalProductsCount();
            LowStockCount = GetLowStockCount(storeId);
            CustomerCount = GetCustomerCount();
            TodaySalesCount = GetTodaySalesCount(storeId);
            RecentSales = GetRecentSales(storeId);

            if (storeId.HasValue)
            {
                StoreName = $"Store #{storeId}";
            }

            return Page();
        }

        private int GetTotalProductsCount()
        {
            string query = "SELECT COUNT(*) FROM Products";
            var result = _db.ExecuteScalar(query);
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private int GetLowStockCount(int? storeId)
        {
            string query = @"SELECT COUNT(*) FROM Inventory i 
                             JOIN Products p ON i.ProductID = p.ProductID 
                             WHERE i.QuantityOnHand <= p.ReorderLevel";
            
            if (storeId.HasValue)
            {
                query += " AND i.StoreID = " + storeId.Value;
            }
            
            var result = _db.ExecuteScalar(query);
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private int GetCustomerCount()
        {
            string query = "SELECT COUNT(*) FROM Customers WHERE IsActive = 1";
            var result = _db.ExecuteScalar(query);
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private int GetTodaySalesCount(int? storeId)
        {
            string query = @"SELECT COUNT(*) FROM Invoices 
                             WHERE InvoiceType = 'Sale' 
                             AND CAST(InvoiceDate AS DATE) = CAST(GETDATE() AS DATE)";
            
            if (storeId.HasValue)
            {
                query += " AND StoreID = " + storeId.Value;
            }
            
            var result = _db.ExecuteScalar(query);
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private List<RecentSale> GetRecentSales(int? storeId)
        {
            var recentSales = new List<RecentSale>();
            
            string query = @"
                SELECT TOP 5 
                    inv.InvoiceNumber,
                    inv.TotalAmount,
                    inv.InvoiceDate,
                    ISNULL(c.FullName, 'Walk-in Customer') as CustomerName
                FROM Invoices inv
                LEFT JOIN Customers c ON inv.CustomerID = c.CustomerID
                WHERE inv.InvoiceType = 'Sale'
            ";
            
            if (storeId.HasValue)
            {
                query += " AND inv.StoreID = " + storeId.Value;
            }
            
            query += " ORDER BY inv.InvoiceDate DESC";
            
            DataTable result = _db.ExecuteQuery(query);
            
            foreach (DataRow row in result.Rows)
            {
                recentSales.Add(new RecentSale
                {
                    InvoiceNumber = row["InvoiceNumber"].ToString(),
                    TotalAmount = Convert.ToDecimal(row["TotalAmount"]),
                    InvoiceDate = Convert.ToDateTime(row["InvoiceDate"]),
                    CustomerName = row["CustomerName"].ToString()
                });
            }
            
            return recentSales;
        }
    }

    public class RecentSale
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
    }
}