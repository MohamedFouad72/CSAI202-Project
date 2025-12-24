using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Sales
{
    public class CreateModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable ProductsList { get; set; } = new();
        public DataTable CustomersList { get; set; } = new();

        public CreateModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            ProductsList = _db.ExecuteQuery("SELECT * FROM Products ORDER BY ProductName");
            CustomersList = _db.ExecuteQuery("SELECT CustomerID, FullName FROM Customers ORDER BY FullName");
        }
    }
}