#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Alerts
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;
        public DataTable AlertsList { get; set; }

        public IndexModel(IConfiguration config)
        {
            _config = config;
            _db = new DbHelper(config);
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
                return RedirectToPage("/Auth/Login");

            LoadAlerts();
            return Page();
        }

        public IActionResult OnPostMarkAsRead(int alertId)
        {
            string query = "UPDATE Alerts SET IsRead = 1 WHERE AlertID = @AlertID";
            _db.ExecuteNonQuery(query, new SqlParameter[] { 
                new SqlParameter("@AlertID", alertId) 
            });

            TempData["SuccessMessage"] = "Alert marked as read";
            return RedirectToPage();
        }

        public IActionResult OnPostGenerateTestAlerts()
        {
            int? storeId = HttpContext.Session.GetInt32("StoreID") ?? 1;
            
            string query = @"
                INSERT INTO Alerts (AlertType, Title, Message, StoreID)
                VALUES 
                ('LowStock', 'Test Low Stock Alert', 'This is a test alert for low stock', @StoreID),
                ('Info', 'System Update', 'System maintenance scheduled for tomorrow', @StoreID)";

            _db.ExecuteNonQuery(query, new SqlParameter[] { 
                new SqlParameter("@StoreID", storeId) 
            });

            TempData["SuccessMessage"] = "Test alerts generated successfully!";
            return RedirectToPage();
        }

        private void LoadAlerts()
        {
            string query = @"
                SELECT 
                    AlertID,
                    AlertType,
                    Title,
                    Message,
                    CreatedAt,
                    IsRead,
                    CASE 
                        WHEN AlertType = 'LowStock' THEN 'danger'
                        WHEN AlertType = 'Expiry' THEN 'warning'
                        ELSE 'info'
                    END as AlertClass
                FROM Alerts 
                WHERE IsRead = 0 OR CreatedAt > DATEADD(DAY, -7, GETDATE())
                ORDER BY IsRead, CreatedAt DESC";

            AlertsList = _db.ExecuteQuery(query);
        }
    }
}