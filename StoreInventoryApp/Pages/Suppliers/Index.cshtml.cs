using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Collections.Generic;

namespace StoreInventoryApp.Pages.Suppliers
{
    public class IndexModel : BasePageModel
    {
        private readonly IConfiguration _configuration;

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<Supplier> Suppliers { get; set; } = new List<Supplier>();

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is authenticated
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            await LoadSuppliersAsync();
            return Page();
        }

        private async Task LoadSuppliersAsync()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Query to get all suppliers
                string query = @"
                    SELECT SupplierID, SupplierName, SupplierEmail, SupplierPhone, SupplierAddress, SSN
                    FROM Suppliers
                    ORDER BY SupplierName";

                using (SqlCommand command = new SqlCommand(query, connection))
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        Suppliers.Add(new Supplier
                        {
                            SupplierID = reader.GetInt32(0),
                            SupplierName = reader.GetString(1),
                            SupplierEmail = reader.IsDBNull(2) ? null : reader.GetString(2),
                            SupplierPhone = reader.IsDBNull(3) ? null : reader.GetString(3),
                            SupplierAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                            SSN = reader.IsDBNull(5) ? null : reader.GetString(5)
                        });
                    }
                }
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            // Check if user is authenticated
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if supplier has any purchase orders
                    string checkQuery = @"
                        SELECT COUNT(*) 
                        FROM Invoices 
                        WHERE SupplierID = @SupplierID AND InvoiceType = 'Purchase'";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@SupplierID", id);
                        int count = (int)await checkCommand.ExecuteScalarAsync();

                        if (count > 0)
                        {
                            TempData["ErrorMessage"] = "Cannot delete supplier. This supplier has associated purchase orders.";
                            return RedirectToPage();
                        }
                    }

                    // Delete the supplier
                    string deleteQuery = "DELETE FROM Suppliers WHERE SupplierID = @SupplierID";
                    using (SqlCommand deleteCommand = new SqlCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.AddWithValue("@SupplierID", id);
                        await deleteCommand.ExecuteNonQueryAsync();
                    }

                    TempData["SuccessMessage"] = "Supplier deleted successfully!";
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547) // Foreign key constraint violation
                {
                    TempData["ErrorMessage"] = "Cannot delete supplier: record is referenced by other data";
                }
                else
                {
                    TempData["ErrorMessage"] = "Error deleting supplier: " + ex.Message;
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Unexpected error: " + ex.Message;
            }

            return RedirectToPage();
        }
    }

    // Supplier data model
    public class Supplier
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
        public string? SupplierEmail { get; set; }
        public string? SupplierPhone { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SSN { get; set; }
    }
}