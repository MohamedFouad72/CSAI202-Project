using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Sales
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable InvoicesList { get; set; }

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            // Query: Get Sales Invoices with User and Customer names
            string query = @"
                SELECT inv.InvoiceID, inv.InvoiceNumber, inv.InvoiceDate, 
                       inv.TotalAmount, inv.PaymentMethod,
                       u.UserName, ISNULL(c.FullName, 'Walk-in Customer') as CustomerName
                FROM Invoices inv
                JOIN Users u ON inv.UserID = u.UserID
                LEFT JOIN Customers c ON inv.CustomerID = c.CustomerID
                WHERE inv.InvoiceType = 'Sale'
                ORDER BY inv.InvoiceDate DESC";

            InvoicesList = _db.ExecuteQuery(query);
        }
    }
}