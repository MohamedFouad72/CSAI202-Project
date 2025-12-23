using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace StoreInventoryApp.Pages.Products
{
    public class CreateModel : PageModel
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;

        public CreateModel(IConfiguration config)
        {
            _config = config;
            _db = new DbHelper(config);
        }

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
        public int ReorderLevel { get; set; } = 10;

        public DataTable Categories { get; set; }
        public string ErrorMessage { get; set; }

        public void OnGet()
        {
            LoadCategories();
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
                        // Generate barcode if empty
                        if (string.IsNullOrWhiteSpace(Barcode))
                        {
                            Barcode = GenerateBarcode();
                        }

                        // Check if barcode already exists
                        string checkBarcodeQuery = "SELECT COUNT(*) FROM Products WHERE Barcode = @Barcode";
                        using (SqlCommand checkCmd = new SqlCommand(checkBarcodeQuery, connection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@Barcode", Barcode);
                            int count = (int)checkCmd.ExecuteScalar();
                            if (count > 0)
                            {
                                ErrorMessage = "A product with this barcode already exists.";
                                transaction.Rollback();
                                return Page();
                            }
                        }

                        // 1. Insert into Products table
                        string insertProductQuery = @"
                            INSERT INTO Products (ProductName, Barcode, CategoryID, Manufacturer, 
                                                  UnitCost, UnitPrice, ReorderLevel)
                            VALUES (@Name, @Barcode, @CategoryID, @Manufacturer, 
                                    @Cost, @Price, @ReorderLevel);
                            SELECT SCOPE_IDENTITY();";

                        int newProductID;
                        using (SqlCommand cmd = new SqlCommand(insertProductQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Name", ProductName);
                            cmd.Parameters.AddWithValue("@Barcode", Barcode ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@CategoryID", CategoryID);
                            cmd.Parameters.AddWithValue("@Manufacturer", Manufacturer ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Cost", UnitCost);
                            cmd.Parameters.AddWithValue("@Price", UnitPrice);
                            cmd.Parameters.AddWithValue("@ReorderLevel", ReorderLevel);

                            newProductID = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // 2. Get all stores
                        string getStoresQuery = "SELECT StoreID FROM Stores";
                        DataTable stores = new DataTable();
                        using (SqlCommand cmd = new SqlCommand(getStoresQuery, connection, transaction))
                        {
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                adapter.Fill(stores);
                            }
                        }

                        // 3. Insert inventory record for each store (QuantityOnHand = 0)
                        string insertInventoryQuery = @"
                            INSERT INTO Inventory (ProductID, StoreID, QuantityOnHand, QuantityStored, LastUpdated)
                            VALUES (@ProductID, @StoreID, 0, 0, GETDATE())";

                        foreach (DataRow storeRow in stores.Rows)
                        {
                            using (SqlCommand cmd = new SqlCommand(insertInventoryQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ProductID", newProductID);
                                cmd.Parameters.AddWithValue("@StoreID", storeRow["StoreID"]);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();

                        TempData["SuccessMessage"] = "Product created successfully!";
                        return RedirectToPage("./Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ErrorMessage = $"Error creating product: {ex.Message}";
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

        private string GenerateBarcode()
        {
            // Generate a simple barcode based on timestamp
            return DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }
    }
}