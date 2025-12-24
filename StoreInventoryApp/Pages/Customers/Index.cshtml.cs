using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using StoreInventoryApp.Helpers;
using System.Data;
#nullable disable
namespace StoreInventoryApp.Pages.Customers
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;

        public DataTable CustomerList { get; set; }

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            string query = "SELECT * FROM Customers WHERE IsActive = 1 ORDER BY CreatedAt DESC";
            CustomerList = _db.ExecuteQuery(query);
        }

        public IActionResult OnPostDelete(int id)
        {
            try
            {
                // Soft Delete: Just mark IsActive = 0
                string query = "UPDATE Customers SET IsActive = 0 WHERE CustomerID = @ID";
                SqlParameter[] p = { new SqlParameter("@ID", id) };

                _db.ExecuteNonQuery(query, p);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting customer: " + ex.Message;
                return RedirectToPage();
            }
        }
    }
}