using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.Data;

namespace StoreInventoryApp.Pages.Auth
{
    public class LoginModel : PageModel
    {
        private readonly DbHelper _db;
        public string? ErrorMessage { get; set; }

        [BindProperty]
        public string UserName { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public LoginModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserID") != null)
            {
                return RedirectToPage("/Dashboard/Index");
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            string query = @"SELECT UserID, UserName, UserRole, UserEmail, StoreID 
                             FROM Users 
                             WHERE UserName = @User AND Password = @Pass AND IsActive = 1";

            SqlParameter[] parameters = {
                new SqlParameter("@User", UserName),
                new SqlParameter("@Pass", Password)
            };

            DataTable result = _db.ExecuteQuery(query, parameters);

            if (result.Rows.Count > 0)
            {
                var row = result.Rows[0];

                HttpContext.Session.SetInt32("UserID", (int)row["UserID"]);
                HttpContext.Session.SetString("UserName", row["UserName"].ToString()!);
                HttpContext.Session.SetString("UserRole", row["UserRole"].ToString()!);

                if (row["StoreID"] != DBNull.Value)
                {
                    HttpContext.Session.SetInt32("StoreID", (int)row["StoreID"]);
                }

                return RedirectToPage("/Dashboard/Index");
            }
            else
            {
                ErrorMessage = "Invalid Username or Password";
                return Page();
            }
        }
    }
}