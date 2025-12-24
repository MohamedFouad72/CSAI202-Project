using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Suppliers
{
    public class EditModel : PageModel
    {
        private readonly DbHelper _db;

        [BindProperty]
        public SupplierInput Supplier { get; set; }

        public EditModel(DbHelper db)
        {
            _db = db;
        }

        public IActionResult OnGet(int id)
        {
            DataTable dt = _db.ExecuteQuery(
                "SELECT * FROM Suppliers WHERE SupplierID = @Id",
                new[] { new SqlParameter("@Id", id) }
            );

            if (dt.Rows.Count == 0)
                return RedirectToPage("Index");

            var r = dt.Rows[0];

            Supplier = new SupplierInput
            {
                SupplierID = (int)r["SupplierID"],
                SupplierName = r["SupplierName"].ToString(),
                SupplierEmail = r["SupplierEmail"]?.ToString(),
                SupplierPhone = r["SupplierPhone"].ToString(),
                SupplierAddress = r["SupplierAddress"]?.ToString(),
                SSN = r["SSN"]?.ToString()
            };

            return Page();
        }

        public IActionResult OnPost()
        {
            string query = @"
                UPDATE Suppliers
                SET SupplierName=@Name, SupplierEmail=@Email,
                    SupplierPhone=@Phone, SupplierAddress=@Address, SSN=@SSN
                WHERE SupplierID=@Id
            ";

            SqlParameter[] parameters =
            {
                new SqlParameter("@Name", Supplier.SupplierName),
                new SqlParameter("@Email", (object?)Supplier.SupplierEmail ?? DBNull.Value),
                new SqlParameter("@Phone", Supplier.SupplierPhone),
                new SqlParameter("@Address", (object?)Supplier.SupplierAddress ?? DBNull.Value),
                new SqlParameter("@SSN", (object?)Supplier.SSN ?? DBNull.Value),
                new SqlParameter("@Id", Supplier.SupplierID)
            };

            _db.ExecuteNonQuery(query, parameters);
            return RedirectToPage("Index");
        }

        public class SupplierInput
        {
            public int SupplierID { get; set; }
            public string SupplierName { get; set; }
            public string? SupplierEmail { get; set; }
            public string SupplierPhone { get; set; }
            public string? SupplierAddress { get; set; }
            public string? SSN { get; set; }
        }
    }
}
