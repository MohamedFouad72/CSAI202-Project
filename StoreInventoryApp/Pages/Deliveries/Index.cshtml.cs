#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Deliveries
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable Deliveries { get; set; }

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
                return RedirectToPage("/Auth/Login");

            LoadDeliveries();
            return Page();
        }

        public IActionResult OnPostUpdateStatus(int deliveryId, string status)
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
                return RedirectToPage("/Auth/Login");

            string query = "UPDATE Deliveries SET Status = @Status, UpdatedAt = GETDATE() WHERE DeliveryID = @DeliveryID";
            _db.ExecuteNonQuery(query, new SqlParameter[] {
                new SqlParameter("@Status", status),
                new SqlParameter("@DeliveryID", deliveryId)
            });

            TempData["SuccessMessage"] = "Delivery status updated!";
            return RedirectToPage();
        }

        private void LoadDeliveries()
        {
            string query = @"
                SELECT 
                    d.DeliveryID,
                    i.InvoiceNumber,
                    c.FullName as CustomerName,
                    c.PhoneNumber,
                    c.Address,
                    d.Status,
                    d.AssignedToUserID,
                    u.UserName as AssignedTo,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.EstimatedDeliveryDate,
                    d.ActualDeliveryDate
                FROM Deliveries d
                LEFT JOIN Invoices i ON d.InvoiceID = i.InvoiceID
                LEFT JOIN Customers c ON i.CustomerID = c.CustomerID
                LEFT JOIN Users u ON d.AssignedToUserID = u.UserID
                ORDER BY d.CreatedAt DESC";

            Deliveries = _db.ExecuteQuery(query);
        }
    }
}