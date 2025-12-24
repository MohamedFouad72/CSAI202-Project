#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using StoreInventoryApp.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace StoreInventoryApp.Pages.Auth
{
    public class LoginModel : PageModel
    {
        private readonly DbHelper _db;
        public string ErrorMessage { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Username is required")]
        public string UserName { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

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
            // Check model validation
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please fill in all required fields.";
                return Page();
            }

            try
            {
                string query = @"
                    SELECT UserID, UserName, UserRole, UserEmail, StoreID, IsActive
                    FROM Users 
                    WHERE UserName = @User AND Password = @Pass";

                SqlParameter[] parameters = {
                    new SqlParameter("@User", UserName ?? string.Empty),
                    new SqlParameter("@Pass", Password ?? string.Empty)
                };

                DataTable result = _db.ExecuteQuery(query, parameters);

                if (result.Rows.Count > 0)
                {
                    DataRow row = result.Rows[0];

                    bool isActive = row["IsActive"] != DBNull.Value && Convert.ToBoolean(row["IsActive"]);

                    if (!isActive)
                    {
                        ErrorMessage = "This account has been deactivated. Please contact administrator.";
                        return Page();
                    }

                    HttpContext.Session.SetInt32("UserID", Convert.ToInt32(row["UserID"]));
                    HttpContext.Session.SetString("UserName", row["UserName"].ToString());
                    HttpContext.Session.SetString("UserRole", row["UserRole"].ToString());

                    if (row["UserEmail"] != DBNull.Value)
                    {
                        HttpContext.Session.SetString("UserEmail", row["UserEmail"].ToString());
                    }

                    if (row["StoreID"] != DBNull.Value)
                    {
                        HttpContext.Session.SetInt32("StoreID", Convert.ToInt32(row["StoreID"]));
                    }

                    return RedirectToPage("/Dashboard/Index");
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                    return Page();
                }
            }
            catch (SqlException)
            {
                ErrorMessage = "Database error occurred. Please try again later.";
                return Page();
            }
            catch (Exception)
            {
                ErrorMessage = "An unexpected error occurred. Please try again.";
                return Page();
            }
        }
    }
}