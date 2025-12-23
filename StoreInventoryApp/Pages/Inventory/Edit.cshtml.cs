using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace StoreInventoryApp.Pages.Inventory
{
    public class EditModel : BasePageModel
    {
        private readonly DbHelper _db;

        public EditModel(DbHelper db)
        {
            _db = db;
        }

        // Keys
        [BindProperty]
        public int ProductID { get; set; }

        [BindProperty]
        public int StoreID { get; set; }

        // Editable fields
        [BindProperty]
        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }

        [BindProperty]
        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityStored { get; set; }

        [BindProperty]
        public string? AdjustmentReason { get; set; }

        // Display-only
        public string ProductName { get; set; } = "";
        public string StoreName { get; set; } = "";

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        // ===================== GET =====================
        public IActionResult OnGet(int productId, int storeId)
        {
            var auth = CheckAuthentication();
            if (auth != null) return auth;

            ProductID = productId;
            StoreID = storeId;

            LoadInventory();
            return Page();
        }

        // ===================== POST =====================
        public IActionResult OnPost()
        {
            var auth = CheckAuthentication();
            if (auth != null) return auth;

            if (!ModelState.IsValid)
            {
                LoadInventory(loadQuantities: false);
                return Page();
            }

            try
            {
                string update = @"
                    UPDATE Inventory
                    SET QuantityOnHand = @OnHand,
                        QuantityStored = @Stored,
                        LastUpdated = GETDATE()
                    WHERE ProductID = @ProductID AND StoreID = @StoreID";

                var parameters = new[]
                {
                    new SqlParameter("@OnHand", QuantityOnHand),
                    new SqlParameter("@Stored", QuantityStored),
                    new SqlParameter("@ProductID", ProductID),
                    new SqlParameter("@StoreID", StoreID)
                };

                int rows = _db.ExecuteNonQuery(update, parameters);

                if (rows == 0)
                {
                    ErrorMessage = "Inventory record not found.";
                    LoadInventory(loadQuantities: false);
                    return Page();
                }

                SuccessMessage = "Inventory updated successfully.";
                LoadInventory(loadQuantities: false);
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                LoadInventory(loadQuantities: false);
                return Page();
            }
        }

        // ===================== HELPERS =====================
        private void LoadInventory(bool loadQuantities = true)
        {
            string query = @"
                SELECT 
                    p.ProductName,
                    s.StoreName,
                    i.QuantityOnHand,
                    i.QuantityStored
                FROM Inventory i
                JOIN Products p ON p.ProductID = i.ProductID
                JOIN Stores s ON s.StoreID = i.StoreID
                WHERE i.ProductID = @ProductID AND i.StoreID = @StoreID";

            var result = _db.ExecuteQuery(query, new[]
            {
                new SqlParameter("@ProductID", ProductID),
                new SqlParameter("@StoreID", StoreID)
            });

            if (result.Rows.Count == 0)
            {
                ErrorMessage = "Inventory record not found.";
                return;
            }

            var row = result.Rows[0];
            ProductName = row["ProductName"].ToString()!;
            StoreName = row["StoreName"].ToString()!;

            if (loadQuantities)
            {
                QuantityOnHand = Convert.ToInt32(row["QuantityOnHand"]);
                QuantityStored = Convert.ToInt32(row["QuantityStored"]);
            }
        }
    }
}
