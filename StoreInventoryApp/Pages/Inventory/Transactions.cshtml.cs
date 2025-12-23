using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Inventory
{
    public class TransactionsModel : BasePageModel
    {
        private readonly DbHelper _db;
        public DataTable Transactions { get; set; } = new DataTable();
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ProductFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? TypeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateTo { get; set; }

        public DataTable Products { get; set; } = new DataTable();

        public TransactionsModel(DbHelper db)
        {
            _db = db;
        }

        public IActionResult OnGet()
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            LoadProducts();
            LoadTransactions();
            return Page();
        }

        private void LoadTransactions()
        {
            try
            {
                string query = @"
                    SELECT 
                        t.TransactionID,
                        t.TransactionType,
                        t.ProductID,
                        p.ProductName,
                        t.BatchID,
                        b.BatchNumber,
                        t.Quantity,
                        u.UserName as CreatedBy,
                        t.CreatedAt,
                        t.Notes,
                        s.StoreName,
                        i.StoreID
                    FROM InventoryTransactions t
                    LEFT JOIN Products p ON t.ProductID = p.ProductID
                    LEFT JOIN Batches b ON t.BatchID = b.BatchID
                    LEFT JOIN Users u ON t.CreatedByID = u.UserID
                    LEFT JOIN Inventory i ON t.ProductID = i.ProductID
                    LEFT JOIN Stores s ON i.StoreID = s.StoreID
                    WHERE 1=1";

                var parameters = new List<SqlParameter>();

                // Apply filters
                if (ProductFilter.HasValue && ProductFilter > 0)
                {
                    query += " AND t.ProductID = @ProductID";
                    parameters.Add(new SqlParameter("@ProductID", ProductFilter.Value));
                }

                if (!string.IsNullOrEmpty(TypeFilter) && TypeFilter != "All")
                {
                    query += " AND t.TransactionType = @TransactionType";
                    parameters.Add(new SqlParameter("@TransactionType", TypeFilter));
                }

                if (DateFrom.HasValue)
                {
                    query += " AND t.CreatedAt >= @DateFrom";
                    parameters.Add(new SqlParameter("@DateFrom", DateFrom.Value.Date));
                }

                if (DateTo.HasValue)
                {
                    query += " AND t.CreatedAt <= @DateTo";
                    parameters.Add(new SqlParameter("@DateTo", DateTo.Value.Date.AddDays(1).AddSeconds(-1)));
                }

                query += " ORDER BY t.CreatedAt DESC";

                Transactions = _db.ExecuteQuery(query, parameters.ToArray());
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading transactions: {ex.Message}";
                Transactions = new DataTable();
            }
        }

        private void LoadProducts()
        {
            string query = @"
                SELECT ProductID, ProductName 
                FROM Products 
                ORDER BY ProductName";

            Products = _db.ExecuteQuery(query);
        }

        public IActionResult OnPostDelete(int id)
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            try
            {
                string query = "DELETE FROM InventoryTransactions WHERE TransactionID = @TransactionID";
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@TransactionID", id)
                };

                int rowsAffected = _db.ExecuteNonQuery(query, parameters);

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Transaction deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Transaction not found or could not be deleted.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting transaction: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}