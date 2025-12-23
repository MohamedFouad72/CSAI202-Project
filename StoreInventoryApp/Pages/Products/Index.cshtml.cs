using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable ProductsList { get; set; }

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            // Query B1: Get All Products
            string query = @"SELECT p.ProductID, p.ProductName, p.Barcode, 
                                    c.CategoryName, p.Manufacturer, 
                                    p.UnitPrice, p.ReorderLevel 
                             FROM Products p
                             JOIN Categories c ON p.CategoryID = c.CategoryID
                             ORDER BY p.ProductName";

            ProductsList = _db.ExecuteQuery(query);
        }
    }
}