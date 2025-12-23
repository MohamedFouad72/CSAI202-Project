using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;
using Microsoft.Data.SqlClient;

namespace StoreInventoryApp.Pages.Products
{
    public class DeletedModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable DeletedProducts { get; set; } = new DataTable();

        public DeletedModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            string query = @"SELECT p.ProductID, p.ProductName, p.Barcode, 
                                    c.CategoryName, p.UnitPrice
                             FROM Products p
                             JOIN Categories c ON p.CategoryID = c.CategoryID
                             WHERE p.IsDeleted = 1
                             ORDER BY p.ProductName";
            DeletedProducts = _db.ExecuteQuery(query) ?? new DataTable();
        }

        public IActionResult OnPostUndelete(int ProductID)
        {
            string query = "UPDATE Products SET IsDeleted = 0 WHERE ProductID = @ProductID";
            _db.ExecuteNonQuery(query, new SqlParameter[] { new SqlParameter("@ProductID", ProductID) });

            TempData["SuccessMessage"] = "Product restored successfully!";
            return RedirectToPage();
        }
    }
}

