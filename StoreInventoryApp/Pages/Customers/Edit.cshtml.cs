using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers; // Assuming DbHelper namespace
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace StoreInventoryApp.Pages.Customers
{
    public class EditModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public EditModel(IConfiguration configuration) => _configuration = configuration;

        [BindProperty]
        public CustomerInputModel Customer { get; set; }

        public class CustomerInputModel
        {
            public int CustomerID { get; set; }
            [Required]
            public string FullName { get; set; }
            [Phone]
            public string PhoneNumber { get; set; }
            public string Address { get; set; }
        }

        public IActionResult OnGet(int id)
        {
            DbHelper db = new DbHelper(_configuration);
            string query = "SELECT * FROM Customers WHERE CustomerID = @ID";
            SqlParameter[] p = { new SqlParameter("@ID", id) };
            DataTable dt = db.ExecuteQuery(query, p);

            if (dt.Rows.Count == 0) return NotFound();

            DataRow row = dt.Rows[0];
            Customer = new CustomerInputModel
            {
                CustomerID = (int)row["CustomerID"],
                FullName = row["FullName"].ToString(),
                PhoneNumber = row["PhoneNumber"]?.ToString(),
                Address = row["Address"]?.ToString()
            };

            return Page();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid) return Page();

            string query = @"UPDATE Customers 
                             SET FullName = @Name, PhoneNumber = @Phone, Address = @Addr, UpdatedAt = GETDATE()
                             WHERE CustomerID = @ID";

            try
            {
                DbHelper db = new DbHelper(_configuration);
                SqlParameter[] p = {
                    new SqlParameter("@Name", Customer.FullName),
                    new SqlParameter("@Phone", (object)Customer.PhoneNumber ?? DBNull.Value),
                    new SqlParameter("@Addr", (object)Customer.Address ?? DBNull.Value),
                    new SqlParameter("@ID", Customer.CustomerID)
                };

                db.ExecuteNonQuery(query, p);
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Update failed: " + ex.Message);
                return Page();
            }
        }
    }
}