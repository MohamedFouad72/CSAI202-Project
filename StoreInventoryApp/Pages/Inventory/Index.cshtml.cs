using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Inventory
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable InventoryList { get; set; }

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            // Query C14: Get Inventory with Status Logic
            // Note: We use ISNULL to handle cases where a product might not have an inventory row yet
            string query = @"
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

            InventoryList = _db.ExecuteQuery(query);
        }
    }
}