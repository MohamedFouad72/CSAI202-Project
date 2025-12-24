using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;
#nullable disable
namespace StoreInventoryApp.Pages.Inventory
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable InventoryList { get; set; } = new();

        public IndexModel(DbHelper db)
        {
            _db = db;
        }

        public void OnGet()
        {
            // Query: Get Inventory with Status Logic
            string query = @"
                SELECT 
                    i.InventoryID,
                    p.ProductID, 
                    p.ProductName, 
                    c.CategoryName,
                    i.StoreID,
                    s.StoreName,
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
                LEFT JOIN Stores s ON i.StoreID = s.StoreID
                JOIN Categories c ON p.CategoryID = c.CategoryID
                WHERE i.StoreID IS NOT NULL
                ORDER BY StockStatus, p.ProductName";

            InventoryList = _db.ExecuteQuery(query);
        }

        // Handler for delete (called by ?handler=Delete&InventoryID=xx)
        public IActionResult OnPostDelete(int InventoryID)
        {
            try
            {
                if (InventoryID <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid inventory id.";
                    return RedirectToPage();
                }

                // Delete the inventory row
                string deleteQuery = "DELETE FROM Inventory WHERE InventoryID = @InventoryID";
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@InventoryID", InventoryID)
                };

                int rows = _db.ExecuteNonQuery(deleteQuery, parameters);

                if (rows > 0)
                {
                    TempData["SuccessMessage"] = "Inventory record deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Inventory record not found or could not be deleted.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting inventory: " + ex.Message;
            }

            return RedirectToPage();
        }
    }
}