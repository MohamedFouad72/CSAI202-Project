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
        public string StoreName { get; set; } = "HQ (Admin View)";

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public IActionResult OnGet()
        {
            // Check Auth
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            int? storeId = HttpContext.Session.GetInt32("StoreID");

            //[cite_start]// [cite: 260] Dashboard Logic
            // 1. Get Low Stock Count (Query C2 from Marawan's list logic)
            string lowStockQuery = @"SELECT COUNT(*) FROM Inventory i 
                                     JOIN Products p ON i.ProductID = p.ProductID 
                                     WHERE i.QuantityOnHand <= p.ReorderLevel";

            // 2. Get Total Products
            string prodQuery = "SELECT COUNT(*) FROM Products";

            if (storeId.HasValue)
            {
                lowStockQuery += " AND i.StoreID = " + storeId;
                StoreName = "Store #" + storeId; // In a real app, query the store name
            }

            LowStockCount = (int)_db.ExecuteScalar(lowStockQuery);
            TotalProducts = (int)_db.ExecuteScalar(prodQuery);

            return Page();
        }
    }
}