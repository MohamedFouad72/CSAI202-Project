using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace StoreInventoryApp.Pages.Products
{
    public class EditModel : PageModel
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;

        public EditModel(IConfiguration config)
        {
            _config = config;
            _db = new DbHelper(config);
        }

        [BindProperty]
        public int ProductID { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Product name is required")]
        public string ProductName { get; set; }

        [BindProperty]
        public string? Barcode { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Category is required")]
        public int CategoryID { get; set; }

        [BindProperty]
        public string? Manufacturer { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Unit cost is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Unit cost must be positive")]
        public decimal UnitCost { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Unit price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Unit price must be positive")]
        public decimal UnitPrice { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Reorder level is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Reorder level must be positive")]
        public int ReorderLevel { get; set; }

        [BindProperty]
        public decimal OldPrice { get; set; }

        public DataTable Categories { get; set; }
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public IActionResult OnGet(int id)
        {
            LoadCategories();
            ProductID = id;

            // Load product details
            string query = @"SELECT ProductID, ProductName, Barcode, CategoryID, Manufacturer, 
                                    UnitCost, UnitPrice, ReorderLevel
                             FROM Products WHERE ProductID = @ProductID";

            SqlParameter[] parameters = { new SqlParameter("@ProductID", id) };
            DataTable result = _db.ExecuteQuery(query, parameters);

            if (result.Rows.Count == 0)
            {
                return RedirectToPage("./Index");
            }

            DataRow row = result.Rows[0];
            ProductName = row["ProductName"].ToString();
            Barcode = row["Barcode"]?.ToString();
            CategoryID = Convert.ToInt32(row["CategoryID"]);
            Manufacturer = row["Manufacturer"]?.ToString();
            UnitCost = Convert.ToDecimal(row["UnitCost"]);
            UnitPrice = Convert.ToDecimal(row["UnitPrice"]);
            OldPrice = UnitPrice; // Store original price
            ReorderLevel = Convert.ToInt32(row["ReorderLevel"]);

            return Page();
        }

        public IActionResult OnPost()
        {
            LoadCategories();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Validation: Unit Price must be >= Unit Cost
            if (UnitPrice < UnitCost)
            {
                ErrorMessage = "Unit Price must be greater than or equal to Unit Cost.";
                return Page();
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if barcode is unique (exclude current product)
                        if (!string.IsNullOrWhiteSpace(Barcode))
                        {
                            string checkBarcodeQuery = @"
                                SELECT COUNT(*) FROM Products 
                                WHERE Barcode = @Barcode AND ProductID != @ProductID";

                            using (SqlCommand checkCmd = new SqlCommand(checkBarcodeQuery, connection, transaction))
                            {
                                checkCmd.Parameters.AddWithValue("@Barcode", Barcode);
                                checkCmd.Parameters.AddWithValue("@ProductID", ProductID);
                                int count = (int)checkCmd.ExecuteScalar();
                                if (count > 0)
                                {
                                    ErrorMessage = "A product with this barcode already exists.";
                                    transaction.Rollback();
                                    return Page();
                                }
                            }
                        }

                        // Update product
                        string updateProductQuery = @"
                            UPDATE Products 
                            SET ProductName = @Name,
                                Barcode = @Barcode,
                                CategoryID = @CategoryID,
                                Manufacturer = @Manufacturer,
                                UnitCost = @Cost,
                                UnitPrice = @Price,
                                ReorderLevel = @ReorderLevel
                            WHERE ProductID = @ProductID";

                        using (SqlCommand cmd = new SqlCommand(updateProductQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ProductID", ProductID);
                            cmd.Parameters.AddWithValue("@Name", ProductName);
                            cmd.Parameters.AddWithValue("@Barcode", Barcode ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@CategoryID", CategoryID);
                            cmd.Parameters.AddWithValue("@Manufacturer", Manufacturer ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Cost", UnitCost);
                            cmd.Parameters.AddWithValue("@Price", UnitPrice);
                            cmd.Parameters.AddWithValue("@ReorderLevel", ReorderLevel);

                            cmd.ExecuteNonQuery();
                        }

                        // If price changed, insert into PriceHistory
                        if (UnitPrice != OldPrice)
                        {
                            int? userId = HttpContext.Session.GetInt32("UserID");

                            string insertPriceHistoryQuery = @"
                                INSERT INTO PriceHistory (ProductID, OldPrice, NewPrice, ChangedAt, ChangedByUserID)
                                VALUES (@ProductID, @OldPrice, @NewPrice, GETDATE(), @UserID)";

                            using (SqlCommand cmd = new SqlCommand(insertPriceHistoryQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ProductID", ProductID);
                                cmd.Parameters.AddWithValue("@OldPrice", OldPrice);
                                cmd.Parameters.AddWithValue("@NewPrice", UnitPrice);
                                cmd.Parameters.AddWithValue("@UserID", userId ?? (object)DBNull.Value);

                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();

                        TempData["SuccessMessage"] = "Product updated successfully!";
                        return RedirectToPage("./Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ErrorMessage = $"Error updating product: {ex.Message}";
                        return Page();
                    }
                }
            }
        }

       

        private void LoadCategories()
        {
            string query = "SELECT CategoryID, CategoryName FROM Categories ORDER BY CategoryName";
            Categories = _db.ExecuteQuery(query);
        }
    }
}