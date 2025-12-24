using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
#nullable disable

namespace StoreInventoryApp.Pages.Customers
{
    public class CreateModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public CreateModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public CustomerInputModel Customer { get; set; }

        public class CustomerInputModel
        {
            [Required]
            public string FullName { get; set; }
            [Phone]
            public string PhoneNumber { get; set; }
            public string Address { get; set; }
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid) return Page();

            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection connection = new SqlConnection(connString))
            {
                try
                {
                    connection.Open();
                    string query = @"INSERT INTO Customers (FullName, PhoneNumber, Address, LoyaltyPoints, IsActive, CreatedAt)
                                     VALUES (@Name, @Phone, @Addr, 0, 1, GETDATE())";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", Customer.FullName);
                        command.Parameters.AddWithValue("@Phone", (object)Customer.PhoneNumber ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Addr", (object)Customer.Address ?? DBNull.Value);

                        command.ExecuteNonQuery();
                    }
                    return RedirectToPage("./Index");
                }
                catch (SqlException ex)
                {
                    ModelState.AddModelError("", "Database Error: " + ex.Message);
                    return Page();
                }
            }
        }
    }
}