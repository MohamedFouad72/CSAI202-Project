using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Customers
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        private IConfiguration _configuration;

        public DataTable CustomerList { get; set; }

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            string query = "SELECT * FROM Customers ORDER BY CreatedAt DESC";
            CustomerList = _db.ExecuteQuery(query);
        }

        // Add this method to your existing IndexModel class
        public IActionResult OnPostDelete(int id)
        {
            try
            {
                DbHelper db = new DbHelper(_configuration);
                // Soft Delete: Just mark IsActive = 0
                string query = "UPDATE Customers SET IsActive = 0 WHERE CustomerID = @ID";
                SqlParameter[] p = { new SqlParameter("@ID", id) };

                db.ExecuteNonQuery(query, p);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                // In a real app, you'd pass an error message to the view
                return RedirectToPage();
            }
        }
    }
}