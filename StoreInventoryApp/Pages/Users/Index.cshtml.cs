using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;
using Microsoft.Data.SqlClient;

namespace StoreInventoryApp.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable UsersList { get; set; } = new DataTable();
        public string CurrentUserRole { get; set; } = string.Empty;
        public int? CurrentUserStoreId { get; set; }

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            CurrentUserRole = HttpContext.Session.GetString("UserRole") ?? string.Empty;
            CurrentUserStoreId = HttpContext.Session.GetInt32("StoreID");

            string query = @"
                SELECT 
                    u.UserID,
                    u.UserName,
                    u.UserRole,
                    u.UserEmail,
                    u.UserPhone,
                    u.IsActive,
                    u.CreatedAt,
                    s.StoreName,
                    e.Position,
                    e.HireDate
                FROM Users u
                LEFT JOIN Stores s ON u.StoreID = s.StoreID
                LEFT JOIN Employees e ON u.UserID = e.UserID
                WHERE 1=1
            ";

            // Non-admin users can only see users from their store
            if (CurrentUserRole != "Admin")
            {
                query += " AND u.StoreID = " + CurrentUserStoreId;
            }

            query += " ORDER BY u.CreatedAt DESC";

            UsersList = _db.ExecuteQuery(query);

            return Page();
        }

        public IActionResult OnPostToggleStatus(int userId)
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            // Check permissions
            CurrentUserRole = HttpContext.Session.GetString("UserRole") ?? string.Empty;
            if (CurrentUserRole != "Admin" && CurrentUserRole != "Manager")
            {
                TempData["Error"] = "You don't have permission to modify users.";
                return RedirectToPage();
            }

            try
            {
                string query = @"
                    UPDATE Users 
                    SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END
                    WHERE UserID = @UserID
                ";

                var parameters = new[] {
                    new Microsoft.Data.SqlClient.SqlParameter("@UserID", userId)
                };

                _db.ExecuteNonQuery(query, parameters);
                TempData["Success"] = "User status updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating user: {ex.Message}";
            }

            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int userId)
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            // Only Admin can delete users
            CurrentUserRole = HttpContext.Session.GetString("UserRole") ?? string.Empty;
            if (CurrentUserRole != "Admin")
            {
                TempData["Error"] = "Only Admin can delete users.";
                return RedirectToPage();
            }

            try
            {
                // Check if user has any transactions before deleting
                string checkQuery = @"
                    SELECT COUNT(*) FROM Invoices WHERE UserID = @UserID
                    UNION ALL
                    SELECT COUNT(*) FROM InventoryTransactions WHERE CreatedByID = @UserID
                ";

                var checkParams = new[] {
                    new Microsoft.Data.SqlClient.SqlParameter("@UserID", userId)
                };

                var result = _db.ExecuteQuery(checkQuery, checkParams);

                bool hasTransactions = false;
                foreach (DataRow row in result.Rows)
                {
                    if (Convert.ToInt32(row[0]) > 0)
                    {
                        hasTransactions = true;
                        break;
                    }
                }

                if (hasTransactions)
                {
                    TempData["Error"] = "Cannot delete user with transaction history.";
                    return RedirectToPage();
                }

                // Delete user (cascade will handle Employees table)
                string deleteQuery = "DELETE FROM Users WHERE UserID = @UserID";
                var deleteParams = new[] {
                    new Microsoft.Data.SqlClient.SqlParameter("@UserID", userId)
                };

                _db.ExecuteNonQuery(deleteQuery, deleteParams);
                TempData["Success"] = "User deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting user: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}