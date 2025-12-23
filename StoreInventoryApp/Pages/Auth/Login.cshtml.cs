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

        public LoginModel(IConfiguration config)
        {
            _db = new DbHelper(config);
        }

        [BindProperty]
        public string UserName { get; set; }
        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet()
        {
            // If already logged in, redirect to Dashboard
            if (HttpContext.Session.GetInt32("UserID") != null)
            {
                Response.Redirect("/Dashboard/Index");
            }
        }

        public IActionResult OnPost()
        {
            //[cite_start]// [cite: 136] Marawan's Query A1: User Login
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
                DataRow row = result.Rows[0];

                //[cite_start]// [cite: 298] Set Session
                HttpContext.Session.SetInt32("UserID", (int)row["UserID"]);
                HttpContext.Session.SetString("UserName", row["UserName"].ToString());
                HttpContext.Session.SetString("UserRole", row["UserRole"].ToString());

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