using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StoreInventoryApp.Pages.Auth
{
    public class LogoutModel : PageModel
    {
        public void OnGet()
        {
            // If someone tries to visit /Auth/Logout directly, just clear and redirect
            DoLogout();
        }

        public IActionResult OnPost()
        {
            // This handles the button click from the Navbar
            return DoLogout();
        }

        private IActionResult DoLogout()
        {
            // 1. Clear the Session (Remove UserID, Role, etc.)
            HttpContext.Session.Clear();

            // 2. Redirect to Login Page
            return RedirectToPage("/Auth/Login");
        }
    }
}