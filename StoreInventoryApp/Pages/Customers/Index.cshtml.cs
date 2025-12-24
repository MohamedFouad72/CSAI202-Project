using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Customers
{
    public class IndexModel : PageModel
    {
        private readonly DbHelper _db;
        public DataTable CustomerList { get; set; } = new();

        public IndexModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public void OnGet()
        {
            string query = "SELECT * FROM Customers ORDER BY CreatedAt DESC";
            CustomerList = _db.ExecuteQuery(query);
        }
    }
}