using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;

        public DataTable ProductsList { get; set; } = new DataTable();

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            // Only show non-deleted products
            string query = @"SELECT p.ProductID, p.ProductName, p.Barcode, 
                                    c.CategoryName, p.UnitPrice
                             FROM Products p
                             JOIN Categories c ON p.CategoryID = c.CategoryID
                             WHERE p.IsDeleted = 0
                             ORDER BY p.ProductName";

            ProductsList = _db.ExecuteQuery(query) ?? new DataTable();
        }

        // Soft delete handler
        public IActionResult OnPostDelete(int ProductID)
        {
            string query = "UPDATE Products SET IsDeleted = 1 WHERE ProductID = @ProductID";
            _db.ExecuteNonQuery(query, new SqlParameter[] { new SqlParameter("@ProductID", ProductID) });

            TempData["SuccessMessage"] = "Product deleted successfully!";
            return RedirectToPage();
        }
    }
}