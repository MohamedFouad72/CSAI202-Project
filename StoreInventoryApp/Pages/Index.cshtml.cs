using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StoreInventoryApp.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Check if user session exists
            if (HttpContext.Session.GetInt32("UserID") != null)
            {
                // User is logged in, go to Dashboard
                return RedirectToPage("/Dashboard/Index");
            }
            else
            {
                // User is NOT logged in, go to Login
                return RedirectToPage("/Auth/Login");
            }
        }
    }
}