using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;
using Microsoft.Data.SqlClient;

namespace StoreInventoryApp.Pages.Products
{
    public class DetailsModel : PageModel
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;

        public DetailsModel(IConfiguration config)
        {
            _config = config;
            _db = new DbHelper(config);
        }

        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public string CategoryName { get; set; }
        public string Manufacturer { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }
        public int ReorderLevel { get; set; }

        public IActionResult OnGet(int id)
        {
            ProductID = id;
            string query = @"SELECT p.ProductID, p.ProductName, p.Barcode, 
                                    c.CategoryName, p.Manufacturer, p.UnitCost, p.UnitPrice, p.ReorderLevel
                             FROM Products p
                             JOIN Categories c ON p.CategoryID = c.CategoryID
                             WHERE ProductID = @ProductID";

            var dt = _db.ExecuteQuery(query, new SqlParameter[] { new SqlParameter("@ProductID", id) });

            if (dt.Rows.Count == 0)
                return RedirectToPage("./Index");

            var row = dt.Rows[0];
            ProductName = row["ProductName"].ToString();
            Barcode = row["Barcode"]?.ToString();
            CategoryName = row["CategoryName"].ToString();
            Manufacturer = row["Manufacturer"]?.ToString();
            UnitCost = Convert.ToDecimal(row["UnitCost"]);
            UnitPrice = Convert.ToDecimal(row["UnitPrice"]);
            ReorderLevel = Convert.ToInt32(row["ReorderLevel"]);

            return Page();
        }
    }
}
